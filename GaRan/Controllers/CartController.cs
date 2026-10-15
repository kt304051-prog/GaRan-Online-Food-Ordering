using GaRan.Data;
using GaRan.Models;
using GaRan.Services;
using GaRan.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;

namespace GaRan.Controllers;

public class CartController : Controller
{
    private const string CartKey = "CART";
    private readonly GaRanDbContext _context;
    private readonly DeliveryDistanceService _distance;
    private readonly IConfiguration _configuration;
    private readonly IOrderEmailSender _emailSender;
    private readonly HashSet<string> _hcmWardNames;
    public CartController(GaRanDbContext context, DeliveryDistanceService distance, IConfiguration configuration, IWebHostEnvironment environment, IOrderEmailSender emailSender)
    {
        _context = context;
        _distance = distance;
        _configuration = configuration;
        _emailSender = emailSender;
        var wardsPath = Path.Combine(environment.WebRootPath, "data", "hcm-wards.json");
        using var wardsJson = JsonDocument.Parse(System.IO.File.ReadAllText(wardsPath));
        _hcmWardNames = wardsJson.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString() ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IActionResult Index() => View(GetCart());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int id, int quantity = 1)
    {
        var food = await _context.MonAns.FindAsync(id);
        if (food == null || !food.TrangThai) return NotFound();
        var cart = GetCart();
        var line = cart.Items.FirstOrDefault(x => x.MonAnID == id);
        if (line == null)
        {
            cart.Items.Add(new CartItemVM { MonAnID = id, TenMon = food.TenMon, DonGia = food.Gia, HinhAnh = food.HinhAnh, SoLuong = Math.Clamp(quantity, 1, 100) });
        }
        else line.SoLuong = Math.Clamp(line.SoLuong + Math.Max(1, quantity), 1, 100);
        SaveCart(cart);
        if (IsAjaxRequest()) return Json(new { success = true, count = cart.TongSoLuong, message = $"Đã thêm {food.TenMon} vào giỏ hàng." });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int id, int quantity)
    {
        var cart = GetCart();
        var line = cart.Items.FirstOrDefault(x => x.MonAnID == id);
        if (line != null) line.SoLuong = Math.Clamp(quantity, 1, 100);
        SaveCart(cart);
        if (IsAjaxRequest()) return Json(new { success = true, count = cart.TongSoLuong, total = cart.TongTien, lineTotal = line?.ThanhTien ?? 0m, quantity = line?.SoLuong ?? 0 });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remove(int id)
    {
        var cart = GetCart();
        cart.Items.RemoveAll(x => x.MonAnID == id);
        SaveCart(cart);
        if (IsAjaxRequest()) return Json(new { success = true, count = cart.TongSoLuong, total = cart.TongTien });
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Checkout()
    {
        var customer = User.Identity?.IsAuthenticated == true && User.IsInRole("KhachHang") ? await CurrentCustomer() : null;
        var cart = GetCart();
        if (!cart.Items.Any()) return RedirectToAction(nameof(Index));
        var settings = await GetDeliverySettingsAsync();
        var storeAddress = await _distance.GetStoreAddressAsync();
        ViewBag.StoreAddress = storeAddress;
        ViewBag.DeliveryReady = !string.IsNullOrWhiteSpace(storeAddress);
        ViewBag.AllowCOD = settings.BatCOD;
        ViewBag.AllowVietQR = settings.BatVietQR;
        var vm = new CheckoutVM { Cart = cart, HoTen = customer?.HoTen ?? "", Email = customer?.Email ?? "", TinhThanh = "Thành phố Hồ Chí Minh", DiaChiChiTiet = customer?.DiaChi ?? "", SoDienThoaiNhan = customer?.SoDienThoai ?? "" };
        vm.DiemHienCo = customer?.DiemTichLuy ?? 0;
        await PopulateMembershipAsync(vm, customer);
        vm.GiamGiaThanhVien = vm.LaThanhVienVip
            ? decimal.Round(cart.TongTien * vm.PhanTramGiamThanhVien / 100m, 0, MidpointRounding.AwayFromZero)
            : 0;
        vm.PhuongThucThanhToan = settings.BatCOD ? "COD" : "QR";
        vm.TyLeThue = settings.TyLeThue * 100m;
        vm.TienThue = decimal.Round(Math.Max(0, cart.TongTien - vm.GiamGiaThanhVien) * vm.TyLeThue / 100m, 0, MidpointRounding.AwayFromZero);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("routing")]
    public async Task<IActionResult> EstimateDelivery(DeliveryEstimateVM vm, CancellationToken cancellationToken)
    {
        if (!DeliveryAddressValidator.IsSpecific(vm.DiaChiChiTiet))
            return Json(new { success = false, message = DeliveryAddressValidator.ErrorMessage });
        if (!IsValidAdministrativeAddress(vm.TinhThanh, vm.PhuongXa))
            return Json(new { success = false, message = "Chọn TP.HCM và phường/xã hiện hành để ước tính phí giao hàng." });
        var settings = await GetDeliverySettingsAsync();
        var destination = BuildDestination(vm.TinhThanh, vm.PhuongXa, vm.DiaChiChiTiet);
        var approximateDestination = BuildDestination(vm.TinhThanh, vm.PhuongXa, null);
        var distance = await _distance.GetDistanceKmAsync(destination, approximateDestination, cancellationToken);
        if (distance == null) return Json(new { success = false, message = GetDistanceErrorMessage() });
        if (distance.Value > settings.ToiDaKm) return Json(new { success = false, distanceKm = distance.Value, message = $"Địa chỉ vượt quá phạm vi giao hàng {settings.ToiDaKm:0.##} km." });
        var fee = CalculateDeliveryFee(distance.Value, settings);
        return Json(new { success = true, distanceKm = distance.Value, shippingFee = fee, message = "Đã ước tính quãng đường và phí giao hàng theo vị trí địa chỉ." });
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive")]
    public async Task<IActionResult> CheckCoupon(string? maKhuyenMai, CancellationToken cancellationToken)
    {
        var code = maKhuyenMai?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { success = false, code = "", percent = 0, discount = 0m, message = "Vui lòng nhập mã ưu đãi." });

        var today = DateTime.Today;
        var promo = await _context.KhuyenMais.AsNoTracking().FirstOrDefaultAsync(x =>
            x.MaKhuyenMai == code && x.TrangThai && x.NgayBatDau.Date <= today && x.NgayKetThuc.Date >= today, cancellationToken);
        if (promo == null)
            return Json(new { success = false, code, percent = 0, discount = 0m, message = "Mã ưu đãi không hợp lệ hoặc đã hết hạn." });

        var discount = decimal.Round(GetCart().TongTien * promo.PhanTramGiam / 100m, 0, MidpointRounding.AwayFromZero);
        return Json(new { success = true, code = promo.MaKhuyenMai, percent = promo.PhanTramGiam, discount, message = $"Đã áp dụng ưu đãi giảm {promo.PhanTramGiam}%." });
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive")]
    public async Task<IActionResult> Checkout(CheckoutVM vm, bool confirmOrder = false, CancellationToken cancellationToken = default)
    {
        vm.GhiChu = string.IsNullOrWhiteSpace(vm.GhiChu) ? null : vm.GhiChu.Trim();
        var deliverySettings = await GetDeliverySettingsAsync();
        var storeAddress = await _distance.GetStoreAddressAsync(cancellationToken);
        ViewBag.StoreAddress = storeAddress;
        ViewBag.DeliveryReady = !string.IsNullOrWhiteSpace(storeAddress);
        ViewBag.AllowCOD = deliverySettings.BatCOD;
        ViewBag.AllowVietQR = deliverySettings.BatVietQR;
        vm.Cart = GetCart();
        var loyaltyCustomer = User.Identity?.IsAuthenticated == true && User.IsInRole("KhachHang")
            ? await CurrentCustomer()
            : null;
        vm.DiemHienCo = loyaltyCustomer?.DiemTichLuy ?? 0;
        await PopulateMembershipAsync(vm, loyaltyCustomer, cancellationToken);
        if (!vm.Cart.Items.Any()) ModelState.AddModelError("", "Giỏ hàng đang trống.");
        else
        {
            var ids = vm.Cart.Items.Select(x => x.MonAnID).Distinct().ToList();
            var currentFoods = await _context.MonAns.Where(x => ids.Contains(x.MonAnID) && x.TrangThai).ToDictionaryAsync(x => x.MonAnID);
            if (currentFoods.Count != ids.Count) ModelState.AddModelError("", "Một hoặc nhiều món trong giỏ hiện không còn được bán. Vui lòng kiểm tra lại giỏ hàng.");
            foreach (var line in vm.Cart.Items)
            {
                if (line.SoLuong < 1 || line.SoLuong > 100) ModelState.AddModelError("", "Số lượng món không hợp lệ.");
                if (currentFoods.TryGetValue(line.MonAnID, out var food))
                {
                    line.TenMon = food.TenMon;
                    line.DonGia = food.Gia;
                    line.HinhAnh = food.HinhAnh;
                }
            }
            SaveCart(vm.Cart);
        }
        if (vm.PhuongThucThanhToan is not ("COD" or "QR" or "Bank")) ModelState.AddModelError(nameof(vm.PhuongThucThanhToan), "Phương thức thanh toán không hợp lệ.");
        else if (vm.PhuongThucThanhToan == "COD" && !deliverySettings.BatCOD)
            ModelState.AddModelError(nameof(vm.PhuongThucThanhToan), "Cửa hàng hiện không nhận đơn thanh toán COD.");
        else if ((vm.PhuongThucThanhToan is "QR" or "Bank") && !deliverySettings.BatVietQR)
            ModelState.AddModelError(nameof(vm.PhuongThucThanhToan), "Cửa hàng hiện tạm tắt thanh toán VietQR.");

        if (!IsValidAdministrativeAddress(vm.TinhThanh, vm.PhuongXa))
        {
            ModelState.AddModelError(nameof(vm.PhuongXa), "Vui lòng chọn phường/xã thuộc TP. Hồ Chí Minh trong danh sách.");
        }
        if (!DeliveryAddressValidator.IsSpecific(vm.DiaChiChiTiet))
            ModelState.AddModelError(nameof(vm.DiaChiChiTiet), DeliveryAddressValidator.ErrorMessage);
        var destination = BuildDestination(vm.TinhThanh, vm.PhuongXa, vm.DiaChiChiTiet);
        vm.DiaChiGiaoHang = destination;
        vm.DiaChiCuaHang = storeAddress;
        if (destination.Length > 250)
            ModelState.AddModelError(nameof(vm.DiaChiChiTiet), "Địa chỉ sau khi ghép phường/xã và thành phố vượt quá giới hạn. Hãy rút gọn số nhà, tên đường.");
        var approximateDestination = BuildDestination(vm.TinhThanh, vm.PhuongXa, null);
        var distance = await _distance.GetDistanceKmAsync(destination, approximateDestination, cancellationToken);
        if (distance == null)
            ModelState.AddModelError(nameof(vm.PhuongXa), GetDistanceErrorMessage());
        else
        {
            vm.KhoangCachKm = distance.Value;
            if (distance.Value > deliverySettings.ToiDaKm)
                ModelState.AddModelError(nameof(vm.DiaChiChiTiet), $"Địa chỉ giao hàng vượt quá phạm vi giao hàng của cửa hàng. Vui lòng chọn địa chỉ trong bán kính {deliverySettings.ToiDaKm:0.##} km.");
            vm.PhiGiaoHang = CalculateDeliveryFee(distance.Value, deliverySettings);
        }

        var normalizedCode = vm.MaKhuyenMai?.Trim().ToUpperInvariant();
        KhuyenMai? promo = null;
        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            promo = await _context.KhuyenMais.FirstOrDefaultAsync(x =>
                x.MaKhuyenMai == normalizedCode &&
                x.TrangThai &&
                x.NgayBatDau.Date <= DateTime.Today &&
                x.NgayKetThuc.Date >= DateTime.Today);

            if (promo == null)
            {
                ModelState.AddModelError(nameof(vm.MaKhuyenMai), "Mã khuyến mãi không hợp lệ hoặc đã hết hạn.");
            }
        }

        vm.GiamGia = promo == null ? 0 : decimal.Round(vm.Cart.TongTien * promo.PhanTramGiam / 100m, 0, MidpointRounding.AwayFromZero);
        vm.GiamGiaThanhVien = vm.LaThanhVienVip
            ? decimal.Round(Math.Max(0, vm.Cart.TongTien - vm.GiamGia) * vm.PhanTramGiamThanhVien / 100m, 0, MidpointRounding.AwayFromZero)
            : 0;
        var remainingBeforePoints = Math.Max(0, vm.Cart.TongTien - vm.GiamGia - vm.GiamGiaThanhVien);
        vm.DiemSuDung = vm.SuDungDiem && loyaltyCustomer != null
            ? Math.Min(loyaltyCustomer.DiemTichLuy, (int)(remainingBeforePoints / 100m))
            : 0;
        vm.GiamGiaDiem = vm.DiemSuDung * 100m;
        vm.TyLeThue = deliverySettings.TyLeThue * 100m;
        vm.TienThue = decimal.Round(Math.Max(0, vm.Cart.TongTien - vm.GiamGia - vm.GiamGiaThanhVien - vm.GiamGiaDiem) * deliverySettings.TyLeThue, 0, MidpointRounding.AwayFromZero);
        vm.DaTinhPhiGiaoHang = distance.HasValue &&
            distance.Value <= deliverySettings.ToiDaKm && destination.Length <= 250 &&
            DeliveryAddressValidator.IsSpecific(vm.DiaChiChiTiet);
        if (!confirmOrder)
        {
            vm.QuoteDistanceKm = vm.KhoangCachKm;
            vm.QuoteTotal = vm.ThanhToan;
            vm.QuoteAddress = destination;
            ModelState.Remove(nameof(vm.QuoteDistanceKm));
            ModelState.Remove(nameof(vm.QuoteTotal));
            ModelState.Remove(nameof(vm.QuoteAddress));
        }
        // The posted quote is only a client-side preview. The order total, tax and shipping
        // above are recalculated from server data, so malformed/stale preview fields must
        // never prevent a valid order from being created.
        ModelState.Remove(nameof(vm.QuoteDistanceKm));
        ModelState.Remove(nameof(vm.QuoteTotal));
        ModelState.Remove(nameof(vm.QuoteAddress));
        // These values are computed on the server and are not posted by the checkout form.
        // With nullable reference types enabled, MVC can otherwise mark the unposted
        // non-nullable strings as implicitly required and silently return the checkout view.
        ModelState.Remove(nameof(vm.DiaChiGiaoHang));
        ModelState.Remove(nameof(vm.DiaChiCuaHang));
        if (!ModelState.IsValid) return View(vm);
        if (!confirmOrder) return View(vm);

        var order = new DonHang
        {
            KhachHangID = User.Identity?.IsAuthenticated == true && User.IsInRole("KhachHang")
                ? int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value) : null,
            KhuyenMaiID = promo?.KhuyenMaiID,
            DiaChiGiaoHang = vm.DiaChiGiaoHang,
            GhiChu = vm.GhiChu,
            SoDienThoaiNhan = vm.SoDienThoaiNhan,
            TongTien = vm.ThanhToan,
            TenKhachDat = vm.HoTen.Trim(),
            EmailKhachDat = vm.Email.Trim(),
            TinhThanh = vm.TinhThanh.Trim(),
            QuanHuyen = null,
            PhuongXa = vm.PhuongXa.Trim(),
            KhoangCachKm = vm.KhoangCachKm,
            TienHang = vm.Cart.TongTien - vm.GiamGia - vm.GiamGiaThanhVien - vm.GiamGiaDiem,
            TienGiamDiem = vm.GiamGiaDiem,
            TienGiamThanhVien = vm.GiamGiaThanhVien,
            DiemDaDung = vm.DiemSuDung,
            TienThue = vm.TienThue,
            PhiGiaoHang = vm.PhiGiaoHang
        };
        foreach (var item in vm.Cart.Items)
        {
            order.ChiTietDonHangs.Add(new ChiTietDonHang { MonAnID = item.MonAnID, SoLuong = item.SoLuong, DonGia = item.DonGia, ThanhTien = item.ThanhTien });
        }
        order.ThanhToans.Add(new ThanhToan
        {
            PhuongThucThanhToan = vm.PhuongThucThanhToan,
            SoTien = order.TongTien,
            TrangThaiThanhToan = vm.PhuongThucThanhToan == "COD" ? "Chưa thanh toán" : "Đang chờ thanh toán"
        });
        await using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            if (loyaltyCustomer != null && vm.DiemSuDung > 0)
                loyaltyCustomer.DiemTichLuy -= vm.DiemSuDung;
            _context.DonHangs.Add(order);
            await _context.SaveChangesAsync(cancellationToken);
            var payment = order.ThanhToans.First();
            var transferPrefix = string.IsNullOrWhiteSpace(deliverySettings.TienToNoiDung)
                ? "WOCORDER"
                : deliverySettings.TienToNoiDung.Trim().ToUpperInvariant();
            payment.MaGiaoDich = $"{transferPrefix}{order.DonHangID}";
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await _emailSender.SendOrderCreatedAsync(order, cancellationToken);
        HttpContext.Session.Remove(CartKey);
        if (order.KhachHangID.HasValue) return RedirectToAction("Details", "Order", new { id = order.DonHangID });
        HttpContext.Session.SetInt32("ORDER_CONFIRMATION_ID", order.DonHangID);
        return RedirectToAction("Confirmation", "Order", new { id = order.DonHangID });
    }

    private CartVM GetCart() => HttpContext.Session.GetObject<CartVM>(CartKey) ?? new CartVM();
    private void SaveCart(CartVM cart) => HttpContext.Session.SetObject(CartKey, cart);
    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    private async Task PopulateMembershipAsync(CheckoutVM vm, Models.KhachHang? customer, CancellationToken cancellationToken = default)
    {
        vm.DiemTichLuyTronDoi = customer == null
            ? 0
            : await _context.DonHangs
                .Where(x => x.KhachHangID == customer.KhachHangID && x.TrangThaiDonHang == "Hoàn thành")
                .SumAsync(x => (int?)x.DiemDaCong, cancellationToken) ?? 0;
        vm.LaThanhVienVip = LoyaltyProgram.IsVip(vm.DiemTichLuyTronDoi);
        vm.PhanTramGiamThanhVien = vm.LaThanhVienVip ? LoyaltyProgram.VipDiscountPercent : 0;
        vm.DiemConThieuLenVip = LoyaltyProgram.PointsToVip(vm.DiemTichLuyTronDoi);
    }

    private async Task<CauHinhGiaoHang> GetDeliverySettingsAsync() =>
        await _context.CauHinhGiaoHangs.FindAsync(1) ?? new CauHinhGiaoHang
        {
            MienPhiDenKm = _configuration.GetValue<decimal>("Delivery:FreeDistanceKm", 3m),
            ToiDaKm = _configuration.GetValue<decimal>("Delivery:MaxDistanceKm", 5m),
            Phi3Den4Km = _configuration.GetValue<decimal>("Delivery:Fee3To4Km", 12000m),
            Phi4Den5Km = _configuration.GetValue<decimal>("Delivery:Fee4To5Km", 22000m),
            TyLeThue = _configuration.GetValue<decimal>("Delivery:TaxRate", 0.10m)
        };
    private Task<KhachHang?> CurrentCustomer()
    {
        var id = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        return _context.KhachHangs.FindAsync(id).AsTask();
    }

    private bool IsValidAdministrativeAddress(string? city, string? ward) =>
        string.Equals(city?.Trim(), "Thành phố Hồ Chí Minh", StringComparison.OrdinalIgnoreCase) &&
        _hcmWardNames.Contains(ward?.Trim() ?? string.Empty);

    private static decimal CalculateDeliveryFee(decimal distanceKm, CauHinhGiaoHang settings)
    {
        if (distanceKm < settings.MienPhiDenKm) return 0m;
        var farZoneStartsAt = settings.MienPhiDenKm + (settings.ToiDaKm - settings.MienPhiDenKm) / 2m;
        return distanceKm < farZoneStartsAt ? settings.Phi3Den4Km : settings.Phi4Den5Km;
    }

    private static string GetDistanceErrorMessage() =>
        "Hiện chưa thể ước tính phí giao hàng. Vui lòng thử lại hoặc liên hệ cửa hàng.";

    private static string BuildDestination(string? city, string? ward, string? detail) =>
        string.Join(", ", new[] { detail, ward, city, "Việt Nam" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim()));
}
