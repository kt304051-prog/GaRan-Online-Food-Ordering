using GaRan.Data;
using GaRan.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Controllers;

public class ProductController : Controller
{
    private readonly GaRanDbContext _context;
    public ProductController(GaRanDbContext context) => _context = context;

    public async Task<IActionResult> Index(int? categoryId, string? q, int page = 1)
    {
        const int pageSize = 8;
        q = q?.Trim();
        page = Math.Max(1, page);
        var query = _context.MonAns.Include(x => x.DanhMuc).Where(x => x.TrangThai);
        if (categoryId.HasValue) query = query.Where(x => x.DanhMucID == categoryId);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.TenMon.Contains(q) || (x.MoTa != null && x.MoTa.Contains(q)) || (x.DanhMuc != null && x.DanhMuc.TenDanhMuc.Contains(q)));

        ViewBag.Categories = await _context.DanhMucs.Where(x => x.TrangThai).ToListAsync();
        ViewBag.HighlightedFoodIds = await _context.MonAns
            .Where(x => x.TrangThai && x.NoiBat)
            .OrderByDescending(x => x.NgayTao)
            .Select(x => x.MonAnID)
            .ToListAsync();
        ViewBag.Query = q;
        ViewBag.CategoryId = categoryId;
        var totalPages = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / (double)pageSize));
        page = Math.Min(page, totalPages);
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        var items = await query.OrderBy(x => x.TenMon).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var itemIds = items.Select(x => x.MonAnID).ToList();
        ViewBag.ProductRatings = await _context.DanhGias.Where(x => x.TrangThai && x.LoaiDanhGia == "MonAn" && x.MonAnID.HasValue && itemIds.Contains(x.MonAnID.Value))
            .GroupBy(x => x.MonAnID!.Value)
            .Select(group => new { FoodId = group.Key, Average = group.Average(x => x.SoSao), Count = group.Count() })
            .ToDictionaryAsync(x => x.FoodId, x => new FoodRatingSummaryVM { Average = x.Average, Count = x.Count });
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await _context.MonAns.Include(x => x.DanhMuc).FirstOrDefaultAsync(x => x.MonAnID == id && x.TrangThai);
        if (item == null) return NotFound();
        ViewBag.Reviews = await _context.DanhGias.Include(x => x.KhachHang)
            .Where(x => x.LoaiDanhGia == "MonAn" && x.MonAnID == id && x.TrangThai).OrderByDescending(x => x.NgayDanhGia).ToListAsync();
        return View(item);
    }
}
