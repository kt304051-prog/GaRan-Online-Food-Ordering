using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GaRan.Data;
using GaRan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Controllers;

[AllowAnonymous]
[Route("webhooks/sepay")]
public sealed class SePayWebhookController : Controller
{
    private static readonly Regex PaymentCodeRegex = new("[A-Za-z0-9]{3,40}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly GaRanDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SePayWebhookController> _logger;
    private readonly IOrderEmailSender _emailSender;

    public SePayWebhookController(GaRanDbContext context, IConfiguration configuration, ILogger<SePayWebhookController> logger, IOrderEmailSender emailSender)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _emailSender = emailSender;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var secret = _configuration["SePay:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("SePay webhook received while SePay:WebhookSecret is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        if (!IsAuthenticRequest(rawBody, secret, Request.Headers["X-SePay-Timestamp"].ToString(), Request.Headers["X-SePay-Signature"].ToString()))
            return Unauthorized(new { success = false });

        SePayTransfer? transfer;
        try
        {
            transfer = JsonSerializer.Deserialize<SePayTransfer>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return BadRequest(new { success = false });
        }

        if (transfer == null || transfer.Id <= 0 || transfer.TransferAmount <= 0)
            return BadRequest(new { success = false });

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var duplicate = await _context.GiaoDichWebhookThanhToans.AnyAsync(x =>
            x.NhaCungCap == "SePay" && x.MaGiaoDichNhaCungCap == transfer.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        if (duplicate)
        {
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { success = true, duplicate = true });
        }

        var bankAccount = await _context.CauHinhGiaoHangs.AsNoTracking()
            .Where(x => x.CauHinhGiaoHangID == 1)
            .Select(x => x.SoTaiKhoan)
            .FirstOrDefaultAsync(cancellationToken);
        bankAccount = string.IsNullOrWhiteSpace(bankAccount) ? _configuration["Bank:AccountNumber"] : bankAccount;
        var accountMatches = !string.IsNullOrWhiteSpace(bankAccount) && DigitsOnly(bankAccount) == DigitsOnly(transfer.AccountNumber ?? string.Empty);
        var isIncoming = string.Equals(transfer.TransferType, "in", StringComparison.OrdinalIgnoreCase);
        var codes = ExtractPaymentCodes(transfer.Code, transfer.Content);

        GaRan.Models.ThanhToan? payment = null;
        if (accountMatches && isIncoming && codes.Count > 0)
        {
            payment = await _context.ThanhToans.Include(x => x.DonHang)!.ThenInclude(x => x!.ChiTietDonHangs).ThenInclude(x => x.MonAn)
                .FirstOrDefaultAsync(x => codes.Contains(x.MaGiaoDich!) &&
                    (x.PhuongThucThanhToan == "QR" || x.PhuongThucThanhToan == "Bank"), cancellationToken);
        }

        var eventRow = new GaRan.Models.GiaoDichWebhookThanhToan
        {
            NhaCungCap = "SePay",
            MaGiaoDichNhaCungCap = transfer.Id.ToString(CultureInfo.InvariantCulture),
            MaThamChieuNganHang = Limit(transfer.ReferenceCode, 100),
            MaThanhToan = Limit(transfer.Code, 100),
            NoiDungChuyenKhoan = Limit(transfer.Content, 500),
            SoTaiKhoanNhan = Limit(transfer.AccountNumber, 30),
            LoaiGiaoDich = Limit(transfer.TransferType, 30),
            SoTien = transfer.TransferAmount,
            NhanLuc = DateTime.UtcNow,
            ThanhToanID = payment?.ThanhToanID,
            DaCongVaoThanhToan = payment != null && accountMatches && isIncoming && payment.DonHang?.TrangThaiDonHang != "Đã hủy"
        };
        _context.GiaoDichWebhookThanhToans.Add(eventRow);

        var paymentJustConfirmed = false;
        if (eventRow.DaCongVaoThanhToan && payment != null)
        {
            await _context.SaveChangesAsync(cancellationToken);
            var totalReceived = await _context.GiaoDichWebhookThanhToans
                .Where(x => x.ThanhToanID == payment.ThanhToanID && x.DaCongVaoThanhToan)
                .SumAsync(x => (decimal?)x.SoTien, cancellationToken) ?? 0m;
            if (totalReceived >= payment.SoTien && payment.TrangThaiThanhToan != "Đã thanh toán")
            {
                payment.TrangThaiThanhToan = "Đã thanh toán";
                payment.NgayThanhToan = DateTime.Now;
                paymentJustConfirmed = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (paymentJustConfirmed && payment?.DonHang != null)
            await _emailSender.SendPaymentReceivedAsync(payment.DonHang, cancellationToken);

        if (!eventRow.DaCongVaoThanhToan)
            _logger.LogWarning("SePay transaction {TransactionId} was recorded but not matched to an order payment.", transfer.Id);
        else
            _logger.LogInformation("SePay transaction {TransactionId} was reconciled with payment {PaymentId}.", transfer.Id, payment!.ThanhToanID);

        return Ok(new { success = true });
    }

    private static bool IsAuthenticRequest(string body, string secret, string timestampHeader, string signatureHeader)
    {
        if (!long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)) return false;
        var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp);
        if (age > 300 || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;

        var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, signedPayload);
        var expected = Encoding.ASCII.GetBytes("sha256=" + Convert.ToHexString(hash).ToLowerInvariant());
        var actual = Encoding.ASCII.GetBytes(signatureHeader);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static List<string> ExtractPaymentCodes(string? code, string? content)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(code)) candidates.Add(NormalizeCode(code));
        if (!string.IsNullOrWhiteSpace(content))
            candidates.AddRange(PaymentCodeRegex.Matches(content).Select(match => NormalizeCode(match.Value)));
        return candidates.Where(value => value.Length is >= 3 and <= 40).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeCode(string value) => new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static string DigitsOnly(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string? Limit(string? value, int maximum) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maximum)];

    private sealed class SePayTransfer
    {
        [JsonPropertyName("id")] public long Id { get; init; }
        [JsonPropertyName("accountNumber")] public string? AccountNumber { get; init; }
        [JsonPropertyName("code")] public string? Code { get; init; }
        [JsonPropertyName("content")] public string? Content { get; init; }
        [JsonPropertyName("transferType")] public string? TransferType { get; init; }
        [JsonPropertyName("transferAmount")] public decimal TransferAmount { get; init; }
        [JsonPropertyName("referenceCode")] public string? ReferenceCode { get; init; }
    }
}
