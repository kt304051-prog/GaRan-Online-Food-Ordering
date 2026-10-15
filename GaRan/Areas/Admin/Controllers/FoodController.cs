using GaRan.Data;
using GaRan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class FoodController : Controller
{
    private readonly GaRanDbContext _context;
    private readonly IWebHostEnvironment _environment;
    public FoodController(GaRanDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);
        q = q?.Trim();
        var query = _context.MonAns.Include(x => x.DanhMuc).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.TenMon.Contains(q));
        var totalPages = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / (double)pageSize));
        page = Math.Min(page, totalPages);
        ViewBag.Query = q;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        return View(await query.OrderBy(x => x.TenMon).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

    public async Task<IActionResult> Create() { await LoadCategories(); return View("Edit", new MonAn { TrangThai = true }); }
    public async Task<IActionResult> Edit(int id) { await LoadCategories(); return View(await _context.MonAns.FindAsync(id) ?? new MonAn()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(MonAn model, IFormFile? imageFile)
    {
        MonAn? existing = null;
        if (model.MonAnID != 0)
        {
            existing = await _context.MonAns.FindAsync(model.MonAnID);
            if (existing == null) return NotFound();
            if (imageFile == null || imageFile.Length == 0) model.HinhAnh = existing.HinhAnh;
        }
        if (imageFile is { Length: > 0 })
        {
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (imageFile.Length > 5 * 1024 * 1024 || extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                ModelState.AddModelError(string.Empty, "Ảnh chỉ nhận JPG, PNG hoặc WEBP và không vượt quá 5 MB.");
        }
        if (!ModelState.IsValid) { await LoadCategories(); return View("Edit", model); }
        if (imageFile is { Length: > 0 })
        {
            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(imageFile.FileName)}";
            var folder = Path.Combine(_environment.WebRootPath, "images", "foods");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(path);
            await imageFile.CopyToAsync(stream);
            model.HinhAnh = $"/images/foods/{fileName}";
        }
        if (model.MonAnID == 0) _context.MonAns.Add(model);
        else _context.Entry(existing!).CurrentValues.SetValues(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.MonAns.FindAsync(id);
        if (item != null)
        {
            var hasOrders = await _context.ChiTietDonHangs.AnyAsync(x => x.MonAnID == id);
            if (hasOrders)
            {
                item.TrangThai = false;
            }
            else
            {
                _context.MonAns.Remove(item);
            }
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCategories()
    {
        ViewBag.DanhMucID = new SelectList(await _context.DanhMucs.Where(x => x.TrangThai).ToListAsync(), "DanhMucID", "TenDanhMuc");
    }
}
