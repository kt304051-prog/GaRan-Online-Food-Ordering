using GaRan.Data;
using GaRan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using GaRan.Services;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin,NhanVien")]
public class OrderAdminController : Controller
{
    private readonly GaRanDbContext _context;
    private readonly IConfiguration _configuration;
    public OrderAdminController(GaRanDbContext context, IConfiguration configuration) { _context = context; _configuration = configuration; }

    public async Task<IActionResult> Index(string? status, DateTime? date)
    {
        var selectedDate = (date ?? DateTime.Today).Date;
        var nextDate = selectedDate.AddDays(1);
        var query = _context.DonHangs
            .Where(x => x.NgayDat >= selectedDate && x.NgayDat < nextDate)
            .Include(x => x.KhachHang).Include(x => x.ThanhToans).Include(x => x.NhanVien).AsQueryable();
        var orders = await query.OrderByDescending(x => x.NgayDat).ToListAsync();
        var selectedStatus = status?.Trim();
        if (!string.IsNullOrWhiteSpace(selectedStatus))
        {
            orders = orders.Where(x => string.Equals(
                x.TrangThaiDonHang?.Trim(), selectedStatus, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        ViewBag.Status = selectedStatus;
        ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
        return View(orders);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> NotificationSnapshot(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var latestOrderId = await _context.DonHangs
            .Where(x => x.NgayDat >= today && x.NgayDat < tomorrow)
            .MaxAsync(x => (int?)x.DonHangID, cancellationToken) ?? 0;
        return Json(new { latestOrderId });
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> NewOrders(int afterId, CancellationToken cancellationToken)
    {
        afterId = Math.Max(0, afterId);
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var orders = await _context.DonHangs.AsNoTracking()
            .Where(x => x.DonHangID > afterId && x.NgayDat >= today && x.NgayDat < tomorrow)
            .OrderBy(x => x.DonHangID)
            .Select(x => new
            {
                x.DonHangID,
                CustomerName = x.TenKhachDat ?? x.KhachHang!.HoTen,
                x.NgayDat,
                x.TongTien,
                Status = x.TrangThaiDonHang
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        return Json(orders.Select(x => new
        {
            orderId = x.DonHangID,
            customerName = x.CustomerName ?? "Khách vãng lai",
            time = x.NgayDat.ToString("HH:mm"),
            total = x.TongTien,
            status = x.Status,
            detailsUrl = Url.Action(nameof(Details), new { id = x.DonHangID })
        }));
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.DonHangs.Include(x => x.KhachHang)
            .Include(x => x.ChiTietDonHangs).ThenInclude(x => x.MonAn)
            .Include(x => x.ThanhToans).AsSplitQuery().FirstOrDefaultAsync(x => x.DonHangID == id);
        if (order == null) return NotFound();
        await SetInvoiceSettingsAsync();
        ViewBag.DeliveredAt = await _context.GiaoHangs.Where(x => x.DonHangID == id).Select(x => x.NgayGiaoHang).FirstOrDefaultAsync();
        var paymentId = order.ThanhToans.Select(x => (int?)x.ThanhToanID).FirstOrDefault();
        ViewBag.BankTransactions = paymentId.HasValue
            ? await _context.GiaoDichWebhookThanhToans.Where(x => x.ThanhToanID == paymentId.Value)
                .OrderByDescending(x => x.NhanLuc).ToListAsync()
            : new List<GiaoDichWebhookThanhToan>();
        return View(order);
    }

    [Authorize(Roles = "Admin,NhanVien")]
    public async Task<IActionResult> Print(int id)
    {
        var order = await _context.DonHangs
            .Include(x => x.KhachHang)
            .Include(x => x.ChiTietDonHangs).ThenInclude(x => x.MonAn)
            .Include(x => x.ThanhToans)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.DonHangID == id);
        if (order == null) return NotFound();
        await SetInvoiceSettingsAsync();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string status, string? returnDate)
    {
        var allowedStatuses = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chế biến", "Chờ giao hàng", "Đang giao", "Hoàn thành", "Đã hủy" };
        if (!allowedStatuses.Contains(status)) return BadRequest();

        var order = await _context.DonHangs.FindAsync(id);
        if (order == null) return NotFound();
        if (!OrderStatusFlow.CanTransition(order.TrangThaiDonHang, status)) return BadRequest();
        if (status == order.TrangThaiDonHang) return RedirectToAction(nameof(Index), new { date = returnDate });
        order.TrangThaiDonHang = status;
        order.NhanVienID ??= CurrentUserId();

        if (status is "Chờ giao hàng" or "Đang giao")
        {
            await EnsureDeliveryForOrder(order, status);
        }

        if (status == "Hoàn thành")
        {
            var delivery = await _context.GiaoHangs.FirstOrDefaultAsync(x => x.DonHangID == id);
            if (delivery == null)
            {
                _context.GiaoHangs.Add(new GiaoHang
                {
                    DonHangID = id,
                    TrangThaiGiaoHang = "Giao thành công",
                    NgayNhanDon = DateTime.Now,
                    NgayGiaoHang = DateTime.Now
                });
            }
            else
            {
                delivery.TrangThaiGiaoHang = "Giao thành công";
                delivery.NgayGiaoHang ??= DateTime.Now;
            }
            await EnsureInvoiceForOrder(order);

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

        if (status == "Đã hủy")
        {
            await RefundRedeemedPointsAsync(order);
            var delivery = await _context.GiaoHangs.FirstOrDefaultAsync(x => x.DonHangID == id);
            if (delivery != null)
            {
                delivery.TrangThaiGiaoHang = "Giao thất bại";
                delivery.NgayGiaoHang ??= DateTime.Now;
            }
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { date = returnDate });
    }

    private async Task EnsureDeliveryForOrder(DonHang order, string orderStatus)
    {
        var delivery = await _context.GiaoHangs.FirstOrDefaultAsync(x => x.DonHangID == order.DonHangID);
        if (delivery == null)
        {
            delivery = new GiaoHang
            {
                DonHangID = order.DonHangID,
                TrangThaiGiaoHang = orderStatus == "Đang giao" ? "Đang giao" : "Chờ nhận",
                NgayNhanDon = orderStatus == "Đang giao" ? DateTime.Now : null
            };
            _context.GiaoHangs.Add(delivery);
            return;
        }

        if (orderStatus == "Đang giao")
        {
            delivery.TrangThaiGiaoHang = "Đang giao";
            delivery.NgayNhanDon ??= DateTime.Now;
        }
        else if (delivery.TrangThaiGiaoHang == "Giao thất bại")
        {
            delivery.TrangThaiGiaoHang = "Chờ nhận";
            delivery.NgayGiaoHang = null;
        }
    }

    private async Task EnsureInvoiceForOrder(DonHang order)
    {
        if (!await _context.HoaDons.AnyAsync(x => x.DonHangID == order.DonHangID))
        {
            _context.HoaDons.Add(new HoaDon
            {
                DonHangID = order.DonHangID,
                TongTien = order.TongTien,
                NguoiLapID = CurrentUserId()
            });
        }
    }

    private async Task RefundRedeemedPointsAsync(DonHang order)
    {
        if (!order.KhachHangID.HasValue || order.DiemDaDung <= 0 || order.DiemDaHoanLai) return;
        var customer = await _context.KhachHangs.FirstOrDefaultAsync(x => x.KhachHangID == order.KhachHangID.Value);
        if (customer == null) return;
        customer.DiemTichLuy += order.DiemDaDung;
        order.DiemDaHoanLai = true;
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private async Task SetInvoiceSettingsAsync()
    {
        var settings = await _context.CauHinhGiaoHangs.FindAsync(1);
        var bankName = ConfigValue(settings?.TenNganHang, _configuration["Bank:Name"]);
        var bankBin = ConfigValue(settings?.NganHangBin, _configuration["Bank:Bin"])?.Trim();
        if (bankBin == "000000" && IsMbBank(bankName)) bankBin = "970422";
        if (bankBin == "000000") bankBin = null;
        ViewBag.BankName = bankName;
        ViewBag.BankAccount = ConfigValue(settings?.SoTaiKhoan, _configuration["Bank:AccountNumber"])?.Trim();
        ViewBag.BankOwner = ConfigValue(settings?.ChuTaiKhoan, _configuration["Bank:AccountName"])?.Trim();
        ViewBag.BankBin = bankBin;
        ViewBag.StoreName = ConfigValue(settings?.TenCuaHang, _configuration["Store:Name"]);
        ViewBag.StorePhone = ConfigValue(settings?.DienThoaiCuaHang, _configuration["Store:Phone"]);
        ViewBag.StoreEmail = ConfigValue(settings?.EmailCuaHang, _configuration["Store:Email"]);
        ViewBag.StoreAddress = ConfigValue(settings?.DiaChiCuaHang, _configuration["Delivery:StoreAddress"]);
    }

    private static string? ConfigValue(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static bool IsMbBank(string? bankName)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(bankName?.ToUpperInvariant() ?? "", "[^A-Z0-9]", "");
        return normalized == "MB" || normalized.StartsWith("MBBANK", StringComparison.Ordinal) ||
               normalized.Contains("QUANDOI", StringComparison.Ordinal) || normalized.Contains("MILITARYBANK", StringComparison.Ordinal);
    }
}
