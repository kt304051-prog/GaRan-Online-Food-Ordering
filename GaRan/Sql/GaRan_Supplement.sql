IF DB_ID(N'GaRan') IS NULL
CREATE DATABASE [GaRan];
GO

USE [GaRan];
GO

IF OBJECT_ID('VaiTros') IS NULL
CREATE TABLE VaiTros (
    VaiTroID INT IDENTITY(1,1) PRIMARY KEY,
    TenVaiTro NVARCHAR(50) NOT NULL UNIQUE
);

IF OBJECT_ID('NguoiDungs') IS NULL
CREATE TABLE NguoiDungs (
    NguoiDungID INT IDENTITY(1,1) PRIMARY KEY,
    HoTen NVARCHAR(120) NOT NULL,
    Email NVARCHAR(120) NULL,
    SoDienThoai NVARCHAR(20) NULL,
    TenDangNhap NVARCHAR(80) NOT NULL UNIQUE,
    MatKhauHash NVARCHAR(MAX) NOT NULL,
    VaiTroID INT NOT NULL,
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Hoạt động',
    NgayTao DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_NguoiDungs_VaiTros FOREIGN KEY (VaiTroID) REFERENCES VaiTros(VaiTroID)
);

IF OBJECT_ID('KhachHangs') IS NULL
CREATE TABLE KhachHangs (
    KhachHangID INT IDENTITY(1,1) PRIMARY KEY,
    HoTen NVARCHAR(120) NOT NULL,
    Email NVARCHAR(120) NOT NULL UNIQUE,
    SoDienThoai NVARCHAR(20) NULL,
    MatKhauHash NVARCHAR(MAX) NOT NULL,
    DiaChi NVARCHAR(300) NULL,
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Hoạt động',
    MaXacNhan NVARCHAR(20) NULL,
    HanMaXacNhan DATETIME2 NULL,
    NgayTao DATETIME2 NOT NULL DEFAULT GETDATE()
);

IF OBJECT_ID('DanhMucs') IS NULL
CREATE TABLE DanhMucs (
    DanhMucID INT IDENTITY(1,1) PRIMARY KEY,
    TenDanhMuc NVARCHAR(100) NOT NULL,
    MoTa NVARCHAR(500) NULL,
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Hiển thị'
);

IF OBJECT_ID('MonAns') IS NULL
CREATE TABLE MonAns (
    MonAnID INT IDENTITY(1,1) PRIMARY KEY,
    DanhMucID INT NOT NULL,
    TenMon NVARCHAR(160) NOT NULL,
    Gia DECIMAL(18,2) NOT NULL,
    HinhAnh NVARCHAR(300) NULL,
    MoTa NVARCHAR(1000) NULL,
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Đang bán',
    NgayTao DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_MonAns_DanhMucs FOREIGN KEY (DanhMucID) REFERENCES DanhMucs(DanhMucID)
);

IF OBJECT_ID('KhuyenMais') IS NULL
CREATE TABLE KhuyenMais (
    KhuyenMaiID INT IDENTITY(1,1) PRIMARY KEY,
    MaKhuyenMai NVARCHAR(40) NOT NULL UNIQUE,
    TenKhuyenMai NVARCHAR(160) NOT NULL,
    PhanTramGiam INT NOT NULL,
    NgayBatDau DATETIME2 NOT NULL,
    NgayKetThuc DATETIME2 NOT NULL,
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Đang áp dụng'
);

IF OBJECT_ID('DonHangs') IS NULL
CREATE TABLE DonHangs (
    DonHangID INT IDENTITY(1,1) PRIMARY KEY,
    KhachHangID INT NULL,
    NhanVienID INT NULL,
    KhuyenMaiID INT NULL,
    NgayDat DATETIME2 NOT NULL DEFAULT GETDATE(),
    DiaChiGiaoHang NVARCHAR(300) NOT NULL,
    SoDienThoaiNhan NVARCHAR(20) NOT NULL,
    TongTien DECIMAL(18,2) NOT NULL,
    TienGiamThanhVien DECIMAL(18,2) NOT NULL CONSTRAINT DF_DonHangs_TienGiamThanhVien DEFAULT(0),
    TrangThaiDonHang NVARCHAR(50) NOT NULL DEFAULT N'Chờ xác nhận',
    CONSTRAINT FK_DonHangs_KhachHangs FOREIGN KEY (KhachHangID) REFERENCES KhachHangs(KhachHangID),
    CONSTRAINT FK_DonHangs_NhanViens FOREIGN KEY (NhanVienID) REFERENCES NguoiDungs(NguoiDungID),
    CONSTRAINT FK_DonHangs_KhuyenMais FOREIGN KEY (KhuyenMaiID) REFERENCES KhuyenMais(KhuyenMaiID)
);

IF COL_LENGTH('DonHangs', 'TienGiamThanhVien') IS NULL
    ALTER TABLE DonHangs ADD TienGiamThanhVien DECIMAL(18,2) NOT NULL CONSTRAINT DF_DonHangs_TienGiamThanhVien DEFAULT(0);

IF OBJECT_ID('ChiTietDonHangs') IS NULL
CREATE TABLE ChiTietDonHangs (
    ChiTietID INT IDENTITY(1,1) PRIMARY KEY,
    DonHangID INT NOT NULL,
    MonAnID INT NOT NULL,
    SoLuong INT NOT NULL,
    DonGia DECIMAL(18,2) NOT NULL,
    ThanhTien DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_ChiTietDonHangs_DonHangs FOREIGN KEY (DonHangID) REFERENCES DonHangs(DonHangID),
    CONSTRAINT FK_ChiTietDonHangs_MonAns FOREIGN KEY (MonAnID) REFERENCES MonAns(MonAnID)
);

IF OBJECT_ID('ThanhToans') IS NULL
CREATE TABLE ThanhToans (
    ThanhToanID INT IDENTITY(1,1) PRIMARY KEY,
    DonHangID INT NOT NULL,
    PhuongThucThanhToan NVARCHAR(80) NOT NULL,
    SoTien DECIMAL(18,2) NOT NULL,
    TrangThaiThanhToan NVARCHAR(50) NOT NULL,
    NgayThanhToan DATETIME2 NULL,
    MaGiaoDich NVARCHAR(120) NULL,
    CONSTRAINT FK_ThanhToans_DonHangs FOREIGN KEY (DonHangID) REFERENCES DonHangs(DonHangID)
);

IF OBJECT_ID('GiaoHangs') IS NULL
CREATE TABLE GiaoHangs (
    GiaoHangID INT IDENTITY(1,1) PRIMARY KEY,
    DonHangID INT NOT NULL,
    NgayNhanDon DATETIME2 NULL,
    NgayGiaoHang DATETIME2 NULL,
    TrangThaiGiaoHang NVARCHAR(50) NOT NULL DEFAULT N'Chờ nhận',
    CONSTRAINT FK_GiaoHangs_DonHangs FOREIGN KEY (DonHangID) REFERENCES DonHangs(DonHangID)
);

IF OBJECT_ID('HoaDons') IS NULL
CREATE TABLE HoaDons (
    HoaDonID INT IDENTITY(1,1) PRIMARY KEY,
    DonHangID INT NOT NULL,
    NguoiLapID INT NULL,
    NgayLap DATETIME2 NOT NULL DEFAULT GETDATE(),
    TongTien DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_HoaDons_DonHangs FOREIGN KEY (DonHangID) REFERENCES DonHangs(DonHangID),
    CONSTRAINT FK_HoaDons_NguoiDungs FOREIGN KEY (NguoiLapID) REFERENCES NguoiDungs(NguoiDungID)
);

IF OBJECT_ID('DanhGias') IS NULL
CREATE TABLE DanhGias (
    DanhGiaID INT IDENTITY(1,1) PRIMARY KEY,
    KhachHangID INT NOT NULL,
    MonAnID INT NOT NULL,
    DonHangID INT NULL,
    SoSao INT NOT NULL,
    NoiDung NVARCHAR(1000) NULL,
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Chờ duyệt',
    NgayDanhGia DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_DanhGias_KhachHangs FOREIGN KEY (KhachHangID) REFERENCES KhachHangs(KhachHangID),
    CONSTRAINT FK_DanhGias_MonAns FOREIGN KEY (MonAnID) REFERENCES MonAns(MonAnID),
    CONSTRAINT FK_DanhGias_DonHangs FOREIGN KEY (DonHangID) REFERENCES DonHangs(DonHangID)
);

IF COL_LENGTH('dbo.DanhGias', 'LoaiDanhGia') IS NULL
    ALTER TABLE dbo.DanhGias ADD LoaiDanhGia NVARCHAR(20) NOT NULL CONSTRAINT DF_DanhGias_LoaiDanhGia DEFAULT(N'MonAn') WITH VALUES;
IF COL_LENGTH('dbo.DanhGias', 'MonAnID') IS NOT NULL
    ALTER TABLE dbo.DanhGias ALTER COLUMN MonAnID INT NULL;

IF NOT EXISTS (SELECT 1 FROM VaiTros WHERE TenVaiTro = N'Admin') INSERT INTO VaiTros(TenVaiTro) VALUES (N'Admin');
IF NOT EXISTS (SELECT 1 FROM VaiTros WHERE TenVaiTro = N'NhanVien') INSERT INTO VaiTros(TenVaiTro) VALUES (N'NhanVien');

IF NOT EXISTS (SELECT 1 FROM DanhMucs)
INSERT INTO DanhMucs(TenDanhMuc, MoTa, TrangThai) VALUES
(N'Gà rán', N'Gà rán giòn nóng', 1),
(N'Burger', N'Burger no bụng', 1),
(N'Combo', N'Combo tiết kiệm', 1),
(N'Nước uống', N'Đồ uống mát lạnh', 1),
(N'Món phụ', N'Ăn kèm hấp dẫn', 1);

IF NOT EXISTS (SELECT 1 FROM NguoiDungs WHERE TenDangNhap = N'admin')
INSERT INTO NguoiDungs(HoTen, Email, SoDienThoai, TenDangNhap, MatKhauHash, VaiTroID, TrangThai)
SELECT N'Quản trị viên', N'admin@garaan.local', N'0900000000', N'admin',
       N'E86F78A8A3CAF0B60D8E74E5942AA6D86DC150CD3C03338AEF25B7D2D7E3ACC7',
       VaiTroID, 1
FROM VaiTros WHERE TenVaiTro = N'Admin';

IF NOT EXISTS (SELECT 1 FROM MonAns)
INSERT INTO MonAns(DanhMucID, TenMon, Gia, HinhAnh, MoTa, TrangThai)
SELECT DanhMucID, N'Gà rán giòn cay', 45000, N'/images/foods/7a485b45bd2442b180c45f3142a4469f.jpg', N'Miếng gà rán phủ vị cay nhẹ, vỏ giòn.', 1 FROM DanhMucs WHERE TenDanhMuc = N'Gà rán'
UNION ALL SELECT DanhMucID, N'Burger gà phô mai', 59000, N'/images/foods/39e58e4b16d74b5d8965c93489ba637b.jpg', N'Burger gà giòn kèm phô mai béo thơm.', 1 FROM DanhMucs WHERE TenDanhMuc = N'Burger'
UNION ALL SELECT DanhMucID, N'Combo gia đình', 189000, N'/images/foods/06b2629eccbc496992ff601f007fde87.jpg', N'Combo gà rán, khoai tây và nước uống.', 1 FROM DanhMucs WHERE TenDanhMuc = N'Combo'
UNION ALL SELECT DanhMucID, N'Pepsi ly lớn', 18000, N'/images/foods/6fa76cb819234eb1b8e4b843f4618e24.jpg', N'Nước uống mát lạnh.', 1 FROM DanhMucs WHERE TenDanhMuc = N'Nước uống'
UNION ALL SELECT DanhMucID, N'Khoai tây chiên', 29000, N'/images/foods/e929219f68a74d3da751cc21dc904e77.jpg', N'Khoai tây chiên vàng giòn.', 1 FROM DanhMucs WHERE TenDanhMuc = N'Món phụ';
GO
