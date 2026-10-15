using GaRan.Data;
using GaRan.Models;
using GaRan.Services;
using GaRan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private readonly GaRanDbContext _context;

    public UserController(GaRanDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var users = await _context.NguoiDungs
            .Include(x => x.VaiTro)
            .Where(x => x.VaiTro!.TenVaiTro == "Admin" || x.VaiTro.TenVaiTro == "NhanVien")
            .OrderBy(x => x.HoTen)
            .ToListAsync();
        return View(users);
    }

    public async Task<IActionResult> Customers()
    {
        var rows = await _context.KhachHangs
            .Select(customer => new
            {
                Customer = customer,
                CompletedOrders = customer.DonHangs.Count(order => order.TrangThaiDonHang == "Hoàn thành"),
                Spending = customer.DonHangs.Where(order => order.TrangThaiDonHang == "Hoàn thành")
                    .Select(order => (decimal?)order.TongTien).Sum() ?? 0m,
                LifetimePoints = customer.DonHangs.Where(order => order.TrangThaiDonHang == "Hoàn thành")
                    .Select(order => (int?)order.DiemDaCong).Sum() ?? 0
            })
            .OrderByDescending(x => x.Spending)
            .ToListAsync();
        var customers = rows.Select(row => new KhachHangTiemNangVM
        {
            KhachHang = row.Customer,
            SoDonHoanThanh = row.CompletedOrders,
            TongChiTieu = row.Spending,
            DiemTichLuyTronDoi = row.LifetimePoints,
            LaThanhVienVip = LoyaltyProgram.IsVip(row.LifetimePoints)
        }).ToList();
        return View(customers);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCustomerStatus(int id)
    {
        var customer = await _context.KhachHangs.FindAsync(id);
        if (customer == null) return NotFound();
        customer.TrangThai = !customer.TrangThai;
        await _context.SaveChangesAsync();
        TempData["Message"] = customer.TrangThai
            ? $"Đã mở khóa tài khoản {customer.Email}."
            : $"Đã khóa tài khoản {customer.Email}.";
        return RedirectToAction(nameof(Customers));
    }

    public async Task<IActionResult> Create()
    {
        await LoadRoles();
        return View("Edit", new NguoiDung { TrangThai = true, NgayTao = DateTime.Now });
    }

    public async Task<IActionResult> Edit(int id)
    {
        await LoadRoles();
        var user = await _context.NguoiDungs.FindAsync(id);
        return user == null ? NotFound() : View(user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(NguoiDung model, string? password)
    {
        ModelState.Remove(nameof(model.MatKhauHash));

        if (model.NguoiDungID == 0 && string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("password", "Vui lòng nhập mật khẩu.");
        }

        if (!ModelState.IsValid)
        {
            await LoadRoles();
            return View("Edit", model);
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            model.MatKhauHash = PasswordHasher.Hash(password);
        }

        if (model.NguoiDungID == 0)
        {
            model.NgayTao = DateTime.Now;
            _context.NguoiDungs.Add(model);
        }
        else
        {
            var current = await _context.NguoiDungs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.NguoiDungID == model.NguoiDungID);
            if (current == null) return NotFound();
            if (string.IsNullOrWhiteSpace(password)) model.MatKhauHash = current.MatKhauHash;
            model.NgayTao = current.NgayTao;
            _context.NguoiDungs.Update(model);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.NguoiDungs.FindAsync(id);
        if (item != null)
        {
            item.TrangThai = false;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadRoles()
    {
        ViewBag.VaiTroID = new SelectList(await _context.VaiTros
            .Where(x => x.TenVaiTro == "Admin" || x.TenVaiTro == "NhanVien")
            .ToListAsync(), "VaiTroID", "TenVaiTro");
    }
}
