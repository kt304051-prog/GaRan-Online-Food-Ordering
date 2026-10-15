using GaRan.Data;
using GaRan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly GaRanDbContext _context;
    public DashboardController(GaRanDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var now = DateTime.Now;
        var dayStart = now.Date;
        var nextDayStart = dayStart.AddDays(1);
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var nextMonthStart = monthStart.AddMonths(1);
        var chartStart = monthStart.AddMonths(-11);
        var completed = _context.DonHangs.Where(x => x.TrangThaiDonHang == "Hoàn thành");
        var monthlyRevenue = await completed
            .Where(x => x.NgayDat >= chartStart && x.NgayDat < nextMonthStart)
            .GroupBy(x => new { x.NgayDat.Year, x.NgayDat.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.TongTien) })
            .ToListAsync();
        var revenueByMonth = monthlyRevenue.ToDictionary(x => (x.Year, x.Month), x => x.Amount);
        var statuses = await _context.DonHangs
            .Where(x => x.NgayDat >= dayStart && x.NgayDat < nextDayStart)
            .GroupBy(x => x.TrangThaiDonHang)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var recentOrders = await _context.DonHangs.AsNoTracking()
            .OrderByDescending(x => x.NgayDat).Take(6)
            .Select(x => new DashboardOrderVM
            {
                DonHangID = x.DonHangID,
                TenKhach = x.TenKhachDat ?? x.KhachHang!.HoTen,
                TongTien = x.TongTien,
                TrangThai = x.TrangThaiDonHang,
                NgayDat = x.NgayDat
            }).ToListAsync();
        var categoryRevenue = await _context.ChiTietDonHangs.AsNoTracking()
            .Where(x => x.DonHang!.TrangThaiDonHang == "Hoàn thành")
            .GroupBy(x => x.MonAn!.DanhMuc!.TenDanhMuc)
            .Select(g => new RevenueVM { Label = g.Key, Amount = g.Sum(x => x.ThanhTien) })
            .OrderByDescending(x => x.Amount).Take(6).ToListAsync();
        var recentReviews = await _context.DanhGias.AsNoTracking()
            .OrderByDescending(x => x.NgayDanhGia).Take(4)
            .Select(x => new { x.NgayDanhGia, Food = x.MonAn == null ? "Đánh giá dịch vụ" : x.MonAn.TenMon, x.SoSao })
            .ToListAsync();
        var recentCustomers = await _context.KhachHangs.AsNoTracking()
            .OrderByDescending(x => x.NgayTao).Take(3)
            .Select(x => new { x.HoTen, x.NgayTao })
            .ToListAsync();
        var recentFoods = await _context.MonAns.AsNoTracking()
            .OrderByDescending(x => x.NgayTao).Take(2)
            .Select(x => new { x.TenMon, x.NgayTao })
            .ToListAsync();
        var activities = new List<DashboardActivityVM>();
        activities.AddRange(recentOrders.Take(3).Select(x => new DashboardActivityVM
        {
            BieuTuong = "🛍", TieuDe = $"Đơn hàng #{x.DonHangID}", MoTa = $"Khách hàng: {x.TenKhach}",
            ThoiGian = x.NgayDat, GiaTri = $"{x.TongTien:N0} đ"
        }));
        activities.AddRange(recentReviews.Select(x => new DashboardActivityVM
        {
            BieuTuong = "⭐", TieuDe = "Đánh giá mới", MoTa = x.Food,
            ThoiGian = x.NgayDanhGia, GiaTri = $"{x.SoSao} sao"
        }));
        activities.AddRange(recentFoods.Select(x => new DashboardActivityVM
        {
            BieuTuong = "🍗", TieuDe = "Món ăn mới", MoTa = x.TenMon, ThoiGian = x.NgayTao
        }));
        activities.AddRange(recentCustomers.Select(x => new DashboardActivityVM
        {
            BieuTuong = "♙", TieuDe = "Khách hàng mới", MoTa = x.HoTen, ThoiGian = x.NgayTao
        }));
        var activityFeed = activities.OrderByDescending(x => x.ThoiGian).Take(7).ToList();
        var statusOrder = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang chế biến", "Chờ giao hàng", "Đang giao", "Hoàn thành", "Đã hủy" };
        var completedThisMonth = await completed.CountAsync(x => x.NgayDat >= monthStart && x.NgayDat < nextMonthStart);
        var revenueThisMonth = await completed.Where(x => x.NgayDat >= monthStart && x.NgayDat < nextMonthStart)
            .SumAsync(x => (decimal?)x.TongTien) ?? 0;
        var vm = new DashboardVM
        {
            TongMonAn = await _context.MonAns.CountAsync(x => x.TrangThai),
            TongDonHang = await _context.DonHangs.CountAsync(x => x.NgayDat >= dayStart && x.NgayDat < nextDayStart),
            TongKhachHang = await _context.KhachHangs.CountAsync(),
            TongDoanhThu = await completed.SumAsync(x => (decimal?)x.TongTien) ?? 0,
            DoanhThuThangNay = revenueThisMonth,
            DoanhThuThangTruoc = revenueByMonth.GetValueOrDefault((previousMonthStart.Year, previousMonthStart.Month)),
            DonHangThangNay = await _context.DonHangs.CountAsync(x => x.NgayDat >= monthStart && x.NgayDat < nextMonthStart),
            DonHangChoXuLy = await _context.DonHangs.CountAsync(x => x.NgayDat >= dayStart && x.NgayDat < nextDayStart && x.TrangThaiDonHang == "Chờ xác nhận"),
            GiaTriDonTrungBinhThangNay = completedThisMonth == 0 ? 0 : revenueThisMonth / completedThisMonth,
            DoanhThuTheoThang = Enumerable.Range(0, 12).Select(offset =>
            {
                var month = chartStart.AddMonths(offset);
                var amount = revenueByMonth.GetValueOrDefault((month.Year, month.Month));
                return new RevenueVM { Label = $"T{month.Month:00}/{month.Year}", Amount = amount };
            }).ToList(),
            TopMonBanChay = await _context.ChiTietDonHangs.Where(x => x.DonHang!.TrangThaiDonHang == "Hoàn thành").Include(x => x.MonAn)
                .GroupBy(x => x.MonAn!.TenMon)
                .Select(g => new RevenueVM { Label = g.Key, Amount = g.Sum(x => x.SoLuong) })
                .OrderByDescending(x => x.Amount).Take(5).ToListAsync(),
            DonHangGanDay = recentOrders,
            HoatDongGanDay = activityFeed,
            DoanhThuTheoDanhMuc = categoryRevenue,
            TongDanhMuc = await _context.DanhMucs.CountAsync(x => x.TrangThai),
            TongMonDangBan = await _context.MonAns.CountAsync(x => x.TrangThai),
            TongDanhGiaChoDuyet = await _context.DanhGias.CountAsync(x => !x.TrangThai),
            KhachHangMoiThang = await _context.KhachHangs.CountAsync(x => x.NgayTao >= monthStart && x.NgayTao < nextMonthStart),
            DonHangTheoTrangThai = statusOrder.Select(label => new DashboardStatusVM
            {
                Label = label,
                Count = statuses.FirstOrDefault(x => x.Status == label)?.Count ?? 0
            }).ToList()
        };
        return View(vm);
    }
}
