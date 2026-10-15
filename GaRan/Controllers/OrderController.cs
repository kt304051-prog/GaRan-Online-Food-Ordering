using GaRan.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GaRan.ViewModels;
using GaRan.Models;
using Microsoft.Extensions.Configuration;
using GaRan.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Data;

namespace GaRan.Controllers;

public class OrderController : Controller
{
    private readonly GaRanDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPayOsService _payOsService;
    public OrderController(GaRanDbContext context, IConfiguration configuration, IPayOsService payOsService)
    {
        _context = context;
        _configuration = configuration;
        _payOsService = payOsService;
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        var role = CurrentRole();
        var id = CurrentUserId();
        var orders = _context.DonHangs.Include(x => x.KhachHang).AsQueryable();

        if (role == "KhachHang")
            orders = orders.Where(x => x.KhachHangID == id);
        else if (role is not ("Admin" or "NhanVien"))
            return Forbid();

        return View(await orders.OrderByDescending(x => x.NgayDat).ToListAsync());
    }

    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.DonHangs.Include(x => x.KhachHang).Include(x => x.ChiTietDonHangs).ThenInclude(x => x.MonAn)
            .Include(x => x.ThanhToans).AsSplitQuery().FirstOrDefaultAsync(x => x.DonHangID == id);
        if (order == null) return NotFound();
        if (!CanAccessOrder(order.KhachHangID)) return Forbid();
        await SetInvoiceSettingsAsync();
        ViewBag.PayOsConfigured = _payOsService.IsConfigured;
        ViewBag.DeliveredAt = await _context.GiaoHangs.Where(x => x.DonHangID == id).Select(x => x.NgayGiaoHang).FirstOrDefaultAsync();
        if (User.IsInRole("KhachHang"))
        {
            var customerId = CurrentUserId();
            ViewBag.ServiceReviewSubmitted = await _context.DanhGias.AnyAsync(x =>
                x.DonHangID == id && x.KhachHangID == customerId && x.LoaiDanhGia == "DichVu");
            ViewBag.ProductReviewSubmittedIds = await _context.DanhGias
                .Where(x => x.DonHangID == id && x.KhachHangID == customerId && x.LoaiDanhGia == "MonAn" && x.MonAnID.HasValue)
                .Select(x => x.MonAnID!.Value)
                .ToListAsync();
        }
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var customerId = User.Identity?.IsAuthenticated == true && User.IsInRole("KhachHang")
            ? CurrentUserId()
            : (int?)null;
        var isGuest = HttpContext.Session.GetInt32("ORDER_CONFIRMATION_ID") == id;
        if (!customerId.HasValue && !isGuest) return Forbid();

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var changed = await _context.DonHangs
            .Where(x => x.DonHangID == id && x.TrangThaiDonHang == "Chờ xác nhận" &&
                (customerId.HasValue ? x.KhachHangID == customerId.Value : x.KhachHangID == null) &&
                !_context.ThanhToans.Any(payment => payment.DonHangID == id && payment.TrangThaiThanhToan == "Đã thanh toán"))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.TrangThaiDonHang, "Đã hủy"), cancellationToken);

        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["Message"] = "Không thể hủy đơn này. Chỉ được hủy khi đơn còn chờ xác nhận và chưa thanh toán.";
            return RedirectToOrderPage(id, isGuest);
        }

        var order = await _context.DonHangs.FirstAsync(x => x.DonHangID == id, cancellationToken);
        if (order.KhachHangID.HasValue && order.DiemDaDung > 0 && !order.DiemDaHoanLai)
        {
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(x => x.KhachHangID == order.KhachHangID.Value, cancellationToken);
            if (customer != null)
            {
                customer.DiemTichLuy += order.DiemDaDung;
                order.DiemDaHoanLai = true;
            }
        }

        var payment = await _context.ThanhToans.FirstOrDefaultAsync(x => x.DonHangID == id, cancellationToken);
        if (payment != null && payment.TrangThaiThanhToan != "Đã thanh toán")
            payment.TrangThaiThanhToan = "Đã hủy";
        var delivery = await _context.GiaoHangs.FirstOrDefaultAsync(x => x.DonHangID == id, cancellationToken);
        if (delivery != null)
        {
            delivery.TrangThaiGiaoHang = "Giao thất bại";
            delivery.NgayGiaoHang ??= DateTime.Now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        TempData["Message"] = "Đơn hàng đã được hủy.";
        return RedirectToOrderPage(id, isGuest);
    }

    [HttpGet]
    public async Task<IActionResult> PaymentStatus(int id, CancellationToken cancellationToken)
    {
        var order = await _context.DonHangs.Include(x => x.ThanhToans)
            .FirstOrDefaultAsync(x => x.DonHangID == id, cancellationToken);
        if (order == null) return NotFound();

        var isGuestConfirmation = HttpContext.Session.GetInt32("ORDER_CONFIRMATION_ID") == id;
        var hasAccess = isGuestConfirmation || User.Identity?.IsAuthenticated == true && CanAccessOrder(order.KhachHangID);
        if (!hasAccess) return Forbid();

        var payment = order.ThanhToans.FirstOrDefault();
        return Json(new { paid = payment?.TrangThaiThanhToan == "Đã thanh toán", status = payment?.TrangThaiThanhToan ?? "Chưa thanh toán" });
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        // Guest checkout can view only the order created in this browser session.
        if (HttpContext.Session.GetInt32("ORDER_CONFIRMATION_ID") != id) return NotFound();
        var order = await _context.DonHangs.Include(x => x.KhachHang).Include(x => x.ChiTietDonHangs).ThenInclude(x => x.MonAn)
            .Include(x => x.ThanhToans).AsSplitQuery().FirstOrDefaultAsync(x => x.DonHangID == id);
        if (order == null) return NotFound();
        await SetInvoiceSettingsAsync();
        ViewBag.PayOsConfigured = _payOsService.IsConfigured;
        ViewBag.DeliveredAt = await _context.GiaoHangs.Where(x => x.DonHangID == id).Select(x => x.NgayGiaoHang).FirstOrDefaultAsync();
        ViewData["Title"] = "Xác nhận đơn hàng";
        return View("Details", order);
    }

    [HttpGet]
    public IActionResult Lookup(string? code) => View(new OrderLookupVM { Code = code ?? string.Empty });

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive")]
    public async Task<IActionResult> Lookup(OrderLookupVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var digits = vm.Code.Trim().Replace("DH", "", StringComparison.OrdinalIgnoreCase);
        if (!int.TryParse(digits, out var orderId))
        {
            ModelState.AddModelError(nameof(vm.Code), "Mã đơn hàng không hợp lệ.");
            return View(vm);
        }
        var contact = vm.Contact.Trim();
        var order = await _context.DonHangs
            .Include(x => x.KhachHang)
            .Include(x => x.ChiTietDonHangs).ThenInclude(x => x.MonAn)
            .Include(x => x.ThanhToans)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.DonHangID == orderId &&
                ((x.EmailKhachDat != null && x.EmailKhachDat == contact) || x.SoDienThoaiNhan == contact));
        if (order == null)
        {
            ModelState.AddModelError("", "Không tìm thấy đơn hàng với mã và thông tin liên hệ đã nhập.");
            return View(vm);
        }
        vm.Order = order;
        vm.NgayGiaoHang = await _context.GiaoHangs.Where(x => x.DonHangID == orderId).Select(x => x.NgayGiaoHang).FirstOrDefaultAsync();
        await SetInvoiceSettingsAsync();
        return View(vm);
    }

    [HttpPost, Authorize(Roles = "Admin,NhanVien"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string status)
    {
        var allowedStatuses = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chế biến", "Chờ giao hàng", "Đang giao", "Hoàn thành", "Đã hủy" };
        if (!allowedStatuses.Contains(status)) return BadRequest();

        var order = await _context.DonHangs.FindAsync(id);
        if (order == null) return NotFound();
        if (!OrderStatusFlow.CanTransition(order.TrangThaiDonHang, status)) return BadRequest();
        if (status == order.TrangThaiDonHang) return RedirectToAction(nameof(Details), new { id });
        order.TrangThaiDonHang = status;
        if (status is "Chờ giao hàng" or "Đang giao" && !await _context.GiaoHangs.AnyAsync(x => x.DonHangID == id))
        {
            _context.GiaoHangs.Add(new()
            {
                DonHangID = id,
                TrangThaiGiaoHang = status == "Đang giao" ? "Đang giao" : "Chờ nhận",
                NgayNhanDon = status == "Đang giao" ? DateTime.Now : null
            });
        }

        if (status == "Hoàn thành")
        {
            var delivery = await _context.GiaoHangs.FirstOrDefaultAsync(x => x.DonHangID == id);
            if (delivery == null)
            {
                _context.GiaoHangs.Add(new GiaoHang { DonHangID = id, TrangThaiGiaoHang = "Giao thành công", NgayNhanDon = DateTime.Now, NgayGiaoHang = DateTime.Now });
            }
            else
            {
                delivery.TrangThaiGiaoHang = "Giao thành công";
                delivery.NgayGiaoHang ??= DateTime.Now;
            }
            if (!await _context.HoaDons.AnyAsync(x => x.DonHangID == id))
                _context.HoaDons.Add(new HoaDon { DonHangID = id, TongTien = order.TongTien, NguoiLapID = CurrentUserId() });

            if (order.KhachHangID.HasValue && !order.DiemDaCongThuong)
            {
                var customer = await _context.KhachHangs.FirstOrDefaultAsync(x => x.KhachHangID == order.KhachHangID.Value);
                if (customer != null)
                {
                    order.DiemDaCong = (int)Math.Floor(Math.Max(0, order.TienHang) / 10000m);
                    customer.DiemTichLuy += order.DiemDaCong;
                    order.DiemDaCongThuong = true;
                }
            }
        }
        if (status == "Đã hủy" && order.KhachHangID.HasValue && order.DiemDaDung > 0 && !order.DiemDaHoanLai)
        {
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(x => x.KhachHangID == order.KhachHangID.Value);
            if (customer != null)
            {
                customer.DiemTichLuy += order.DiemDaDung;
                order.DiemDaHoanLai = true;
            }
        }
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool CanAccessOrder(int? customerId)
    {
        var role = CurrentRole();
        var id = CurrentUserId();

        if (role is "Admin" or "NhanVien") return true;
        if (role == "KhachHang") return customerId == id;
        return false;
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string CurrentRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    private IActionResult RedirectToOrderPage(int id, bool guest) => guest
        ? RedirectToAction(nameof(Confirmation), new { id })
        : RedirectToAction(nameof(Details), new { id });

    private async Task SetInvoiceSettingsAsync()
    {
        var settings = await _context.CauHinhGiaoHangs.FindAsync(1);
        var bankName = ConfigValue(settings?.TenNganHang, _configuration["Bank:Name"]);
        var bankBin = ConfigValue(settings?.NganHangBin, _configuration["Bank:Bin"]);
        var bankAccount = ConfigValue(settings?.SoTaiKhoan, _configuration["Bank:AccountNumber"]);
        var bankOwner = ConfigValue(settings?.ChuTaiKhoan, _configuration["Bank:AccountName"]);
        bankBin = bankBin?.Trim();
        if (bankBin == "000000" && IsMbBank(bankName))
            bankBin = "970422";
        if (bankBin == "000000") bankBin = null;
        ViewBag.BankName = bankName;
        ViewBag.BankAccount = bankAccount?.Trim();
        ViewBag.BankOwner = bankOwner?.Trim();
        ViewBag.BankBin = bankBin;
        ViewBag.StoreName = ConfigValue(settings?.TenCuaHang, _configuration["Store:Name"]);
        ViewBag.StorePhone = ConfigValue(settings?.DienThoaiCuaHang, _configuration["Store:Phone"]);
        ViewBag.StoreEmail = ConfigValue(settings?.EmailCuaHang, _configuration["Store:Email"]);
        ViewBag.StoreAddress = ConfigValue(settings?.DiaChiCuaHang, _configuration["Delivery:StoreAddress"]);
    }

    private static string? ConfigValue(string? configured, string? fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured;

    private static bool IsMbBank(string? bankName)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(bankName?.ToUpperInvariant() ?? "", "[^A-Z0-9]", "");
        return normalized == "MB" || normalized.StartsWith("MBBANK", StringComparison.Ordinal) ||
               normalized.Contains("QUANDOI", StringComparison.Ordinal) || normalized.Contains("MILITARYBANK", StringComparison.Ordinal);
    }
}
