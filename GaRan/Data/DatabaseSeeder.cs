using GaRan.Models;
using GaRan.Services;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Data;

public static class DatabaseSeeder
{
    private static readonly Dictionary<string, string> TextFixes = new()
    {
        ["Quáº£n trá»‹ viÃªn"] = "Quản trị viên",
        ["NhÃ¢n viÃªn bÃ¡n hÃ ng"] = "Nhân viên bán hàng",
        ["GÃ  rÃ¡n"] = "Gà rán",
        ["GÃ  rÃ¡n giÃ²n nÃ³ng"] = "Gà rán giòn nóng",
        ["Burger no bá»¥ng"] = "Burger no bụng",
        ["Combo tiáº¿t kiá»‡m"] = "Combo tiết kiệm",
        ["NÆ°á»›c uá»‘ng"] = "Nước uống",
        ["Äá»“ uá»‘ng mÃ¡t láº¡nh"] = "Đồ uống mát lạnh",
        ["MÃ³n phá»¥"] = "Món phụ",
        ["Ä‚n kÃ¨m háº¥p dáº«n"] = "Ăn kèm hấp dẫn",
        ["GÃ  rÃ¡n giÃ²n cay"] = "Gà rán giòn cay",
        ["Miáº¿ng gÃ  rÃ¡n phá»§ vá»‹ cay nháº¹, vá» giÃ²n."] = "Miếng gà rán phủ vị cay nhẹ, vỏ giòn.",
        ["Burger gÃ  phÃ´ mai"] = "Burger gà phô mai",
        ["Burger gÃ  giÃ²n kÃ¨m phÃ´ mai bÃ©o thÆ¡m."] = "Burger gà giòn kèm phô mai béo thơm.",
        ["Combo gia Ä‘Ã¬nh"] = "Combo gia đình",
        ["Combo gÃ  rÃ¡n, khoai tÃ¢y vÃ  nÆ°á»›c uá»‘ng."] = "Combo gà rán, khoai tây và nước uống.",
        ["Pepsi ly lá»›n"] = "Pepsi ly lớn",
        ["NÆ°á»›c uá»‘ng mÃ¡t láº¡nh."] = "Nước uống mát lạnh.",
        ["Khoai tÃ¢y chiÃªn"] = "Khoai tây chiên",
        ["Khoai tÃ¢y chiÃªn vÃ ng giÃ²n."] = "Khoai tây chiên vàng giòn.",
        ["Chá» xÃ¡c nháº­n"] = "Chờ xác nhận",
        ["ÄÃ£ xÃ¡c nháº­n"] = "Đã xác nhận",
        ["Äang cháº¿ biáº¿n"] = "Đang chế biến",
        ["Chá» giao hÃ ng"] = "Chờ giao hàng",
        ["Äang giao"] = "Đang giao",
        ["HoÃ n thÃ nh"] = "Hoàn thành",
        ["ÄÃ£ há»§y"] = "Đã hủy",
        ["ChÆ°a thanh toÃ¡n"] = "Chưa thanh toán",
        ["ÄÃ£ thanh toÃ¡n"] = "Đã thanh toán",
        ["Thanh toÃ¡n khi nháº­n hÃ ng"] = "Thanh toán khi nhận hàng",
        ["Chá» nháº­n"] = "Chờ nhận",
        ["Giao thÃ nh cÃ´ng"] = "Giao thành công",
        ["Giao tháº¥t báº¡i"] = "Giao thất bại"
    };

    public static async Task SeedAsync(GaRanDbContext context, bool seedDemoData)
    {
        await EnsureOrderNoteColumnAsync(context);
        await EnsurePasswordResetColumnsAsync(context);
        await EnsureReviewColumnsAsync(context);
        await EnsureFoodFeaturedColumn(context);
        await EnsureCheckoutColumns(context);
        await EnsureLoyaltyColumns(context);
        await EnsureDeliverySettings(context);
        await EnsurePayOsPaymentColumns(context);
        await EnsureWebhookPaymentTable(context);

        if (!await context.VaiTros.AnyAsync(x => x.TenVaiTro == "Admin"))
            context.VaiTros.Add(new VaiTro { TenVaiTro = "Admin" });
        if (!await context.VaiTros.AnyAsync(x => x.TenVaiTro == "NhanVien"))
            context.VaiTros.Add(new VaiTro { TenVaiTro = "NhanVien" });
        await context.SaveChangesAsync();

        if (seedDemoData) await NormalizeExistingData(context);

        var adminRoleId = await context.VaiTros
            .Where(x => x.TenVaiTro == "Admin")
            .Select(x => x.VaiTroID)
            .FirstAsync();
        var staffRoleId = await context.VaiTros
            .Where(x => x.TenVaiTro == "NhanVien")
            .Select(x => x.VaiTroID)
            .FirstAsync();
        var legacyShipperRole = await context.VaiTros.FirstOrDefaultAsync(x => x.TenVaiTro == "Shipper");
        if (legacyShipperRole != null)
        {
            var legacyShipperUsers = await context.NguoiDungs
                .Where(x => x.VaiTroID == legacyShipperRole.VaiTroID)
                .ToListAsync();
            foreach (var legacyUser in legacyShipperUsers) legacyUser.TrangThai = false;
            await context.SaveChangesAsync();
        }

        if (!seedDemoData) return;

        var admin = await context.NguoiDungs.FirstOrDefaultAsync(x => x.TenDangNhap == "admin" || x.Email == "admin@garaan.local");
        if (admin == null)
        {
            context.NguoiDungs.Add(new NguoiDung
            {
                HoTen = "Quản trị viên",
                Email = "admin@garaan.local",
                SoDienThoai = "0900000000",
                TenDangNhap = "admin",
                MatKhauHash = PasswordHasher.Hash("Admin@123"),
                VaiTroID = adminRoleId,
                TrangThai = true,
                NgayTao = DateTime.Now
            });
        }

        await EnsureUser(context, "Nhân viên bán hàng", "nhanvien@garan.vn", "0900000001", "nhanvien", "NhanVien@123", staffRoleId);
        await EnsureCategory(context, "Gà rán", "Gà rán giòn nóng");
        await EnsureCategory(context, "Burger", "Burger no bụng");
        await EnsureCategory(context, "Combo", "Combo tiết kiệm");
        await EnsureCategory(context, "Nước uống", "Đồ uống mát lạnh");
        await EnsureCategory(context, "Món phụ", "Ăn kèm hấp dẫn");
        await context.SaveChangesAsync();

        if (!await context.MonAns.AnyAsync())
        {
            var categories = await context.DanhMucs.ToDictionaryAsync(x => x.TenDanhMuc, x => x.DanhMucID);
            context.MonAns.AddRange(
                new MonAn { DanhMucID = categories["Gà rán"], TenMon = "Gà rán giòn cay", Gia = 45000, HinhAnh = "/images/foods/7a485b45bd2442b180c45f3142a4469f.jpg", MoTa = "Miếng gà rán phủ vị cay nhẹ, vỏ giòn.", TrangThai = true },
                new MonAn { DanhMucID = categories["Burger"], TenMon = "Burger gà phô mai", Gia = 59000, HinhAnh = "/images/foods/39e58e4b16d74b5d8965c93489ba637b.jpg", MoTa = "Burger gà giòn kèm phô mai béo thơm.", TrangThai = true },
                new MonAn { DanhMucID = categories["Combo"], TenMon = "Combo gia đình", Gia = 189000, HinhAnh = "/images/foods/06b2629eccbc496992ff601f007fde87.jpg", MoTa = "Combo gà rán, khoai tây và nước uống.", TrangThai = true },
                new MonAn { DanhMucID = categories["Nước uống"], TenMon = "Pepsi ly lớn", Gia = 18000, HinhAnh = "/images/foods/6fa76cb819234eb1b8e4b843f4618e24.jpg", MoTa = "Nước uống mát lạnh.", TrangThai = true },
                new MonAn { DanhMucID = categories["Món phụ"], TenMon = "Khoai tây chiên", Gia = 29000, HinhAnh = "/images/foods/e929219f68a74d3da751cc21dc904e77.jpg", MoTa = "Khoai tây chiên vàng giòn.", TrangThai = true });
            await context.SaveChangesAsync();
        }

        if (seedDemoData)
        {
            var defaultImages = new Dictionary<string, string>
            {
                ["Gà rán giòn cay"] = "/images/foods/7a485b45bd2442b180c45f3142a4469f.jpg",
                ["Burger gà phô mai"] = "/images/foods/39e58e4b16d74b5d8965c93489ba637b.jpg",
                ["Combo gia đình"] = "/images/foods/06b2629eccbc496992ff601f007fde87.jpg",
                ["Pepsi ly lớn"] = "/images/foods/6fa76cb819234eb1b8e4b843f4618e24.jpg",
                ["Khoai tây chiên"] = "/images/foods/e929219f68a74d3da751cc21dc904e77.jpg"
            };
            var foodsWithoutImages = await context.MonAns
                .Where(x => x.HinhAnh == null || x.HinhAnh == "")
                .ToListAsync();
            var updatedFoodImages = 0;
            foreach (var food in foodsWithoutImages)
            {
                if (defaultImages.TryGetValue(food.TenMon, out var imagePath))
                {
                    food.HinhAnh = imagePath;
                    updatedFoodImages++;
                }
            }
            if (updatedFoodImages > 0) await context.SaveChangesAsync();
        }

        if (!await context.MonAns.AnyAsync(x => x.NoiBat))
        {
            var fallbackFoods = await context.MonAns
                .Where(x => x.TrangThai)
                .OrderByDescending(x => x.NgayTao)
                .Take(12)
                .ToListAsync();

            foreach (var food in fallbackFoods)
            {
                food.NoiBat = true;
            }

            await context.SaveChangesAsync();
        }
    }

    public static Task EnsureOrderNoteColumnAsync(GaRanDbContext context) =>
        context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.DonHangs', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.DonHangs', N'GhiChu') IS NULL
    ALTER TABLE dbo.DonHangs ADD GhiChu NVARCHAR(500) NULL;");

    public static Task EnsurePasswordResetColumnsAsync(GaRanDbContext context) =>
        context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.KhachHangs', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.KhachHangs', N'MaXacNhan') IS NULL ALTER TABLE dbo.KhachHangs ADD MaXacNhan NVARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.KhachHangs', N'HanMaXacNhan') IS NULL ALTER TABLE dbo.KhachHangs ADD HanMaXacNhan DATETIME2 NULL;
END");

    public static Task EnsureMemberDiscountColumnAsync(GaRanDbContext context) =>
        context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.DonHangs', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.DonHangs', N'TienGiamThanhVien') IS NULL
    ALTER TABLE dbo.DonHangs ADD TienGiamThanhVien DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_DonHangs_TienGiamThanhVien DEFAULT(0);");

    private static async Task EnsureCheckoutColumns(GaRanDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('DonHangs', 'TenKhachDat') IS NULL ALTER TABLE DonHangs ADD TenKhachDat NVARCHAR(120) NULL;
IF COL_LENGTH('DonHangs', 'EmailKhachDat') IS NULL ALTER TABLE DonHangs ADD EmailKhachDat NVARCHAR(150) NULL;
IF COL_LENGTH('DonHangs', 'TinhThanh') IS NULL
    ALTER TABLE DonHangs ADD TinhThanh NVARCHAR(100) NULL;
ELSE
    ALTER TABLE DonHangs ALTER COLUMN TinhThanh NVARCHAR(100) NULL;
IF COL_LENGTH('DonHangs', 'QuanHuyen') IS NULL ALTER TABLE DonHangs ADD QuanHuyen NVARCHAR(100) NULL;
IF COL_LENGTH('DonHangs', 'PhuongXa') IS NULL ALTER TABLE DonHangs ADD PhuongXa NVARCHAR(100) NULL;
IF COL_LENGTH('DonHangs', 'KhoangCachKm') IS NULL ALTER TABLE DonHangs ADD KhoangCachKm DECIMAL(8,2) NOT NULL CONSTRAINT DF_DonHangs_KhoangCachKm DEFAULT(0);
IF COL_LENGTH('DonHangs', 'TienHang') IS NULL ALTER TABLE DonHangs ADD TienHang DECIMAL(18,2) NOT NULL CONSTRAINT DF_DonHangs_TienHang DEFAULT(0);
IF COL_LENGTH('DonHangs', 'TienThue') IS NULL ALTER TABLE DonHangs ADD TienThue DECIMAL(18,2) NOT NULL CONSTRAINT DF_DonHangs_TienThue DEFAULT(0);
IF COL_LENGTH('DonHangs', 'PhiGiaoHang') IS NULL ALTER TABLE DonHangs ADD PhiGiaoHang DECIMAL(18,2) NOT NULL CONSTRAINT DF_DonHangs_PhiGiaoHang DEFAULT(0);
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DonHangs') AND name = 'KhachHangID' AND is_nullable = 0)
    ALTER TABLE DonHangs ALTER COLUMN KhachHangID INT NULL;");
    }

    private static async Task EnsureLoyaltyColumns(GaRanDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('KhachHangs', 'DiemTichLuy') IS NULL
    ALTER TABLE KhachHangs ADD DiemTichLuy INT NOT NULL CONSTRAINT DF_KhachHangs_DiemTichLuy DEFAULT(0);
IF COL_LENGTH('DonHangs', 'TienGiamDiem') IS NULL
    ALTER TABLE DonHangs ADD TienGiamDiem DECIMAL(18,2) NOT NULL CONSTRAINT DF_DonHangs_TienGiamDiem DEFAULT(0);
IF COL_LENGTH('DonHangs', 'TienGiamThanhVien') IS NULL
    ALTER TABLE DonHangs ADD TienGiamThanhVien DECIMAL(18,2) NOT NULL CONSTRAINT DF_DonHangs_TienGiamThanhVien DEFAULT(0);
IF COL_LENGTH('DonHangs', 'DiemDaDung') IS NULL
    ALTER TABLE DonHangs ADD DiemDaDung INT NOT NULL CONSTRAINT DF_DonHangs_DiemDaDung DEFAULT(0);
IF COL_LENGTH('DonHangs', 'DiemDaCong') IS NULL
    ALTER TABLE DonHangs ADD DiemDaCong INT NOT NULL CONSTRAINT DF_DonHangs_DiemDaCong DEFAULT(0);
IF COL_LENGTH('DonHangs', 'DiemDaCongThuong') IS NULL
    ALTER TABLE DonHangs ADD DiemDaCongThuong BIT NOT NULL CONSTRAINT DF_DonHangs_DiemDaCongThuong DEFAULT(0);
IF COL_LENGTH('DonHangs', 'DiemDaHoanLai') IS NULL
    ALTER TABLE DonHangs ADD DiemDaHoanLai BIT NOT NULL CONSTRAINT DF_DonHangs_DiemDaHoanLai DEFAULT(0);");
    }

    public static Task EnsureReviewColumnsAsync(GaRanDbContext context) =>
        context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.DanhGias', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.DanhGias', N'LoaiDanhGia') IS NULL
        ALTER TABLE dbo.DanhGias ADD LoaiDanhGia NVARCHAR(20) NOT NULL CONSTRAINT DF_DanhGias_LoaiDanhGia DEFAULT(N'MonAn') WITH VALUES;
    IF COL_LENGTH(N'dbo.DanhGias', N'MonAnID') IS NOT NULL
        ALTER TABLE dbo.DanhGias ALTER COLUMN MonAnID INT NULL;
END");

    private static async Task EnsureDeliverySettings(GaRanDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('CauHinhGiaoHangs', 'U') IS NULL
BEGIN
    CREATE TABLE CauHinhGiaoHangs (
        CauHinhGiaoHangID INT NOT NULL PRIMARY KEY,
        MienPhiDenKm DECIMAL(8,2) NOT NULL,
        ToiDaKm DECIMAL(8,2) NOT NULL,
        UocTinhCungPhuongKm DECIMAL(8,2) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_UocTinhCungPhuongKm DEFAULT(2),
        UocTinhKhacPhuongKm DECIMAL(8,2) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_UocTinhKhacPhuongKm DEFAULT(4.5),
        Phi3Den4Km DECIMAL(18,2) NOT NULL,
        Phi4Den5Km DECIMAL(18,2) NOT NULL,
        TyLeThue DECIMAL(5,4) NOT NULL,
        DiaChiCuaHang NVARCHAR(250) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_DiaChiCuaHang DEFAULT(N'KFC, 183-185 Lê Đức Thọ, Phường Gò Vấp, Thành phố Hồ Chí Minh, Việt Nam'),
        TienToNoiDung NVARCHAR(20) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_TienToNoiDung DEFAULT(N'WOCORDER'),
        BatCOD BIT NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_BatCOD DEFAULT(1),
        BatVietQR BIT NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_BatVietQR DEFAULT(1)
    );
END");

        await context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('CauHinhGiaoHangs', 'TenCuaHang') IS NULL ALTER TABLE CauHinhGiaoHangs ADD TenCuaHang NVARCHAR(150) NULL;
IF COL_LENGTH('CauHinhGiaoHangs', 'MienPhiDenKm') IS NULL ALTER TABLE CauHinhGiaoHangs ADD MienPhiDenKm DECIMAL(8,2) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_MienPhiDenKm DEFAULT(3);
IF COL_LENGTH('CauHinhGiaoHangs', 'ToiDaKm') IS NULL ALTER TABLE CauHinhGiaoHangs ADD ToiDaKm DECIMAL(8,2) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_ToiDaKm DEFAULT(5);
IF COL_LENGTH('CauHinhGiaoHangs', 'UocTinhCungPhuongKm') IS NULL ALTER TABLE CauHinhGiaoHangs ADD UocTinhCungPhuongKm DECIMAL(8,2) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_UocTinhCungPhuongKm DEFAULT(2);
IF COL_LENGTH('CauHinhGiaoHangs', 'UocTinhKhacPhuongKm') IS NULL ALTER TABLE CauHinhGiaoHangs ADD UocTinhKhacPhuongKm DECIMAL(8,2) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_UocTinhKhacPhuongKm DEFAULT(4.5);
IF COL_LENGTH('CauHinhGiaoHangs', 'Phi3Den4Km') IS NULL ALTER TABLE CauHinhGiaoHangs ADD Phi3Den4Km DECIMAL(18,2) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_Phi3Den4Km DEFAULT(12000);
IF COL_LENGTH('CauHinhGiaoHangs', 'Phi4Den5Km') IS NULL ALTER TABLE CauHinhGiaoHangs ADD Phi4Den5Km DECIMAL(18,2) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_Phi4Den5Km DEFAULT(22000);
IF COL_LENGTH('CauHinhGiaoHangs', 'TyLeThue') IS NULL ALTER TABLE CauHinhGiaoHangs ADD TyLeThue DECIMAL(5,4) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_TyLeThue DEFAULT(0.10);
IF COL_LENGTH('CauHinhGiaoHangs', 'DienThoaiCuaHang') IS NULL ALTER TABLE CauHinhGiaoHangs ADD DienThoaiCuaHang NVARCHAR(20) NULL;
IF COL_LENGTH('CauHinhGiaoHangs', 'EmailCuaHang') IS NULL ALTER TABLE CauHinhGiaoHangs ADD EmailCuaHang NVARCHAR(150) NULL;
IF COL_LENGTH('CauHinhGiaoHangs', 'NganHangBin') IS NULL ALTER TABLE CauHinhGiaoHangs ADD NganHangBin NVARCHAR(10) NULL;
IF COL_LENGTH('CauHinhGiaoHangs', 'TenNganHang') IS NULL ALTER TABLE CauHinhGiaoHangs ADD TenNganHang NVARCHAR(80) NULL;
IF COL_LENGTH('CauHinhGiaoHangs', 'SoTaiKhoan') IS NULL ALTER TABLE CauHinhGiaoHangs ADD SoTaiKhoan NVARCHAR(30) NULL;
IF COL_LENGTH('CauHinhGiaoHangs', 'ChuTaiKhoan') IS NULL ALTER TABLE CauHinhGiaoHangs ADD ChuTaiKhoan NVARCHAR(100) NULL;
IF COL_LENGTH('CauHinhGiaoHangs', 'DiaChiCuaHang') IS NULL ALTER TABLE CauHinhGiaoHangs ADD DiaChiCuaHang NVARCHAR(250) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_DiaChiCuaHang DEFAULT(N'KFC, 183-185 Lê Đức Thọ, Phường Gò Vấp, Thành phố Hồ Chí Minh, Việt Nam');
IF COL_LENGTH('CauHinhGiaoHangs', 'TienToNoiDung') IS NULL ALTER TABLE CauHinhGiaoHangs ADD TienToNoiDung NVARCHAR(20) NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_TienToNoiDung DEFAULT(N'WOCORDER');
IF COL_LENGTH('CauHinhGiaoHangs', 'BatCOD') IS NULL ALTER TABLE CauHinhGiaoHangs ADD BatCOD BIT NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_BatCOD DEFAULT(1);
IF COL_LENGTH('CauHinhGiaoHangs', 'BatVietQR') IS NULL ALTER TABLE CauHinhGiaoHangs ADD BatVietQR BIT NOT NULL CONSTRAINT DF_CauHinhGiaoHangs_BatVietQR DEFAULT(1)");
        await context.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT 1 FROM CauHinhGiaoHangs WHERE CauHinhGiaoHangID = 1)
    INSERT INTO CauHinhGiaoHangs (CauHinhGiaoHangID, MienPhiDenKm, ToiDaKm, Phi3Den4Km, Phi4Den5Km, TyLeThue)
    VALUES (1, 3, 5, 12000, 22000, 0.10)");
        await context.Database.ExecuteSqlRawAsync(@"
UPDATE CauHinhGiaoHangs SET TienToNoiDung = N'WOCORDER'
WHERE CauHinhGiaoHangID = 1 AND TienToNoiDung = N'GARAN';
IF OBJECT_ID('ThanhToans', 'U') IS NOT NULL AND OBJECT_ID('DonHangs', 'U') IS NOT NULL
BEGIN
    UPDATE p
    SET p.MaGiaoDich = CONCAT(N'WOCORDER', CONVERT(nvarchar(20), o.DonHangID))
    FROM ThanhToans AS p
    INNER JOIN DonHangs AS o ON o.DonHangID = p.DonHangID
    WHERE p.MaGiaoDich LIKE N'GARANDH%'
      AND p.PhuongThucThanhToan IN (N'QR', N'Bank')
      AND p.TrangThaiThanhToan <> N'Đã thanh toán';
END");
    }

    private static async Task EnsureWebhookPaymentTable(GaRanDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.GiaoDichWebhookThanhToans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GiaoDichWebhookThanhToans (
        GiaoDichWebhookThanhToanID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_GiaoDichWebhookThanhToans PRIMARY KEY,
        NhaCungCap NVARCHAR(30) NOT NULL,
        MaGiaoDichNhaCungCap NVARCHAR(100) NOT NULL,
        MaThamChieuNganHang NVARCHAR(100) NULL,
        MaThanhToan NVARCHAR(100) NULL,
        NoiDungChuyenKhoan NVARCHAR(500) NULL,
        SoTaiKhoanNhan NVARCHAR(30) NULL,
        LoaiGiaoDich NVARCHAR(30) NULL,
        SoTien DECIMAL(18,2) NOT NULL,
        NhanLuc DATETIME2 NOT NULL,
        ThanhToanID INT NULL,
        DaCongVaoThanhToan BIT NOT NULL,
        CONSTRAINT FK_GiaoDichWebhookThanhToans_ThanhToans
            FOREIGN KEY (ThanhToanID) REFERENCES dbo.ThanhToans(ThanhToanID)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.GiaoDichWebhookThanhToans')
      AND name = N'UX_GiaoDichWebhookThanhToans_NhaCungCap_MaGiaoDich'
)
    CREATE UNIQUE INDEX UX_GiaoDichWebhookThanhToans_NhaCungCap_MaGiaoDich
        ON dbo.GiaoDichWebhookThanhToans(NhaCungCap, MaGiaoDichNhaCungCap);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.GiaoDichWebhookThanhToans')
      AND name = N'IX_GiaoDichWebhookThanhToans_ThanhToanID'
)
    CREATE INDEX IX_GiaoDichWebhookThanhToans_ThanhToanID
        ON dbo.GiaoDichWebhookThanhToans(ThanhToanID);");
    }

    private static async Task EnsurePayOsPaymentColumns(GaRanDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.ThanhToans', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ThanhToans', N'PayOsPaymentLinkId') IS NULL
        ALTER TABLE dbo.ThanhToans ADD PayOsPaymentLinkId NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.ThanhToans', N'PayOsCheckoutUrl') IS NULL
        ALTER TABLE dbo.ThanhToans ADD PayOsCheckoutUrl NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'dbo.ThanhToans', N'PayOsOrderCode') IS NULL
        ALTER TABLE dbo.ThanhToans ADD PayOsOrderCode INT NULL;
END");

        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.ThanhToans', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ThanhToans', N'PayOsOrderCode') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.ThanhToans')
         AND name = N'IX_ThanhToans_PayOsOrderCode'
   )
    CREATE UNIQUE INDEX IX_ThanhToans_PayOsOrderCode
        ON dbo.ThanhToans(PayOsOrderCode)
        WHERE PayOsOrderCode IS NOT NULL;");
    }

    private static async Task EnsureFoodFeaturedColumn(GaRanDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('MonAns', 'NoiBat') IS NULL
BEGIN
    ALTER TABLE MonAns ADD NoiBat bit NOT NULL CONSTRAINT DF_MonAns_NoiBat DEFAULT(0)
END");
    }

    private static async Task NormalizeExistingData(GaRanDbContext context)
    {
        foreach (var user in await context.NguoiDungs.ToListAsync())
        {
            user.HoTen = FixRequiredText(user.HoTen);
        }

        foreach (var customer in await context.KhachHangs.ToListAsync())
        {
            customer.HoTen = FixRequiredText(customer.HoTen);
            customer.DiaChi = FixRequiredText(customer.DiaChi);
        }

        foreach (var category in await context.DanhMucs.ToListAsync())
        {
            category.TenDanhMuc = FixRequiredText(category.TenDanhMuc);
            category.MoTa = FixText(category.MoTa);
        }

        foreach (var food in await context.MonAns.ToListAsync())
        {
            food.TenMon = FixRequiredText(food.TenMon);
            food.MoTa = FixText(food.MoTa);
        }

        foreach (var order in await context.DonHangs.ToListAsync())
        {
            order.TrangThaiDonHang = FixRequiredText(order.TrangThaiDonHang);
            order.DiaChiGiaoHang = FixRequiredText(order.DiaChiGiaoHang);
        }

        foreach (var payment in await context.ThanhToans.ToListAsync())
        {
            payment.TrangThaiThanhToan = FixRequiredText(payment.TrangThaiThanhToan);
            payment.PhuongThucThanhToan = FixRequiredText(payment.PhuongThucThanhToan);
        }

        foreach (var delivery in await context.GiaoHangs.ToListAsync())
        {
            delivery.TrangThaiGiaoHang = FixRequiredText(delivery.TrangThaiGiaoHang);
        }

        await context.SaveChangesAsync();
    }

    private static string? FixText(string? value)
    {
        if (value == null) return null;
        return TextFixes.TryGetValue(value, out var fixedValue) ? fixedValue : value;
    }

    private static string FixRequiredText(string value)
    {
        return FixText(value) ?? value;
    }

    private static async Task EnsureCategory(GaRanDbContext context, string name, string description)
    {
        var category = await context.DanhMucs.FirstOrDefaultAsync(x => x.TenDanhMuc == name);
        if (category == null)
        {
            context.DanhMucs.Add(new DanhMuc { TenDanhMuc = name, MoTa = description, TrangThai = true });
        }
        else
        {
            category.MoTa = description;
            category.TrangThai = true;
        }
    }

    private static async Task EnsureUser(GaRanDbContext context, string fullName, string email, string phone, string username, string password, int roleId)
    {
        var user = await context.NguoiDungs.FirstOrDefaultAsync(x => x.TenDangNhap == username || x.Email == email);
        if (user == null)
        {
            context.NguoiDungs.Add(new NguoiDung
            {
                HoTen = fullName,
                Email = email,
                SoDienThoai = phone,
                TenDangNhap = username,
                MatKhauHash = PasswordHasher.Hash(password),
                VaiTroID = roleId,
                TrangThai = true,
                NgayTao = DateTime.Now
            });
        }
    }
}
