using GaRan.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class ReviewAdminController : Controller
{
    private readonly GaRanDbContext _context;
    public ReviewAdminController(GaRanDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? email)
    {
        email = email?.Trim();
        var query = _context.DanhGias.Include(x => x.KhachHang).Include(x => x.MonAn).Include(x => x.DonHang).AsQueryable();
        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(x => x.KhachHang != null && x.KhachHang.Email.Contains(email));
        ViewBag.SearchEmail = email;
        var reviews = await query
            .OrderByDescending(x => x.NgayDanhGia).ToListAsync();
        return View(reviews);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var review = await _context.DanhGias.FindAsync(id);
        if (review != null)
        {
            review.TrangThai = true;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var review = await _context.DanhGias.FindAsync(id);
        if (review != null)
        {
            _context.DanhGias.Remove(review);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
