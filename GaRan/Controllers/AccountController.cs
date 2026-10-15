using System.Security.Claims;
using GaRan.Data;
using GaRan.Models;
using GaRan.Services;
using GaRan.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Controllers;

[EnableRateLimiting("sensitive")]
public class AccountController : Controller
{
    private readonly GaRanDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IOrderEmailSender _emailSender;

    public AccountController(GaRanDbContext context, IConfiguration configuration, IOrderEmailSender emailSender)
    {
        _context = context;
        _configuration = configuration;
        _emailSender = emailSender;
    }

    public IActionResult Login()
    {
        return View(new LoginVM());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var login = vm.UsernameOrEmail.Trim();

        var customer = await _context.KhachHangs
            .FirstOrDefaultAsync(x => x.Email == login);
        if (customer != null && !customer.TrangThai)
        {
            ModelState.AddModelError("", "Tài khoản đã bị khóa.");
            return View(vm);
        }

        if (customer != null && PasswordHasher.Verify(vm.Password, customer.MatKhauHash))
        {
            if (PasswordHasher.NeedsRehash(customer.MatKhauHash))
            {
                customer.MatKhauHash = PasswordHasher.Hash(vm.Password);
                await _context.SaveChangesAsync();
            }
            await SignIn(customer.KhachHangID, customer.HoTen, "KhachHang", "Customer");
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
        return View(vm);
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVM vm, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(vm);

        var customer = await _context.KhachHangs.FirstOrDefaultAsync(
            x => x.Email == vm.Email.Trim() && x.TrangThai, cancellationToken);
        if (customer != null)
        {
            var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            customer.MaXacNhan = code;
            customer.HanMaXacNhan = DateTime.Now.AddMinutes(15);
            await _context.SaveChangesAsync(cancellationToken);
            var sent = await _emailSender.SendPasswordResetCodeAsync(customer.Email, code, cancellationToken);
            if (!sent)
            {
                customer.MaXacNhan = null;
                customer.HanMaXacNhan = null;
                await _context.SaveChangesAsync(cancellationToken);
                ViewBag.DeliveryFailed = true;
            }
        }

        return View("ForgotPasswordSent");
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email) => View(new ResetPasswordVM { Email = email ?? string.Empty });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVM vm, CancellationToken cancellationToken)
    {
        vm.Email = vm.Email?.Trim() ?? string.Empty;
        vm.Code = vm.Code?.Trim() ?? string.Empty;
        if (!ModelState.IsValid) return View(vm);

        var customer = await _context.KhachHangs.FirstOrDefaultAsync(x => x.Email == vm.Email, cancellationToken);
        if (customer == null || !customer.TrangThai || customer.MaXacNhan != vm.Code ||
            !customer.HanMaXacNhan.HasValue || customer.HanMaXacNhan.Value <= DateTime.Now)
        {
            ModelState.AddModelError(string.Empty, "Mã xác nhận không đúng hoặc đã hết hạn.");
            return View(vm);
        }

        customer.MatKhauHash = PasswordHasher.Hash(vm.Password);
        customer.MaXacNhan = null;
        customer.HanMaXacNhan = null;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["Message"] = "Đã đổi mật khẩu. Bạn có thể đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult StaffLogin() => View();

    public IActionResult AdminLogin() => View("AdminLogin", new LoginVM());

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> AdminLogin(LoginVM vm)
    {
        return InternalLogin(vm, nameof(AdminLogin), "Admin");
    }

    public IActionResult EmployeeLogin() => View(new LoginVM());

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> EmployeeLogin(LoginVM vm)
    {
        return InternalLogin(vm, nameof(EmployeeLogin), "NhanVien");
    }

    public IActionResult Register() => View(new RegisterVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVM vm)
    {
        if (!ModelState.IsValid) return View(vm);

        vm.Email = vm.Email.Trim();
        vm.HoTen = vm.HoTen.Trim();
        vm.SoDienThoai = vm.SoDienThoai.Trim();
        vm.DiaChi = vm.DiaChi.Trim();

        if (await _context.KhachHangs.AnyAsync(x => x.Email == vm.Email))
        {
            ModelState.AddModelError(nameof(vm.Email), "Email đã được sử dụng.");
            return View(vm);
        }

        var customer = new KhachHang
        {
            HoTen = vm.HoTen,
            Email = vm.Email,
            SoDienThoai = vm.SoDienThoai,
            DiaChi = vm.DiaChi,
            MatKhauHash = PasswordHasher.Hash(vm.Password),
            TrangThai = true,
            NgayTao = DateTime.Now
        };

        _context.KhachHangs.Add(customer);
        await _context.SaveChangesAsync();

        await SignIn(customer.KhachHangID, customer.HoTen, "KhachHang", "Customer");
        return IsInternalPortal()
            ? RedirectToAction(nameof(StaffLogin))
            : RedirectToAction("Index", "Home");
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        HttpContext.Session.Clear();
        return IsInternalPortal()
            ? RedirectToAction(nameof(StaffLogin))
            : RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    private async Task<IActionResult> InternalLogin(LoginVM vm, string viewName, params string[] allowedRoles)
    {
        if (!ModelState.IsValid) return View(viewName, vm);

        var login = vm.UsernameOrEmail.Trim();
        var user = await _context.NguoiDungs
            .Include(x => x.VaiTro)
            .FirstOrDefaultAsync(x => x.TenDangNhap == login || x.Email == login);

        var role = user?.VaiTro?.TenVaiTro ?? "";
        if (user != null && allowedRoles.Contains(role) && !user.TrangThai)
        {
            ModelState.AddModelError("", "Tài khoản đã bị khóa.");
            return View(viewName, vm);
        }

        if (user == null || !allowedRoles.Contains(role) || !PasswordHasher.Verify(vm.Password, user.MatKhauHash))
        {
            ModelState.AddModelError("", "Tài khoản hoặc mật khẩu không đúng với khu vực đăng nhập này.");
            return View(viewName, vm);
        }

        if (PasswordHasher.NeedsRehash(user.MatKhauHash))
        {
            user.MatKhauHash = PasswordHasher.Hash(vm.Password);
            await _context.SaveChangesAsync();
        }
        await SignIn(user.NguoiDungID, user.HoTen, role, "Staff");
        return RedirectInternalUser(role);
    }

    private IActionResult RedirectInternalUser(string role)
    {
        if (role == "Admin")
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

        return RedirectToAction("Index", "OrderAdmin", new { area = "Admin" });
    }

    private bool IsInternalPortal() =>
        string.Equals(_configuration["Portal:Mode"], "Internal", StringComparison.OrdinalIgnoreCase);

    private async Task SignIn(int id, string name, string role, string type)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new("AccountType", type)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
    }
}
