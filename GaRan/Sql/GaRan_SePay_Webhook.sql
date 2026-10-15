USE [GaRan];
GO

IF OBJECT_ID(N'dbo.GiaoDichWebhookThanhToans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GiaoDichWebhookThanhToans (
        GiaoDichWebhookThanhToanID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GiaoDichWebhookThanhToans PRIMARY KEY,
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
        CONSTRAINT FK_GiaoDichWebhookThanhToans_ThanhToans FOREIGN KEY (ThanhToanID)
            REFERENCES dbo.ThanhToans(ThanhToanID)
    );

    CREATE UNIQUE INDEX UX_GiaoDichWebhookThanhToans_NhaCungCap_MaGiaoDich
        ON dbo.GiaoDichWebhookThanhToans(NhaCungCap, MaGiaoDichNhaCungCap);
    CREATE INDEX IX_GiaoDichWebhookThanhToans_ThanhToanID
        ON dbo.GiaoDichWebhookThanhToans(ThanhToanID);
END;
GO
