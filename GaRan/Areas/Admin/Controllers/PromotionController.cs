using GaRan.Data;
using GaRan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class PromotionController : Controller
{
    private readonly GaRanDbContext _context;
    public PromotionController(GaRanDbContext context) => _context = context;
    public async Task<IActionResult> Index() => View(await _context.KhuyenMais.OrderByDescending(x => x.NgayBatDau).ToListAsync());
    public IActionResult Create() => View("Edit", new KhuyenMai { TrangThai = true, NgayBatDau = DateTime.Today, NgayKetThuc = DateTime.Today.AddDays(30) });
    public async Task<IActionResult> Edit(int id) => View(await _context.KhuyenMais.FindAsync(id) ?? new KhuyenMai());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(KhuyenMai model)
    {
        model.MaKhuyenMai = (model.MaKhuyenMai ?? string.Empty).Trim().ToUpperInvariant();
        model.TenKhuyenMai = (model.TenKhuyenMai ?? string.Empty).Trim();
        if (await _context.KhuyenMais.AnyAsync(x => x.KhuyenMaiID != model.KhuyenMaiID && x.MaKhuyenMai == model.MaKhuyenMai))
            ModelState.AddModelError(nameof(model.MaKhuyenMai), "Mã khuyến mãi này đã tồn tại.");
        if (model.NgayKetThuc < model.NgayBatDau)
        {
            ModelState.AddModelError(nameof(model.NgayKetThuc), "Ngày kết thúc phải sau ngày bắt đầu.");
        }
        if (!ModelState.IsValid) return View("Edit", model);
        if (model.KhuyenMaiID == 0) _context.KhuyenMais.Add(model); else _context.KhuyenMais.Update(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.KhuyenMais.FindAsync(id);
        if (item != null)
        {
            var used = await _context.DonHangs.AnyAsync(x => x.KhuyenMaiID == id);
            if (used)
            {
                item.TrangThai = false;
            }
            else
            {
                _context.KhuyenMais.Remove(item);
            }
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
