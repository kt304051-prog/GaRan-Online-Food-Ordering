using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GaRan.Models;

public class VaiTro
{
    public int VaiTroID { get; set; }
    [Required, StringLength(50)] public string TenVaiTro { get; set; } = string.Empty;
    public ICollection<NguoiDung> NguoiDungs { get; set; } = new List<NguoiDung>();
}

public class NguoiDung
{
    public int NguoiDungID { get; set; }
    [Required, StringLength(120)] public string HoTen { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(20)] public string SoDienThoai { get; set; } = string.Empty;
    [Required, StringLength(50)] public string TenDangNhap { get; set; } = string.Empty;
    [Required] public string MatKhauHash { get; set; } = string.Empty;
    public int VaiTroID { get; set; }
    public bool TrangThai { get; set; } = true;
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public VaiTro? VaiTro { get; set; }
    public ICollection<DonHang> DonHangNhanViens { get; set; } = new List<DonHang>();
}

public class KhachHang
{
    public int KhachHangID { get; set; }
    [Required, StringLength(120)] public string HoTen { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(20)] public string SoDienThoai { get; set; } = string.Empty;
    [Required] public string MatKhauHash { get; set; } = string.Empty;
    [Required, StringLength(250)] public string DiaChi { get; set; } = string.Empty;
    public bool TrangThai { get; set; } = true;
    [StringLength(20)] public string? MaXacNhan { get; set; }
    public DateTime? HanMaXacNhan { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public int DiemTichLuy { get; set; }
    public ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();
}

public class DanhMuc
{
    public int DanhMucID { get; set; }
    [Required, StringLength(100)] public string TenDanhMuc { get; set; } = string.Empty;
    [StringLength(255)] public string? MoTa { get; set; }
    public bool TrangThai { get; set; } = true;
    public ICollection<MonAn> MonAns { get; set; } = new List<MonAn>();
}

public class MonAn
{
    public int MonAnID { get; set; }
    public int DanhMucID { get; set; }
    [Required, StringLength(150)] public string TenMon { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal Gia { get; set; }
    [StringLength(255)] public string? HinhAnh { get; set; }
    [StringLength(500)] public string? MoTa { get; set; }
    public bool NoiBat { get; set; }
    public bool TrangThai { get; set; } = true;
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public DanhMuc? DanhMuc { get; set; }
}

public class KhuyenMai
{
    public int KhuyenMaiID { get; set; }
    [Required, StringLength(50)] public string MaKhuyenMai { get; set; } = string.Empty;
    [Required, StringLength(150)] public string TenKhuyenMai { get; set; } = string.Empty;
    [Range(1, 100, ErrorMessage = "Phần trăm giảm phải từ 1 đến 100.")]
    public int PhanTramGiam { get; set; }
    public DateTime NgayBatDau { get; set; }
    public DateTime NgayKetThuc { get; set; }
    public bool TrangThai { get; set; } = true;
}

public class DonHang
{
    public int DonHangID { get; set; }
    public int? KhachHangID { get; set; }
    public int? NhanVienID { get; set; }
    public int? KhuyenMaiID { get; set; }
    public DateTime NgayDat { get; set; } = DateTime.Now;
    [Required, StringLength(250)] public string DiaChiGiaoHang { get; set; } = string.Empty;
    [StringLength(500)] public string? GhiChu { get; set; }
    [Required, StringLength(20)] public string SoDienThoaiNhan { get; set; } = string.Empty;
    [StringLength(120)] public string? TenKhachDat { get; set; }
    [StringLength(150)] public string? EmailKhachDat { get; set; }
    [StringLength(100)] public string? TinhThanh { get; set; }
    [StringLength(100)] public string? QuanHuyen { get; set; }
    [StringLength(100)] public string? PhuongXa { get; set; }
    [Column(TypeName = "decimal(8,2)")] public decimal KhoangCachKm { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TienHang { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TienGiamDiem { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TienGiamThanhVien { get; set; }
    public int DiemDaDung { get; set; }
    public int DiemDaCong { get; set; }
    public bool DiemDaCongThuong { get; set; }
    public bool DiemDaHoanLai { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TienThue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PhiGiaoHang { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TongTien { get; set; }
    [Required, StringLength(50)] public string TrangThaiDonHang { get; set; } = "Chờ xác nhận";
    public KhachHang? KhachHang { get; set; }
    public NguoiDung? NhanVien { get; set; }
    public KhuyenMai? KhuyenMai { get; set; }
    public ICollection<ChiTietDonHang> ChiTietDonHangs { get; set; } = new List<ChiTietDonHang>();
    public ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
}

public class ChiTietDonHang
{
    public int ChiTietID { get; set; }
    public int DonHangID { get; set; }
    public int MonAnID { get; set; }
    public int SoLuong { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DonGia { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ThanhTien { get; set; }
    public DonHang? DonHang { get; set; }
    public MonAn? MonAn { get; set; }
}

public class ThanhToan
{
    public int ThanhToanID { get; set; }
    public int DonHangID { get; set; }
    [Required, StringLength(80)] public string PhuongThucThanhToan { get; set; } = "COD";
    [Column(TypeName = "decimal(18,2)")] public decimal SoTien { get; set; }
    [Required, StringLength(50)] public string TrangThaiThanhToan { get; set; } = "Chưa thanh toán";
    public DateTime? NgayThanhToan { get; set; }
    [StringLength(100)] public string? MaGiaoDich { get; set; }
    [StringLength(100)] public string? PayOsPaymentLinkId { get; set; }
    [StringLength(1000)] public string? PayOsCheckoutUrl { get; set; }
    public int? PayOsOrderCode { get; set; }
    public DonHang? DonHang { get; set; }
}

public class GiaoDichWebhookThanhToan
{
    public int GiaoDichWebhookThanhToanID { get; set; }
    [Required, StringLength(30)] public string NhaCungCap { get; set; } = "SePay";
    [Required, StringLength(100)] public string MaGiaoDichNhaCungCap { get; set; } = string.Empty;
    [StringLength(100)] public string? MaThamChieuNganHang { get; set; }
    [StringLength(100)] public string? MaThanhToan { get; set; }
    [StringLength(500)] public string? NoiDungChuyenKhoan { get; set; }
    [StringLength(30)] public string? SoTaiKhoanNhan { get; set; }
    [StringLength(30)] public string? LoaiGiaoDich { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SoTien { get; set; }
    public DateTime NhanLuc { get; set; } = DateTime.UtcNow;
    public int? ThanhToanID { get; set; }
    public bool DaCongVaoThanhToan { get; set; }
    public ThanhToan? ThanhToan { get; set; }
}

public class GiaoHang
{
    public int GiaoHangID { get; set; }
    public int DonHangID { get; set; }
    public DateTime? NgayNhanDon { get; set; }
    public DateTime? NgayGiaoHang { get; set; }
    [Required, StringLength(50)] public string TrangThaiGiaoHang { get; set; } = "Chờ nhận";
    public DonHang? DonHang { get; set; }
}

public class CauHinhGiaoHang
{
    public int CauHinhGiaoHangID { get; set; }
    [Column(TypeName = "decimal(8,2)")] public decimal MienPhiDenKm { get; set; } = 3m;
    [Column(TypeName = "decimal(8,2)")] public decimal ToiDaKm { get; set; } = 5m;
    [Column(TypeName = "decimal(8,2)")] public decimal UocTinhCungPhuongKm { get; set; } = 2m;
    [Column(TypeName = "decimal(8,2)")] public decimal UocTinhKhacPhuongKm { get; set; } = 4.5m;
    [Column(TypeName = "decimal(18,2)")] public decimal Phi3Den4Km { get; set; } = 12000m;
    [Column(TypeName = "decimal(18,2)")] public decimal Phi4Den5Km { get; set; } = 22000m;
    [Column(TypeName = "decimal(5,4)")] public decimal TyLeThue { get; set; } = 0.10m;
    [StringLength(150)] public string? TenCuaHang { get; set; }
    [StringLength(250)] public string? DiaChiCuaHang { get; set; }
    [StringLength(20)] public string? DienThoaiCuaHang { get; set; }
    [EmailAddress, StringLength(150)] public string? EmailCuaHang { get; set; }
    [StringLength(10)] public string? NganHangBin { get; set; }
    [StringLength(80)] public string? TenNganHang { get; set; }
    [StringLength(30)] public string? SoTaiKhoan { get; set; }
    [StringLength(100)] public string? ChuTaiKhoan { get; set; }
    [StringLength(20), RegularExpression(@"^[A-Za-z0-9_-]*$", ErrorMessage = "Tiền tố chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới.")]
    public string TienToNoiDung { get; set; } = "WOCORDER";
    public bool BatCOD { get; set; } = true;
    public bool BatVietQR { get; set; } = true;
}

public class HoaDon
{
    public int HoaDonID { get; set; }
    public int DonHangID { get; set; }
    public int? NguoiLapID { get; set; }
    public DateTime NgayLap { get; set; } = DateTime.Now;
    [Column(TypeName = "decimal(18,2)")] public decimal TongTien { get; set; }
    public DonHang? DonHang { get; set; }
    public NguoiDung? NguoiLap { get; set; }
}

public class DanhGia
{
    public int DanhGiaID { get; set; }
    public int KhachHangID { get; set; }
    public int? MonAnID { get; set; }
    public int DonHangID { get; set; }
    [Required, StringLength(20)] public string LoaiDanhGia { get; set; } = "MonAn";
    [Range(1, 5)] public int SoSao { get; set; }
    [StringLength(500)] public string? NoiDung { get; set; }
    public bool TrangThai { get; set; }
    public DateTime NgayDanhGia { get; set; } = DateTime.Now;
    public KhachHang? KhachHang { get; set; }
    public MonAn? MonAn { get; set; }
    public DonHang? DonHang { get; set; }
}
