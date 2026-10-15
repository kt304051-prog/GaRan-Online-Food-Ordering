using System.Globalization;
using System.Text;
using GaRan.Data;
using GaRan.Models;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Services;

public interface IOrderEmailSender
{
    Task SendOrderCreatedAsync(DonHang order, CancellationToken cancellationToken = default);
    Task SendPaymentReceivedAsync(DonHang order, CancellationToken cancellationToken = default);
    Task<bool> SendPasswordResetCodeAsync(string recipient, string code, CancellationToken cancellationToken = default);
}

public sealed class OrderEmailSender : IOrderEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderEmailSender> _logger;
    private readonly GaRanDbContext _context;

    public OrderEmailSender(IConfiguration configuration, ILogger<OrderEmailSender> logger, GaRanDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _context = context;
    }

    public Task SendOrderCreatedAsync(DonHang order, CancellationToken cancellationToken = default) =>
        SendAsync(order, "Đã nhận đơn hàng", "Cửa hàng đã nhận đơn và sẽ cập nhật trạng thái xử lý.", cancellationToken);

    public Task SendPaymentReceivedAsync(DonHang order, CancellationToken cancellationToken = default) =>
        SendAsync(order, "Đã nhận thanh toán", "Cửa hàng đã xác nhận nhận đủ thanh toán cho đơn hàng.", cancellationToken);

    public async Task<bool> SendPasswordResetCodeAsync(string recipient, string code, CancellationToken cancellationToken = default)
    {
        try
        {
            var host = _configuration["Smtp:Host"];
            var storeSettings = await _context.CauHinhGiaoHangs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.CauHinhGiaoHangID == 1, cancellationToken);
            var fromEmail = FirstConfiguredValue(_configuration["Smtp:FromEmail"], storeSettings?.EmailCuaHang, _configuration["Store:Email"]);
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("Password reset email skipped: SMTP sender is not configured.");
                return false;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(FirstConfiguredValue(_configuration["Smtp:FromName"], storeSettings?.TenCuaHang, _configuration["Store:Name"], "GaRan"), fromEmail));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = "GaRan - Mã đặt lại mật khẩu";
            message.Body = new TextPart("plain")
            {
                Text = $"Mã xác nhận đặt lại mật khẩu GaRan của bạn là: {code}\nMã có hiệu lực trong 15 phút. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này."
            };

            var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
            var enableSsl = _configuration.GetValue("Smtp:EnableSsl", true);
            using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = 15000 };
            var socketOptions = enableSsl
                ? port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
                : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            var username = _configuration["Smtp:Username"];
            if (!string.IsNullOrWhiteSpace(username))
            {
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                await client.AuthenticateAsync(username, _configuration["Smtp:Password"] ?? string.Empty, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send a password reset email.");
            return false;
        }
    }

    private async Task SendAsync(DonHang order, string subject, string statusMessage, CancellationToken cancellationToken)
    {
        var recipient = string.IsNullOrWhiteSpace(order.EmailKhachDat) ? order.KhachHang?.Email : order.EmailKhachDat;
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(recipient) || string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("Order email skipped for order {OrderId}: SMTP is not configured.", order.DonHangID);
            return;
        }

        try
        {
            var storedOrder = await _context.DonHangs.AsNoTracking()
                .Include(x => x.ChiTietDonHangs)
                .FirstOrDefaultAsync(x => x.DonHangID == order.DonHangID, cancellationToken);
            if (storedOrder != null) order = storedOrder;

            var storeSettings = await _context.CauHinhGiaoHangs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.CauHinhGiaoHangID == 1, cancellationToken);
            var storeName = FirstConfiguredValue(
                _configuration["Smtp:FromName"],
                storeSettings?.TenCuaHang,
                _configuration["Store:Name"],
                "GaRan");
            var fromEmail = FirstConfiguredValue(
                _configuration["Smtp:FromEmail"],
                storeSettings?.EmailCuaHang,
                _configuration["Store:Email"]);
            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogInformation("Order email skipped for order {OrderId}: no sender email is configured.", order.DonHangID);
                return;
            }

            var foodIds = order.ChiTietDonHangs.Select(x => x.MonAnID).Distinct().ToList();
            var foodNames = await _context.MonAns.AsNoTracking().Where(x => foodIds.Contains(x.MonAnID))
                .ToDictionaryAsync(x => x.MonAnID, x => x.TenMon, cancellationToken);
            var payment = await _context.ThanhToans.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DonHangID == order.DonHangID, cancellationToken);
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(storeName, fromEmail));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = $"{storeName} - {subject} #{order.DonHangID}";
            message.Body = new TextPart("plain")
            {
                Text = BuildBody(order, payment, statusMessage, foodNames, storeSettings)
            };

            var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
            var enableSsl = _configuration.GetValue("Smtp:EnableSsl", true);
            using var client = new MailKit.Net.Smtp.SmtpClient
            {
                Timeout = 15000
            };
            var username = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];
            var socketOptions = enableSsl
                ? port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
                : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(username))
            {
                // Gmail SMTP app passwords use LOGIN/PLAIN, not OAuth bearer tokens.
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                await client.AuthenticateAsync(username, password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            _logger.LogInformation("Order email sent for order {OrderId}.", order.DonHangID);
        }
        catch (SmtpCommandException ex)
        {
            _logger.LogError(ex,
                "SMTP server rejected delivery for order {OrderId}. StatusCode={StatusCode}; ErrorCode={ErrorCode}; ServerMessage={ServerMessage}.",
                order.DonHangID, ex.StatusCode, ex.ErrorCode, ex.Message);
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "SMTP protocol failed for order {OrderId}: {ProtocolMessage}.", order.DonHangID, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send order email for order {OrderId}.", order.DonHangID);
        }
    }

    private string BuildBody(
        DonHang order,
        ThanhToan? payment,
        string statusMessage,
        IReadOnlyDictionary<int, string> foodNames,
        CauHinhGiaoHang? storeSettings)
    {
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        var storeName = FirstConfiguredValue(storeSettings?.TenCuaHang, _configuration["Store:Name"], "GaRan");
        var body = new StringBuilder()
            .AppendLine(storeName)
            .AppendLine($"Đơn hàng #{order.DonHangID}")
            .AppendLine($"Ngày đặt: {order.NgayDat:dd/MM/yyyy HH:mm}")
            .AppendLine(statusMessage)
            .AppendLine()
            .AppendLine("Món đã đặt:");

        foreach (var detail in order.ChiTietDonHangs)
            body.AppendLine($"- {foodNames.GetValueOrDefault(detail.MonAnID, detail.MonAn?.TenMon ?? "Món ăn")} × {detail.SoLuong}: {detail.ThanhTien.ToString("N0", culture)} đ");

        if (order.TienGiamThanhVien > 0)
            body.AppendLine($"Ưu đãi thành viên VIP: -{order.TienGiamThanhVien.ToString("N0", culture)} đ");

        body.AppendLine().AppendLine($"Tổng thanh toán: {order.TongTien.ToString("N0", culture)} đ");
        body.AppendLine($"Địa chỉ giao hàng: {order.DiaChiGiaoHang}");

        if (payment != null)
        {
            body.AppendLine($"Phương thức thanh toán: {PaymentMethodName(payment.PhuongThucThanhToan)}")
                .AppendLine($"Tình trạng thanh toán: {payment.TrangThaiThanhToan}");

            if (payment.TrangThaiThanhToan != "Đã thanh toán" && (payment.PhuongThucThanhToan is "QR" or "Bank"))
                AppendTransferInstructions(body, order, payment, storeSettings, culture);
        }

        body.AppendLine().AppendLine("Cảm ơn bạn đã đặt hàng.");
        return body.ToString();
    }

    private static void AppendTransferInstructions(
        StringBuilder body,
        DonHang order,
        ThanhToan payment,
        CauHinhGiaoHang? storeSettings,
        CultureInfo culture)
    {
        body.AppendLine().AppendLine("Thông tin chuyển khoản:");
        if (!string.IsNullOrWhiteSpace(storeSettings?.TenNganHang))
            body.AppendLine($"Ngân hàng: {storeSettings.TenNganHang}");
        if (!string.IsNullOrWhiteSpace(storeSettings?.ChuTaiKhoan))
            body.AppendLine($"Chủ tài khoản: {storeSettings.ChuTaiKhoan}");
        if (!string.IsNullOrWhiteSpace(storeSettings?.SoTaiKhoan))
            body.AppendLine($"Số tài khoản: {storeSettings.SoTaiKhoan}");

        var transferNote = string.IsNullOrWhiteSpace(payment.MaGiaoDich)
            ? $"WOCORDER{order.DonHangID}"
            : payment.MaGiaoDich;
        body.AppendLine($"Số tiền: {payment.SoTien.ToString("N0", culture)} đ")
            .AppendLine($"Nội dung chuyển khoản: {transferNote}");

        if (!string.IsNullOrWhiteSpace(storeSettings?.NganHangBin) && !string.IsNullOrWhiteSpace(storeSettings.SoTaiKhoan))
        {
            var amount = decimal.Round(payment.SoTien, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
            var qrUrl = $"https://img.vietqr.io/image/{Uri.EscapeDataString(storeSettings.NganHangBin)}-{Uri.EscapeDataString(storeSettings.SoTaiKhoan)}-compact2.png?amount={amount}&addInfo={Uri.EscapeDataString(transferNote)}&accountName={Uri.EscapeDataString(storeSettings.ChuTaiKhoan ?? string.Empty)}";
            body.AppendLine($"Mã QR chuyển khoản: {qrUrl}");
        }

        body.AppendLine("Đơn hàng sẽ được cập nhật sau khi cửa hàng xác nhận đã nhận tiền.");
    }

    private static string PaymentMethodName(string paymentMethod) => paymentMethod switch
    {
        "COD" => "Thanh toán khi nhận hàng (COD)",
        "QR" => "Thanh toán QR",
        "Bank" => "Chuyển khoản ngân hàng",
        _ => paymentMethod
    };

    private static string FirstConfiguredValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
