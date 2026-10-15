using GaRan.Data;
using GaRan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class CategoryController : Controller
{
    private readonly GaRanDbContext _context;
    public CategoryController(GaRanDbContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.DanhMucs.Include(x => x.MonAns).OrderBy(x => x.TenDanhMuc).ToListAsync());
    public IActionResult Create() { return View("Edit", new DanhMuc { TrangThai = true }); }
    public async Task<IActionResult> Edit(int id) => View(await _context.DanhMucs.FindAsync(id) ?? new DanhMuc());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DanhMuc model)
    {
        if (!ModelState.IsValid) return View("Edit", model);
        if (model.DanhMucID == 0) _context.DanhMucs.Add(model); else _context.DanhMucs.Update(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.DanhMucs.FindAsync(id);
        if (item != null)
        {
            var hasFoods = await _context.MonAns.AnyAsync(x => x.DanhMucID == id);
            if (hasFoods)
            {
                item.TrangThai = false;
            }
            else
            {
                _context.DanhMucs.Remove(item);
            }
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
