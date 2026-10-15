using System.ComponentModel.DataAnnotations;
using GaRan.Models;

namespace GaRan.ViewModels;

public class LoginVM
{
    [Required(ErrorMessage = "Vui lòng nhập tài khoản/email")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu"), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool IsStaff { get; set; }
}

public class ForgotPasswordVM
{
    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không đúng định dạng"), StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordVM
{
    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không đúng định dạng"), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã xác nhận"), RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác nhận gồm 6 chữ số")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới"), DataType(DataType.Password), MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu"), Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp"), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RegisterVM
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), Display(Name = "Họ tên")]
    public string HoTen { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email phải có @ và đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"), Display(Name = "Số điện thoại")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Số điện thoại chỉ được nhập số")]
    public string SoDienThoai { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ"), Display(Name = "Địa chỉ")]
    public string DiaChi { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu"), DataType(DataType.Password), MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu"), Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp"), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class CartItemVM
{
    public int MonAnID { get; set; }
    public string TenMon { get; set; } = string.Empty;
    public string? HinhAnh { get; set; }
    public decimal DonGia { get; set; }
    public int SoLuong { get; set; }
    public decimal ThanhTien => DonGia * SoLuong;
}

public class CartVM
{
    public List<CartItemVM> Items { get; set; } = new();
    public decimal TongTien => Items.Sum(x => x.ThanhTien);
    public int TongSoLuong => Items.Sum(x => x.SoLuong);
}

public class FoodRatingSummaryVM
{
    public double Average { get; set; }
    public int Count { get; set; }
}

public class ReviewFoodSummaryVM
{
    public int FoodId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
    public double Average { get; set; }
    public int Count { get; set; }
}

public class ReviewIndexVM
{
    public List<DanhGia> Reviews { get; set; } = new();
    public List<ReviewFoodSummaryVM> PopularFoods { get; set; } = new();
    public double Average { get; set; }
    public int Count { get; set; }
    public Dictionary<int, int> RatingCounts { get; set; } = new();
    public string Type { get; set; } = "all";
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
}

public class CheckoutVM
{
    public CartVM Cart { get; set; } = new();

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? GhiChu { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(120)]
    public string HoTen { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không đúng định dạng"), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Địa chỉ giao hàng")]
    public string DiaChiGiaoHang { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành phố"), StringLength(100)]
    public string TinhThanh { get; set; } = "Thành phố Hồ Chí Minh";

    [Required(ErrorMessage = "Vui lòng chọn phường/xã"), StringLength(100)]
    public string PhuongXa { get; set; } = string.Empty;

    [StringLength(150)]
    public string DiaChiChiTiet { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại nhận"), Display(Name = "Số điện thoại nhận"), StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 chữ số")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Số điện thoại nhận chỉ được nhập số")]
    public string SoDienThoaiNhan { get; set; } = string.Empty;

    [Display(Name = "Mã khuyến mãi")]
    public string? MaKhuyenMai { get; set; }

    [Required]
    public string PhuongThucThanhToan { get; set; } = "COD";

    public decimal GiamGia { get; set; }
    public decimal GiamGiaThanhVien { get; set; }
    public decimal PhanTramGiamThanhVien { get; set; }
    public bool LaThanhVienVip { get; set; }
    public int DiemTichLuyTronDoi { get; set; }
    public int DiemConThieuLenVip { get; set; }
    public int DiemHienCo { get; set; }
    public bool SuDungDiem { get; set; }
    public int DiemSuDung { get; set; }
    public decimal GiamGiaDiem { get; set; }
    public decimal KhoangCachKm { get; set; }
    public decimal PhiGiaoHang { get; set; }
    public decimal TienThue { get; set; }
    public decimal TyLeThue { get; set; } = 10m;
    public bool DaTinhPhiGiaoHang { get; set; }
    public decimal? QuoteDistanceKm { get; set; }
    public decimal? QuoteTotal { get; set; }
    public string QuoteAddress { get; set; } = string.Empty;
    public string DiaChiCuaHang { get; set; } = string.Empty;
    public decimal ThanhToan => Math.Max(0, Cart.TongTien - GiamGia - GiamGiaThanhVien - GiamGiaDiem + TienThue + PhiGiaoHang);
}

public class KhachHangTiemNangVM
{
    public KhachHang KhachHang { get; set; } = new();
    public int SoDonHoanThanh { get; set; }
    public decimal TongChiTieu { get; set; }
    public int DiemTichLuyTronDoi { get; set; }
    public bool LaThanhVienVip { get; set; }
}

public class DeliveryEstimateVM
{
    public string TinhThanh { get; set; } = string.Empty;
    public string PhuongXa { get; set; } = string.Empty;
    public string DiaChiChiTiet { get; set; } = string.Empty;
}

public class OrderVM
{
    public DonHang DonHang { get; set; } = new();
    public List<ChiTietDonHang> ChiTiet { get; set; } = new();
}

public class OrderLookupVM
{
    [Required(ErrorMessage = "Vui lòng nhập mã đơn hàng")]
    public string Code { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại hoặc email")]
    public string Contact { get; set; } = string.Empty;
    public DonHang? Order { get; set; }
    public DateTime? NgayGiaoHang { get; set; }
}

public class RevenueVM
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class DashboardVM
{
    public int TongMonAn { get; set; }
    public int TongDonHang { get; set; }
    public int TongKhachHang { get; set; }
    public decimal TongDoanhThu { get; set; }
    public decimal DoanhThuThangNay { get; set; }
    public decimal DoanhThuThangTruoc { get; set; }
    public int DonHangThangNay { get; set; }
    public int DonHangChoXuLy { get; set; }
    public decimal GiaTriDonTrungBinhThangNay { get; set; }
    public List<RevenueVM> DoanhThuTheoThang { get; set; } = new();
    public List<RevenueVM> TopMonBanChay { get; set; } = new();
    public List<DashboardOrderVM> DonHangGanDay { get; set; } = new();
    public List<DashboardActivityVM> HoatDongGanDay { get; set; } = new();
    public List<RevenueVM> DoanhThuTheoDanhMuc { get; set; } = new();
    public List<DashboardStatusVM> DonHangTheoTrangThai { get; set; } = new();
    public int TongDanhMuc { get; set; }
    public int TongMonDangBan { get; set; }
    public int TongDanhGiaChoDuyet { get; set; }
    public int KhachHangMoiThang { get; set; }
}

public class DashboardOrderVM
{
    public int DonHangID { get; set; }
    public string TenKhach { get; set; } = string.Empty;
    public decimal TongTien { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public DateTime NgayDat { get; set; }
}

public class DashboardActivityVM
{
    public string BieuTuong { get; set; } = string.Empty;
    public string TieuDe { get; set; } = string.Empty;
    public string MoTa { get; set; } = string.Empty;
    public DateTime ThoiGian { get; set; }
    public string? GiaTri { get; set; }
}

public class DashboardStatusVM
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
