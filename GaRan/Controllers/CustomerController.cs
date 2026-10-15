using GaRan.Data;
using GaRan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GaRan.Controllers;

[Authorize(Roles = "KhachHang")]
public class CustomerController : Controller
{
    private readonly GaRanDbContext _context;
    public CustomerController(GaRanDbContext context) => _context = context;

    public async Task<IActionResult> Profile()
    {
        var customer = await CurrentCustomer();
        if (customer == null) return NotFound();
        var lifetimePoints = await _context.DonHangs
            .Where(x => x.KhachHangID == customer.KhachHangID && x.TrangThaiDonHang == "Hoàn thành")
            .SumAsync(x => (int?)x.DiemDaCong) ?? 0;
        ViewBag.LifetimePoints = lifetimePoints;
        ViewBag.IsVip = LoyaltyProgram.IsVip(lifetimePoints);
        ViewBag.PointsToVip = LoyaltyProgram.PointsToVip(lifetimePoints);
        ViewBag.AddressNeedsUpdate = !DeliveryAddressValidator.IsSpecific(customer.DiaChi);
        return View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string hoTen, string soDienThoai, string diaChi)
    {
        var customer = await CurrentCustomer();
        if (customer == null) return NotFound();

        if (string.IsNullOrWhiteSpace(hoTen) || string.IsNullOrWhiteSpace(soDienThoai) || string.IsNullOrWhiteSpace(diaChi))
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ họ tên, số điện thoại và địa chỉ.";
            return RedirectToAction(nameof(Profile));
        }

        if (hoTen.Trim().Length > 120 || soDienThoai.Trim().Length > 20 || !DeliveryAddressValidator.IsSpecific(diaChi))
        {
            TempData["Error"] = !DeliveryAddressValidator.IsSpecific(diaChi)
                ? DeliveryAddressValidator.ErrorMessage
                : "Họ tên hoặc số điện thoại vượt quá độ dài cho phép.";
            return RedirectToAction(nameof(Profile));
        }

        if (!soDienThoai.All(char.IsDigit))
        {
            TempData["Error"] = "Số điện thoại chỉ được nhập số.";
            return RedirectToAction(nameof(Profile));
        }

        customer.HoTen = hoTen.Trim();
        customer.SoDienThoai = soDienThoai.Trim();
        customer.DiaChi = diaChi.Trim();
        await _context.SaveChangesAsync();
        TempData["Message"] = "Đã cập nhật hồ sơ.";
        return RedirectToAction(nameof(Profile));
    }

    public async Task<IActionResult> History()
    {
        var id = CurrentCustomerId();
        var orders = await _context.DonHangs.Where(x => x.KhachHangID == id).OrderByDescending(x => x.NgayDat).ToListAsync();
        return View(orders);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword)
    {
        var customer = await CurrentCustomer();
        if (customer == null) return NotFound();
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            return RedirectToAction(nameof(Profile));
        }
        if (!PasswordHasher.Verify(oldPassword, customer.MatKhauHash))
        {
            TempData["Error"] = "Mật khẩu cũ không đúng.";
            return RedirectToAction(nameof(Profile));
        }
        customer.MatKhauHash = PasswordHasher.Hash(newPassword);
        await _context.SaveChangesAsync();
        TempData["Message"] = "Đã đổi mật khẩu.";
        return RedirectToAction(nameof(Profile));
    }

    private int CurrentCustomerId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private Task<Models.KhachHang?> CurrentCustomer() => _context.KhachHangs.FindAsync(CurrentCustomerId()).AsTask();
}
