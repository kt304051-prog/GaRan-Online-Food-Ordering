using System.Diagnostics;
using GaRan.Data;
using GaRan.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace GaRan.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly GaRanDbContext _context;

        public HomeController(ILogger<HomeController> logger, GaRanDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Categories = await _context.DanhMucs.Where(x => x.TrangThai).ToListAsync();
            var highlightedIds = await _context.MonAns
                .Where(x => x.TrangThai && x.NoiBat)
                .OrderByDescending(x => x.NgayTao)
                .Take(12)
                .Select(x => x.MonAnID)
                .ToListAsync();

            if (highlightedIds.Count == 0)
            {
                highlightedIds = await _context.MonAns
                    .Where(x => x.TrangThai)
                    .OrderByDescending(x => x.NgayTao)
                    .Take(12)
                    .Select(x => x.MonAnID)
                    .ToListAsync();
            }

            ViewBag.HighlightedFoodIds = highlightedIds;
            ViewBag.Featured = await _context.MonAns.Include(x => x.DanhMuc).Where(x => highlightedIds.Contains(x.MonAnID)).OrderByDescending(x => x.NgayTao).ToListAsync();
            ViewBag.Combos = await _context.MonAns.Include(x => x.DanhMuc).Where(x => x.TrangThai && x.DanhMuc!.TenDanhMuc.Contains("Combo")).Take(4).ToListAsync();
            var ratingIds = highlightedIds
                .Concat(await _context.MonAns.Where(x => x.TrangThai && x.DanhMuc!.TenDanhMuc.Contains("Combo")).Select(x => x.MonAnID).Take(5).ToListAsync())
                .Distinct().ToList();
            ViewBag.ProductRatings = await _context.DanhGias.Where(x => x.TrangThai && x.LoaiDanhGia == "MonAn" && x.MonAnID.HasValue && ratingIds.Contains(x.MonAnID.Value))
                .GroupBy(x => x.MonAnID!.Value)
                .Select(group => new { FoodId = group.Key, Average = group.Average(x => x.SoSao), Count = group.Count() })
                .ToDictionaryAsync(x => x.FoodId, x => new GaRan.ViewModels.FoodRatingSummaryVM { Average = x.Average, Count = x.Count });
            ViewBag.Reviews = await _context.DanhGias.Include(x => x.KhachHang).Include(x => x.MonAn).Where(x => x.TrangThai).OrderByDescending(x => x.NgayDanhGia).Take(6).ToListAsync();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
