using GaRan.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GaRan.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;

namespace GaRan.Controllers;

public class PaymentController : Controller
{
    private readonly GaRanDbContext _context;
    private readonly IOrderEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IPayOsService _payOsService;

    public PaymentController(GaRanDbContext context, IOrderEmailSender emailSender, IConfiguration configuration, IPayOsService payOsService)
    {
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
        _payOsService = payOsService;
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive")]
    public async Task<IActionResult> PayOsCheckout(int orderId, CancellationToken cancellationToken)
    {
        var payment = await _context.ThanhToans
            .Include(x => x.DonHang)!.ThenInclude(x => x!.ChiTietDonHangs).ThenInclude(x => x.MonAn)
            .FirstOrDefaultAsync(x => x.DonHangID == orderId, cancellationToken);
        if (payment?.DonHang == null) return NotFound();

        var order = payment.DonHang;
        var isGuest = HttpContext.Session.GetInt32("ORDER_CONFIRMATION_ID") == orderId;
        var isCustomer = User.Identity?.IsAuthenticated == true && User.IsInRole("KhachHang") &&
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var customerId) && order.KhachHangID == customerId;
        if (!isGuest && !isCustomer) return Forbid();

        if (!_payOsService.IsConfigured)
        {
            TempData["Message"] = "Thanh toán tự động chưa bật: cửa hàng cần cấu hình Client ID, API Key và Checksum Key của payOS.";
            return RedirectToOrder(orderId, isGuest);
        }
        if (payment.PhuongThucThanhToan is not ("QR" or "Bank") || payment.TrangThaiThanhToan != "Đang chờ thanh toán")
        {
            TempData["Message"] = "Đơn hàng chưa ở trạng thái chờ thanh toán QR.";
            return RedirectToOrder(orderId, isGuest);
        }
        if (order.TrangThaiDonHang is "Đã hủy" or "Hoàn thành")
        {
            TempData["Message"] = "Không thể thanh toán đơn hàng đã kết thúc.";
            return RedirectToOrder(orderId, isGuest);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(payment.PayOsCheckoutUrl) && !string.IsNullOrWhiteSpace(payment.PayOsPaymentLinkId))
            {
                var linkStatus = await _payOsService.GetPaymentLinkStatusAsync(payment.PayOsPaymentLinkId, cancellationToken);
                if (linkStatus is "PENDING" or "PROCESSING") return Redirect(payment.PayOsCheckoutUrl);
                if (linkStatus == "PAID")
                {
                    TempData["Message"] = "payOS ghi nhận giao dịch đang thanh toán. Hệ thống sẽ cập nhật đơn sau khi nhận webhook xác thực.";
                    return RedirectToOrder(orderId, isGuest);
                }
                if (linkStatus is not ("CANCELLED" or "EXPIRED"))
                    throw new InvalidOperationException("payOS trả về trạng thái liên kết không xác định. Vui lòng liên hệ cửa hàng.");

                payment.PayOsCheckoutUrl = null;
                payment.PayOsPaymentLinkId = null;
                payment.PayOsOrderCode = null;
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (!payment.PayOsOrderCode.HasValue)
            {
                int providerOrderCode;
                do
                {
                    providerOrderCode = RandomNumberGenerator.GetInt32(100_000_000, int.MaxValue);
                } while (await _context.ThanhToans.AnyAsync(x => x.PayOsOrderCode == providerOrderCode, cancellationToken));
                payment.PayOsOrderCode = providerOrderCode;
                await _context.SaveChangesAsync(cancellationToken);
            }

            var returnUrl = BuildPayOsReturnUrl(orderId, cancelled: false);
            var cancelUrl = BuildPayOsReturnUrl(orderId, cancelled: true);
            var checkout = await _payOsService.CreatePaymentLinkAsync(order, payment, payment.PayOsOrderCode.Value, returnUrl, cancelUrl, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return Redirect(checkout.CheckoutUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Avoid logging provider credentials or response bodies.
            HttpContext.RequestServices.GetRequiredService<ILogger<PaymentController>>()
                .LogWarning("Could not start payOS checkout for order {OrderId}: {Reason}", orderId, ex.Message);
            TempData["Message"] = ex.Message;
            return RedirectToOrder(orderId, isGuest);
        }
    }

    [HttpGet]
    public async Task<IActionResult> PayOsReturn(int orderId, bool cancelled = false, CancellationToken cancellationToken = default)
    {
        var payment = await _context.ThanhToans.Include(x => x.DonHang)
            .FirstOrDefaultAsync(x => x.DonHangID == orderId, cancellationToken);
        if (payment?.DonHang == null) return NotFound();
        var isGuest = HttpContext.Session.GetInt32("ORDER_CONFIRMATION_ID") == orderId;
        var isCustomer = User.Identity?.IsAuthenticated == true && User.IsInRole("KhachHang") &&
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var customerId) && payment.DonHang.KhachHangID == customerId;
        if (!isGuest && !isCustomer) return Forbid();

        TempData["Message"] = payment.TrangThaiThanhToan == "Đã thanh toán"
            ? "payOS đã xác nhận đơn hàng đã thanh toán."
            : cancelled
                ? "Bạn đã quay lại từ trang payOS. Hệ thống đang chờ xác nhận giao dịch từ payOS."
                : "Đã quay lại từ payOS. Đơn chỉ chuyển sang đã thanh toán sau khi máy chủ nhận xác nhận an toàn từ payOS.";
        return RedirectToOrder(orderId, isGuest);
    }

    [HttpPost, Authorize(Roles = "KhachHang"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int orderId, string method = "COD")
    {
        if (method is not ("COD" or "QR" or "Bank")) return BadRequest();
        var settings = await _context.CauHinhGiaoHangs.AsNoTracking().FirstOrDefaultAsync(x => x.CauHinhGiaoHangID == 1);
        if ((method == "COD" && settings?.BatCOD == false) ||
            ((method is "QR" or "Bank") && settings?.BatVietQR == false))
        {
            TempData["Message"] = "Phương thức thanh toán này hiện đang được cửa hàng tạm khóa.";
            return RedirectToAction("Details", "Order", new { id = orderId });
        }

        var customerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var payment = await _context.ThanhToans
            .Include(x => x.DonHang)
            .FirstOrDefaultAsync(x => x.DonHangID == orderId);
        if (payment == null) return NotFound();
        if (payment.DonHang?.KhachHangID != customerId) return Forbid();

        if (payment.TrangThaiThanhToan == "Đã thanh toán")
        {
            TempData["Message"] = "Đơn hàng này đã ghi nhận thanh toán.";
            return RedirectToAction("Details", "Order", new { id = orderId });
        }

        if (payment.DonHang?.TrangThaiDonHang is "Đã hủy" or "Hoàn thành")
        {
            TempData["Message"] = "Không thể thay đổi phương thức thanh toán cho đơn hàng đã kết thúc.";
            return RedirectToAction("Details", "Order", new { id = orderId });
        }

        var paymentMethodChanged = payment.PhuongThucThanhToan != method;
        if (paymentMethodChanged)
        {
            payment.PayOsCheckoutUrl = null;
            payment.PayOsPaymentLinkId = null;
            payment.PayOsOrderCode = null;
        }
        payment.PhuongThucThanhToan = method;
        payment.TrangThaiThanhToan = method == "COD" ? "Chưa thanh toán" : "Đang chờ thanh toán";
        payment.NgayThanhToan = null;
        payment.SoTien = payment.DonHang!.TongTien;
        var transferPrefix = await _context.CauHinhGiaoHangs.AsNoTracking()
            .Where(x => x.CauHinhGiaoHangID == 1)
            .Select(x => x.TienToNoiDung)
            .FirstOrDefaultAsync();
        transferPrefix = string.IsNullOrWhiteSpace(transferPrefix) ? "WOCORDER" : transferPrefix.Trim().ToUpperInvariant();
        payment.MaGiaoDich ??= $"{transferPrefix}{orderId}";

        await _context.SaveChangesAsync();

        if (paymentMethodChanged && payment.DonHang != null)
            await _emailSender.SendOrderCreatedAsync(payment.DonHang);

        TempData["Message"] = method == "COD"
            ? "Đơn hàng COD đã được ghi nhận. Bạn thanh toán khi nhận hàng."
            : "Đơn hàng đang chờ cửa hàng xác nhận chuyển khoản.";

        return RedirectToAction("Details", "Order", new { id = orderId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,NhanVien")]
    public async Task<IActionResult> Confirm(int orderId)
    {
        var payment = await _context.ThanhToans.Include(x => x.DonHang).ThenInclude(x => x!.ChiTietDonHangs).ThenInclude(x => x.MonAn).FirstOrDefaultAsync(x => x.DonHangID == orderId);
        if (payment == null) return NotFound();
        if (payment.TrangThaiThanhToan == "Đã thanh toán")
        {
            TempData["Message"] = "Đơn hàng này đã được ghi nhận thanh toán.";
            return RedirectToAction("Details", "OrderAdmin", new { area = "Admin", id = orderId });
        }
        if (payment.DonHang?.TrangThaiDonHang == "Đã hủy")
        {
            TempData["Message"] = "Không thể xác nhận thanh toán cho đơn hàng đã hủy.";
            return RedirectToAction("Details", "OrderAdmin", new { area = "Admin", id = orderId });
        }
        if (payment.PhuongThucThanhToan == "COD" && payment.DonHang?.TrangThaiDonHang != "Hoàn thành")
        {
            TempData["Message"] = "Chỉ xác nhận đã thu COD sau khi đơn giao thành công.";
            return RedirectToAction("Details", "OrderAdmin", new { area = "Admin", id = orderId });
        }
        if (payment.PhuongThucThanhToan is "QR" or "Bank" && payment.TrangThaiThanhToan != "Đang chờ thanh toán")
        {
            TempData["Message"] = "Đơn chuyển khoản chưa ở trạng thái chờ xác nhận.";
            return RedirectToAction("Details", "OrderAdmin", new { area = "Admin", id = orderId });
        }
        payment.TrangThaiThanhToan = "Đã thanh toán";
        payment.NgayThanhToan = DateTime.Now;
        await _context.SaveChangesAsync();
        if (payment.DonHang != null) await _emailSender.SendPaymentReceivedAsync(payment.DonHang);
        TempData["Message"] = "Đã xác nhận thanh toán cho đơn hàng.";
        return RedirectToAction("Details", "OrderAdmin", new { area = "Admin", id = orderId });
    }

    private IActionResult RedirectToOrder(int orderId, bool guestOwnsOrder) => guestOwnsOrder
        ? RedirectToAction("Confirmation", "Order", new { id = orderId })
        : RedirectToAction("Details", "Order", new { id = orderId });

    private string BuildPayOsReturnUrl(int orderId, bool cancelled)
    {
        var routePath = Url.Action(nameof(PayOsReturn), "Payment", new { orderId, cancelled });
        var publicBaseUrl = _configuration["PayOS:PublicBaseUrl"];
        if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var configuredBase) && configuredBase.Scheme == Uri.UriSchemeHttps)
            return new Uri(new Uri(configuredBase.AbsoluteUri.TrimEnd('/') + "/"), routePath?.TrimStart('/') ?? string.Empty).AbsoluteUri;
        return Url.Action(nameof(PayOsReturn), "Payment", new { orderId, cancelled }, Request.Scheme)
            ?? throw new InvalidOperationException("Không tạo được địa chỉ quay lại từ payOS.");
    }

}
