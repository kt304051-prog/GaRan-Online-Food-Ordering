using GaRan.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GaRan.ViewComponents;

public sealed class StoreContactViewComponent : ViewComponent
{
    private readonly GaRanDbContext _context;
    private readonly IConfiguration _configuration;

    public StoreContactViewComponent(GaRanDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IViewComponentResult> InvokeAsync(string part = "all")
    {
        var settings = await _context.CauHinhGiaoHangs
            .AsNoTracking()
            .Where(x => x.CauHinhGiaoHangID == 1)
            .Select(x => new { x.EmailCuaHang, x.DienThoaiCuaHang })
            .FirstOrDefaultAsync();

        var email = FirstValue(settings?.EmailCuaHang, _configuration["Store:Email"], "cskh@garan.vn");
        var phone = FirstValue(settings?.DienThoaiCuaHang, _configuration["Store:Phone"], "1900 6868");

        return View("Default", new StoreContactVM(email, phone, part));
    }

    private static string FirstValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))!.Trim();
}

public sealed record StoreContactVM(string Email, string Phone, string Part);
