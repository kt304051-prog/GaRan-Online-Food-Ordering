using GaRan.Data;
using GaRan.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var portalMode = builder.Configuration["Portal:Mode"] ?? "Customer";
var internalPortal = portalMode.Equals("Internal", StringComparison.OrdinalIgnoreCase);

builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}
else
{
    builder.Logging.AddJsonConsole();
}

// Add services to the container.
builder.Services.AddDbContext<GaRanDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<GaRan.Services.DeliveryDistanceService>();
builder.Services.AddScoped<GaRan.Services.IOrderEmailSender, GaRan.Services.OrderEmailSender>();
builder.Services.AddHttpClient(nameof(GaRan.Services.PayOsService), client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<GaRan.Services.IPayOsService, GaRan.Services.PayOsService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddControllersWithViews(options =>
    options.Conventions.Add(new GaRan.Services.PortalModeConvention(portalMode)));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("sensitive", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("routing", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddSession(options =>
{
    options.Cookie.Name = internalPortal ? ".GaRan.Internal.Session" : ".GaRan.Customer.Session";
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = internalPortal ? ".GaRan.Internal.Auth" : ".GaRan.Customer.Auth";
        options.LoginPath = internalPortal ? "/Account/EmployeeLogin" : "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<GaRanDbContext>();
    await DatabaseSeeder.EnsureOrderNoteColumnAsync(context);
    await DatabaseSeeder.EnsurePasswordResetColumnsAsync(context);
    if (!internalPortal)
        await DatabaseSeeder.SeedAsync(context, builder.Environment.IsDevelopment());
    else
        await DatabaseSeeder.EnsureMemberDiscountColumnAsync(context);
    await DatabaseSeeder.EnsureReviewColumnsAsync(context);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: internalPortal
        ? "{controller=Account}/{action=StaffLogin}/{id?}"
        : "{controller=Home}/{action=Index}/{id?}");

app.Run();
