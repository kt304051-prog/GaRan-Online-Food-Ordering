using GaRan.Data;
using GaRan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin,NhanVien")]
public class DeliveryController : Controller
{
    private readonly GaRanDbContext _context;
    private readonly IConfiguration _configuration;
    public DeliveryController(GaRanDbContext context, IConfiguration configuration) { _context = context; _configuration = configuration; }

    public async Task<IActionResult> Index(DateTime? date, string? status)
    {
        var selectedDate = (date ?? DateTime.Today).Date;
        var nextDate = selectedDate.AddDays(1);
        var query = _context.GiaoHangs
            .Include(x => x.DonHang).ThenInclude(x => x!.KhachHang)
            .Where(x => x.DonHang != null && x.DonHang.NgayDat >= selectedDate && x.DonHang.NgayDat < nextDate);
        var deliveries = await query.OrderByDescending(x => x.DonHang!.NgayDat).ToListAsync();
        var selectedStatus = status?.Trim();
        if (!string.IsNullOrWhiteSpace(selectedStatus))
        {
            deliveries = deliveries.Where(x => string.Equals(
                x.TrangThaiGiaoHang?.Trim(), selectedStatus, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
        ViewBag.Status = selectedStatus;
        return View(deliveries);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Settings()
    {
        var settings = await _context.CauHinhGiaoHangs.FindAsync(1);
        if (settings == null) return Problem("Chưa khởi tạo cấu hình giao hàng.");
        settings.TenCuaHang ??= _configuration["Store:Name"];
        settings.DiaChiCuaHang = string.IsNullOrWhiteSpace(settings.DiaChiCuaHang)
            ? _configuration["Delivery:StoreAddress"] ?? string.Empty
            : settings.DiaChiCuaHang;
        settings.DienThoaiCuaHang ??= _configuration["Store:Phone"];
        settings.EmailCuaHang ??= _configuration["Store:Email"];
        settings.NganHangBin ??= _configuration["Bank:Bin"];
        settings.TenNganHang ??= _configuration["Bank:Name"];
        settings.SoTaiKhoan ??= _configuration["Bank:AccountNumber"];
        settings.ChuTaiKhoan ??= _configuration["Bank:AccountName"];
        settings.TienToNoiDung = string.IsNullOrWhiteSpace(settings.TienToNoiDung) ? "WOCORDER" : settings.TienToNoiDung;
        if (IsMbBank(settings.TenNganHang) && settings.NganHangBin == "000000")
        {
            settings.NganHangBin = "970422";
            await _context.SaveChangesAsync();
        }
        return View(settings);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveSettings(CauHinhGiaoHang model)
    {
        if (IsMbBank(model.TenNganHang) && model.NganHangBin == "000000") model.NganHangBin = "970422";
        model.DiaChiCuaHang = model.DiaChiCuaHang?.Trim();
        model.TienToNoiDung = string.IsNullOrWhiteSpace(model.TienToNoiDung) ? "WOCORDER" : model.TienToNoiDung.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(model.DiaChiCuaHang))
            ModelState.AddModelError(nameof(model.DiaChiCuaHang), "Vui lòng nhập địa chỉ cửa hàng để ước tính khoảng cách giao hàng.");
        if (!model.BatCOD && !model.BatVietQR)
            ModelState.AddModelError(string.Empty, "Cần bật ít nhất một phương thức thanh toán.");
        if (model.MienPhiDenKm < 0)
            ModelState.AddModelError(nameof(model.MienPhiDenKm), "Ngưỡng miễn phí không được âm.");
        if (model.ToiDaKm <= 0 || model.ToiDaKm > 100)
            ModelState.AddModelError(nameof(model.ToiDaKm), "Phạm vi tối đa phải lớn hơn 0 và không vượt quá 100 km.");
        if (model.ToiDaKm > 0 && model.MienPhiDenKm >= model.ToiDaKm)
            ModelState.AddModelError(nameof(model.MienPhiDenKm), "Ngưỡng miễn phí phải nhỏ hơn phạm vi giao hàng tối đa.");
        if (model.UocTinhCungPhuongKm <= 0 || model.UocTinhCungPhuongKm > 100)
            ModelState.AddModelError(nameof(model.UocTinhCungPhuongKm), "Khoảng cách ước tính cùng phường phải từ 0,1 đến 100 km.");
        if (model.UocTinhKhacPhuongKm <= 0 || model.UocTinhKhacPhuongKm > 100)
            ModelState.AddModelError(nameof(model.UocTinhKhacPhuongKm), "Khoảng cách ước tính khác phường phải từ 0,1 đến 100 km.");
        if (model.Phi3Den4Km < 0 || model.Phi4Den5Km < 0)
            ModelState.AddModelError("", "Phí giao hàng không được âm.");
        if (model.TyLeThue < 0 || model.TyLeThue > 1)
            ModelState.AddModelError(nameof(model.TyLeThue), "Thuế suất nhập theo tỷ lệ từ 0 đến 1, ví dụ 0,10 là 10%.");
        if (!string.IsNullOrWhiteSpace(model.NganHangBin) && !System.Text.RegularExpressions.Regex.IsMatch(model.NganHangBin, "^\\d{6}$"))
            ModelState.AddModelError(nameof(model.NganHangBin), "Mã BIN ngân hàng cần có 6 chữ số.");
        if (model.NganHangBin == "000000")
            ModelState.AddModelError(nameof(model.NganHangBin), "000000 không phải mã BIN hợp lệ. MBBank dùng mã BIN 970422.");
        if (model.BatVietQR)
        {
            if (string.IsNullOrWhiteSpace(model.NganHangBin))
                ModelState.AddModelError(nameof(model.NganHangBin), "Chọn ngân hàng để tạo VietQR.");
            if (string.IsNullOrWhiteSpace(model.SoTaiKhoan))
                ModelState.AddModelError(nameof(model.SoTaiKhoan), "Nhập số tài khoản nhận tiền để tạo VietQR.");
            if (string.IsNullOrWhiteSpace(model.ChuTaiKhoan))
                ModelState.AddModelError(nameof(model.ChuTaiKhoan), "Nhập tên chủ tài khoản để tạo VietQR.");
        }
        if (!ModelState.IsValid) return View("Settings", model);

        model.CauHinhGiaoHangID = 1;
        _context.CauHinhGiaoHangs.Update(model);
        await _context.SaveChangesAsync();
        TempData["Message"] = "Đã lưu phạm vi, phí giao hàng, thông tin cửa hàng và thanh toán.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,NhanVien")]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? returnDate, string? returnStatus)
    {
        var allowedStatuses = new[] { "Chờ nhận", "Đang giao", "Giao thành công", "Giao thất bại" };
        if (!allowedStatuses.Contains(status)) return BadRequest();

        var item = await _context.GiaoHangs.Include(x => x.DonHang).FirstOrDefaultAsync(x => x.GiaoHangID == id);
        if (item == null) return NotFound();
        if ((item.DonHang?.TrangThaiDonHang is "Hoàn thành" or "Đã hủy") && status != item.TrangThaiGiaoHang) return BadRequest();
        var validTransition = item.TrangThaiGiaoHang switch
        {
            "Chờ nhận" => status is "Chờ nhận" or "Đang giao" or "Giao thất bại",
            "Đang giao" => status is "Đang giao" or "Giao thành công" or "Giao thất bại",
            "Giao thành công" => status == "Giao thành công",
            "Giao thất bại" => status == "Giao thất bại",
            _ => false
        };
        if (!validTransition) return BadRequest();
        item.TrangThaiGiaoHang = status;
        if (status == "Đang giao") item.NgayNhanDon ??= DateTime.Now;
        if (status is "Giao thành công" or "Giao thất bại") item.NgayGiaoHang ??= DateTime.Now;
        if (item.DonHang != null)
        {
            if (status == "Đang giao") item.DonHang.TrangThaiDonHang = "Đang giao";
            if (status == "Giao thành công")
            {
                item.DonHang.TrangThaiDonHang = "Hoàn thành";
                await EnsureInvoiceForOrder(item.DonHang);
                await AwardLoyaltyPointsAsync(item.DonHang);
            }
            if (status == "Giao thất bại")
            {
                item.DonHang.TrangThaiDonHang = "Đã hủy";
                await RefundRedeemedPointsAsync(item.DonHang);
            }
        }
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { date = returnDate, status = returnStatus });
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

    private async Task AwardLoyaltyPointsAsync(DonHang order)
    {
        if (!order.KhachHangID.HasValue || order.DiemDaCongThuong) return;
        var customer = await _context.KhachHangs.FirstOrDefaultAsync(x => x.KhachHangID == order.KhachHangID.Value);
        if (customer == null) return;
        order.DiemDaCong = (int)Math.Floor(Math.Max(0, order.TienHang) / 10000m);
        customer.DiemTichLuy += order.DiemDaCong;
        order.DiemDaCongThuong = true;
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
    private static bool IsMbBank(string? bankName)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(bankName?.ToUpperInvariant() ?? "", "[^A-Z0-9]", "");
        return normalized == "MB" || normalized.StartsWith("MBBANK", StringComparison.Ordinal) ||
               normalized.Contains("QUANDOI", StringComparison.Ordinal) || normalized.Contains("MILITARYBANK", StringComparison.Ordinal);
    }
}
