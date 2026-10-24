using System.Data;
using System.Globalization;
using System.Text.Json;
using GaRan.Data;
using GaRan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Controllers;

[AllowAnonymous]
[Route("webhooks/payos")]
public sealed class PayOsWebhookController : Controller
{
    private readonly GaRanDbContext _context;
    private readonly IPayOsService _payOsService;
    private readonly ILogger<PayOsWebhookController> _logger;
    private readonly IOrderEmailSender _emailSender;

    public PayOsWebhookController(GaRanDbContext context, IPayOsService payOsService, ILogger<PayOsWebhookController> logger, IOrderEmailSender emailSender)
    {
        _context = context;
        _payOsService = payOsService;
        _logger = logger;
        _emailSender = emailSender;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (!_payOsService.IsConfigured)
        {
            _logger.LogError("payOS webhook called before API credentials were configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        JsonDocument document;
        try { document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken); }
        catch (JsonException) { return BadRequest(new { success = false }); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryProperty(root, "data", out var data) ||
                !TryString(root, "signature", out var signature) || !_payOsService.VerifyWebhook(data, signature))
                return Unauthorized(new { success = false });

            if (!TryInt64(data, "orderCode", out var orderCode) || orderCode <= 0 ||
                !TryInt64(data, "amount", out var amount) || amount <= 0 ||
                !TryString(data, "paymentLinkId", out var paymentLinkId))
                return BadRequest(new { success = false });

            var reference = TryString(data, "reference", out var referenceValue) ? referenceValue : string.Empty;
            var transactionTime = TryString(data, "transactionDateTime", out var transactionDate) ? transactionDate : string.Empty;
            var providerTransactionId = Limit(string.IsNullOrWhiteSpace(reference)
                ? $"{paymentLinkId}:{transactionTime}"
                : reference, 100)!;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var duplicate = await _context.GiaoDichWebhookThanhToans.AnyAsync(x =>
                x.NhaCungCap == "payOS" && x.MaGiaoDichNhaCungCap == providerTransactionId, cancellationToken);
            if (duplicate)
            {
                await transaction.CommitAsync(cancellationToken);
                return Ok(new { success = true, duplicate = true });
            }

            var payment = await _context.ThanhToans
                .Include(x => x.DonHang)!.ThenInclude(x => x!.ChiTietDonHangs).ThenInclude(x => x.MonAn)
                .FirstOrDefaultAsync(x => x.PayOsOrderCode == orderCode, cancellationToken);
            var callbackIsSuccess = TryString(root, "code", out var envelopeCode) && envelopeCode == "00" &&
                TryBoolean(root, "success", out var success) && success &&
                TryString(data, "code", out var transactionCode) && transactionCode == "00" &&
                (!TryString(data, "currency", out var currency) || currency.Equals("VND", StringComparison.OrdinalIgnoreCase));
            var exactExpectedAmount = payment != null && amount == decimal.ToInt64(payment.SoTien);
            var matchingLink = payment != null && string.Equals(payment.PayOsPaymentLinkId, paymentLinkId, StringComparison.Ordinal);
            var orderCanBePaid = payment?.DonHang?.TrangThaiDonHang is not ("Đã hủy" or "Hoàn thành");
            var shouldCredit = callbackIsSuccess && exactExpectedAmount && matchingLink && orderCanBePaid &&
                payment?.PhuongThucThanhToan is "QR" or "Bank";

            var accountNumber = TryString(data, "accountNumber", out var account) ? account : null;
            var eventRow = new GaRan.Models.GiaoDichWebhookThanhToan
            {
                NhaCungCap = "payOS",
                MaGiaoDichNhaCungCap = providerTransactionId,
                MaThamChieuNganHang = Limit(reference, 100),
                MaThanhToan = Limit(paymentLinkId, 100),
                NoiDungChuyenKhoan = TryString(data, "description", out var description) ? Limit(description, 500) : null,
                SoTaiKhoanNhan = Limit(accountNumber, 30),
                LoaiGiaoDich = callbackIsSuccess ? "payment" : "invalid",
                SoTien = amount,
                NhanLuc = DateTime.UtcNow,
                ThanhToanID = payment?.ThanhToanID,
                DaCongVaoThanhToan = shouldCredit
            };
            _context.GiaoDichWebhookThanhToans.Add(eventRow);

            var paymentJustConfirmed = false;
            if (shouldCredit && payment != null && payment.TrangThaiThanhToan != "Đã thanh toán")
            {
                payment.TrangThaiThanhToan = "Đã thanh toán";
                payment.NgayThanhToan = DateTime.Now;
                paymentJustConfirmed = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (paymentJustConfirmed && payment?.DonHang != null)
                await _emailSender.SendPaymentReceivedAsync(payment.DonHang, cancellationToken);

            if (!shouldCredit)
                _logger.LogWarning("payOS transaction {TransactionId} was acknowledged but not matched to a payable order.", providerTransactionId);
            else
                _logger.LogInformation("payOS transaction {TransactionId} was confirmed for payment {PaymentId}.", providerTransactionId, payment!.ThanhToanID);

            return Ok(new { success = true });
        }
    }

    private static bool TryProperty(JsonElement source, string name, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out value)) return true;
        value = default;
        return false;
    }

    private static bool TryString(JsonElement source, string name, out string value)
    {
        if (TryProperty(source, name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool TryInt64(JsonElement source, string name, out long value)
    {
        value = 0;
        if (!TryProperty(source, name, out var property)) return false;
        return property.ValueKind == JsonValueKind.Number
            ? property.TryGetInt64(out value)
            : property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryBoolean(JsonElement source, string name, out bool value)
    {
        value = false;
        if (!TryProperty(source, name, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        value = property.GetBoolean();
        return true;
    }

    private static string? Limit(string? value, int maximum) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maximum)];
}
