using GaRan.Data;
using GaRan.Models;
using GaRan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Controllers;

[Authorize(Roles = "KhachHang")]
public class ReviewController : Controller
{
    private readonly GaRanDbContext _context;
    public ReviewController(GaRanDbContext context) => _context = context;

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? type = "all", string? sort = "newest", int page = 1)
    {
        const int pageSize = 8;
        type = type is "food" or "service" ? type : "all";
        sort = sort == "rating" ? "rating" : "newest";
        page = Math.Max(1, page);

        var approved = _context.DanhGias.AsNoTracking().Where(x => x.TrangThai);
        var count = await approved.CountAsync();
        var average = count == 0 ? 0 : await approved.AverageAsync(x => (double)x.SoSao);
        var ratingCounts = await approved.GroupBy(x => x.SoSao)
            .Select(group => new { Rating = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Rating, x => x.Count);

        var query = approved.Include(x => x.KhachHang).Include(x => x.MonAn).AsQueryable();
        if (type == "food") query = query.Where(x => x.LoaiDanhGia == "MonAn");
        if (type == "service") query = query.Where(x => x.LoaiDanhGia == "DichVu");

        var filteredCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        query = sort == "rating"
            ? query.OrderByDescending(x => x.SoSao).ThenByDescending(x => x.NgayDanhGia)
            : query.OrderByDescending(x => x.NgayDanhGia);

        var reviews = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var foodRatings = await approved.Where(x => x.LoaiDanhGia == "MonAn" && x.MonAnID.HasValue)
            .GroupBy(x => x.MonAnID!.Value)
            .Select(group => new { FoodId = group.Key, Average = group.Average(x => x.SoSao), Count = group.Count() })
            .OrderByDescending(x => x.Average).ThenByDescending(x => x.Count)
            .Take(4).ToListAsync();
        var foodIds = foodRatings.Select(x => x.FoodId).ToList();
        var foodDetails = await _context.MonAns.AsNoTracking().Where(x => foodIds.Contains(x.MonAnID))
            .ToDictionaryAsync(x => x.MonAnID, x => new { x.TenMon, x.HinhAnh });
        var popularFoods = foodRatings.Where(x => foodDetails.ContainsKey(x.FoodId)).Select(x => new ReviewFoodSummaryVM
        {
            FoodId = x.FoodId,
            Name = foodDetails[x.FoodId].TenMon,
            Image = foodDetails[x.FoodId].HinhAnh,
            Average = x.Average,
            Count = x.Count
        }).ToList();

        return View(new ReviewIndexVM
        {
            Reviews = reviews,
            PopularFoods = popularFoods,
            Average = average,
            Count = count,
            RatingCounts = ratingCounts,
            Type = type,
            Sort = sort,
            Page = page,
            TotalPages = totalPages
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int monAnId, int? donHangId, int soSao, string? noiDung)
    {
        var customerId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        if (!donHangId.HasValue)
        {
            donHangId = await _context.ChiTietDonHangs
                .Where(x => x.MonAnID == monAnId &&
                            x.DonHang!.KhachHangID == customerId &&
                            x.DonHang.TrangThaiDonHang == "Hoàn thành")
                .OrderByDescending(x => x.DonHang!.NgayDat)
                .Select(x => (int?)x.DonHangID)
                .FirstOrDefaultAsync();
        }

        if (!donHangId.HasValue)
        {
            TempData["Message"] = "Bạn cần mua món này trước khi đánh giá.";
            return RedirectToAction("Details", "Product", new { id = monAnId });
        }

        var bought = await _context.ChiTietDonHangs.AnyAsync(x =>
            x.DonHangID == donHangId &&
            x.MonAnID == monAnId &&
            x.DonHang!.KhachHangID == customerId &&
            x.DonHang.TrangThaiDonHang == "Hoàn thành");
        if (!bought)
        {
            TempData["Message"] = "Bạn chỉ có thể đánh giá món đã mua sau khi đơn hàng hoàn thành.";
            return RedirectToAction("Details", "Product", new { id = monAnId });
        }

        var alreadyReviewed = await _context.DanhGias.AnyAsync(x =>
            x.DonHangID == donHangId && x.MonAnID == monAnId && x.KhachHangID == customerId && x.LoaiDanhGia == "MonAn");
        if (alreadyReviewed)
        {
            TempData["Message"] = "Bạn đã đánh giá món này trong đơn hàng đó rồi.";
            return RedirectToAction("Details", "Product", new { id = monAnId });
        }

        _context.DanhGias.Add(new DanhGia
        {
            KhachHangID = customerId,
            MonAnID = monAnId,
            DonHangID = donHangId.Value,
            LoaiDanhGia = "MonAn",
            SoSao = Math.Clamp(soSao, 1, 5),
            NoiDung = noiDung,
            TrangThai = false
        });
        await _context.SaveChangesAsync();
        TempData["Message"] = "Đánh giá đã gửi và đang chờ duyệt.";
        return RedirectToAction("Details", "Product", new { id = monAnId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateService(int donHangId, int soSao, string? noiDung)
    {
        var customerId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var order = await _context.DonHangs.FirstOrDefaultAsync(x =>
            x.DonHangID == donHangId && x.KhachHangID == customerId && x.TrangThaiDonHang == "Hoàn thành");
        if (order == null)
        {
            TempData["Message"] = "Chỉ có thể đánh giá dịch vụ sau khi đơn hàng của bạn hoàn thành.";
            return RedirectToAction("Details", "Order", new { id = donHangId });
        }

        var alreadyReviewed = await _context.DanhGias.AnyAsync(x =>
            x.DonHangID == donHangId && x.KhachHangID == customerId && x.LoaiDanhGia == "DichVu");
        if (alreadyReviewed)
        {
            TempData["Message"] = "Bạn đã gửi đánh giá dịch vụ cho đơn hàng này rồi.";
            return RedirectToAction("Details", "Order", new { id = donHangId });
        }

        _context.DanhGias.Add(new DanhGia
        {
            KhachHangID = customerId,
            DonHangID = donHangId,
            MonAnID = null,
            LoaiDanhGia = "DichVu",
            SoSao = Math.Clamp(soSao, 1, 5),
            NoiDung = string.IsNullOrWhiteSpace(noiDung) ? null : noiDung.Trim(),
            TrangThai = false
        });
        await _context.SaveChangesAsync();
        TempData["Message"] = "Đánh giá dịch vụ đã gửi và đang chờ cửa hàng duyệt.";
        return RedirectToAction("Details", "Order", new { id = donHangId });
    }
}
