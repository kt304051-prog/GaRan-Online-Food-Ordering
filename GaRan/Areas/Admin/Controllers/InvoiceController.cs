using GaRan.Data;
using GaRan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin,NhanVien")]
public class InvoiceController : Controller
{
    private readonly GaRanDbContext _context;
    public InvoiceController(GaRanDbContext context) => _context = context;

    public async Task<IActionResult> Index(DateTime? date)
    {
        var selectedDate = (date ?? DateTime.Today).Date;
        var nextDate = selectedDate.AddDays(1);
        ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
        var invoices = await _context.HoaDons
            .Where(x => x.NgayLap >= selectedDate && x.NgayLap < nextDate)
            .Include(x => x.DonHang).ThenInclude(x => x!.KhachHang)
            .Include(x => x.NguoiLap)
            .OrderByDescending(x => x.NgayLap)
            .ToListAsync();
        return View(invoices);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromOrder(int orderId)
    {
        var order = await _context.DonHangs.FindAsync(orderId);
        if (order == null) return NotFound();
        if (order.TrangThaiDonHang != "Hoàn thành")
        {
            TempData["Message"] = "Chỉ có thể lập hóa đơn cho đơn hàng đã hoàn thành.";
            return RedirectToAction(nameof(Index));
        }
        if (!await _context.HoaDons.AnyAsync(x => x.DonHangID == orderId))
        {
            _context.HoaDons.Add(new HoaDon { DonHangID = orderId, TongTien = order.TongTien, NguoiLapID = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value) });
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.HoaDons.FindAsync(id);
        if (item != null) { _context.HoaDons.Remove(item); await _context.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}
