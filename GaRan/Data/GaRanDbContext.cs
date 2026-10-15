using GaRan.Models;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Data;

public class GaRanDbContext : DbContext
{
    public GaRanDbContext(DbContextOptions<GaRanDbContext> options) : base(options) { }

    public DbSet<VaiTro> VaiTros => Set<VaiTro>();
    public DbSet<NguoiDung> NguoiDungs => Set<NguoiDung>();
    public DbSet<KhachHang> KhachHangs => Set<KhachHang>();
    public DbSet<DanhMuc> DanhMucs => Set<DanhMuc>();
    public DbSet<MonAn> MonAns => Set<MonAn>();
    public DbSet<KhuyenMai> KhuyenMais => Set<KhuyenMai>();
    public DbSet<DonHang> DonHangs => Set<DonHang>();
    public DbSet<ChiTietDonHang> ChiTietDonHangs => Set<ChiTietDonHang>();
    public DbSet<ThanhToan> ThanhToans => Set<ThanhToan>();
    public DbSet<GiaoDichWebhookThanhToan> GiaoDichWebhookThanhToans => Set<GiaoDichWebhookThanhToan>();
    public DbSet<GiaoHang> GiaoHangs => Set<GiaoHang>();
    public DbSet<CauHinhGiaoHang> CauHinhGiaoHangs => Set<CauHinhGiaoHang>();
    public DbSet<HoaDon> HoaDons => Set<HoaDon>();
    public DbSet<DanhGia> DanhGias => Set<DanhGia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VaiTro>().HasKey(x => x.VaiTroID);
        modelBuilder.Entity<NguoiDung>().HasKey(x => x.NguoiDungID);
        modelBuilder.Entity<KhachHang>().HasKey(x => x.KhachHangID);
        modelBuilder.Entity<DanhMuc>().HasKey(x => x.DanhMucID);
        modelBuilder.Entity<MonAn>().HasKey(x => x.MonAnID);
        modelBuilder.Entity<KhuyenMai>().HasKey(x => x.KhuyenMaiID);
        modelBuilder.Entity<DonHang>().HasKey(x => x.DonHangID);
        modelBuilder.Entity<ChiTietDonHang>().HasKey(x => x.ChiTietID);
        modelBuilder.Entity<ThanhToan>().HasKey(x => x.ThanhToanID);
        modelBuilder.Entity<GiaoHang>().HasKey(x => x.GiaoHangID);
        modelBuilder.Entity<CauHinhGiaoHang>().HasKey(x => x.CauHinhGiaoHangID);
        modelBuilder.Entity<HoaDon>().HasKey(x => x.HoaDonID);
        modelBuilder.Entity<DanhGia>().HasKey(x => x.DanhGiaID);

        modelBuilder.Entity<NguoiDung>().HasIndex(x => x.TenDangNhap).IsUnique();
        modelBuilder.Entity<KhachHang>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<KhuyenMai>().HasIndex(x => x.MaKhuyenMai).IsUnique();
        modelBuilder.Entity<ThanhToan>().HasIndex(x => x.DonHangID).IsUnique();
        modelBuilder.Entity<ThanhToan>().HasIndex(x => x.PayOsOrderCode).IsUnique().HasFilter("[PayOsOrderCode] IS NOT NULL");
        modelBuilder.Entity<GiaoDichWebhookThanhToan>().HasIndex(x => new { x.NhaCungCap, x.MaGiaoDichNhaCungCap }).IsUnique();
        modelBuilder.Entity<GiaoDichWebhookThanhToan>().HasIndex(x => x.ThanhToanID);
        modelBuilder.Entity<GiaoHang>().HasIndex(x => x.DonHangID).IsUnique();
        modelBuilder.Entity<HoaDon>().HasIndex(x => x.DonHangID).IsUnique();

        modelBuilder.Entity<NguoiDung>().Property(x => x.TrangThai)
            .HasConversion(v => v ? "Hoạt động" : "Khóa", v => IsActive(v));
        modelBuilder.Entity<KhachHang>().Property(x => x.TrangThai)
            .HasConversion(v => v ? "Hoạt động" : "Khóa", v => IsActive(v));
        modelBuilder.Entity<DanhMuc>().Property(x => x.TrangThai)
            .HasConversion(v => v ? "Hiển thị" : "Ẩn", v => IsActive(v));
        modelBuilder.Entity<MonAn>().Property(x => x.TrangThai)
            .HasConversion(v => v ? "Đang bán" : "Ngừng bán", v => IsActive(v));
        modelBuilder.Entity<KhuyenMai>().Property(x => x.TrangThai)
            .HasConversion(v => v ? "Đang áp dụng" : "Ngừng áp dụng", v => IsActive(v));
        modelBuilder.Entity<DanhGia>().Property(x => x.TrangThai)
            .HasConversion(v => v ? "Đã duyệt" : "Chờ duyệt", v => IsActive(v));

        modelBuilder.Entity<NguoiDung>()
            .HasOne(x => x.VaiTro)
            .WithMany(x => x.NguoiDungs)
            .HasForeignKey(x => x.VaiTroID);

        modelBuilder.Entity<MonAn>()
            .HasOne(x => x.DanhMuc)
            .WithMany(x => x.MonAns)
            .HasForeignKey(x => x.DanhMucID);

        modelBuilder.Entity<DonHang>()
            .HasOne(x => x.KhachHang)
            .WithMany(x => x.DonHangs)
            .HasForeignKey(x => x.KhachHangID);

        modelBuilder.Entity<DonHang>()
            .HasOne(x => x.NhanVien)
            .WithMany(x => x.DonHangNhanViens)
            .HasForeignKey(x => x.NhanVienID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ChiTietDonHang>()   
            .HasOne(x => x.DonHang)
            .WithMany(x => x.ChiTietDonHangs)
            .HasForeignKey(x => x.DonHangID);

        modelBuilder.Entity<ChiTietDonHang>()
            .HasOne(x => x.MonAn)
            .WithMany()
            .HasForeignKey(x => x.MonAnID);

        modelBuilder.Entity<ThanhToan>()
            .HasOne(x => x.DonHang)
            .WithMany(x => x.ThanhToans)
            .HasForeignKey(x => x.DonHangID);

        modelBuilder.Entity<GiaoDichWebhookThanhToan>()
            .HasOne(x => x.ThanhToan)
            .WithMany()
            .HasForeignKey(x => x.ThanhToanID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<GiaoHang>()
            .HasOne(x => x.DonHang)
            .WithMany()
            .HasForeignKey(x => x.DonHangID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<HoaDon>()
            .HasOne(x => x.DonHang)
            .WithMany()
            .HasForeignKey(x => x.DonHangID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<HoaDon>()
            .HasOne(x => x.NguoiLap)
            .WithMany()
            .HasForeignKey(x => x.NguoiLapID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<DanhGia>()
            .HasOne(x => x.KhachHang)
            .WithMany()
            .HasForeignKey(x => x.KhachHangID)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<DanhGia>()
            .HasOne(x => x.MonAn)
            .WithMany()
            .HasForeignKey(x => x.MonAnID)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<DanhGia>()
            .HasOne(x => x.DonHang)
            .WithMany()
            .HasForeignKey(x => x.DonHangID)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static bool IsActive(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim().ToLowerInvariant();
        return text is "1" or "true" or "active"
            or "hoạt động" or "hoat dong"
            or "hiển thị" or "hien thi"
            or "đang bán" or "dang ban"
            or "đang áp dụng" or "dang ap dung"
            or "đã duyệt" or "da duyet";
    }
}
