USE GaRan;
GO

IF NOT EXISTS (SELECT 1 FROM VaiTros WHERE TenVaiTro = N'Admin')
    INSERT INTO VaiTros(TenVaiTro) VALUES (N'Admin');

IF NOT EXISTS (SELECT 1 FROM VaiTros WHERE TenVaiTro = N'NhanVien')
    INSERT INTO VaiTros(TenVaiTro) VALUES (N'NhanVien');

GO

DECLARE @AdminRoleID INT = (SELECT TOP 1 VaiTroID FROM VaiTros WHERE TenVaiTro = N'Admin');
DECLARE @AdminHash NVARCHAR(MAX) = N'E86F78A8A3CAF0B60D8E74E5942AA6D86DC150CD3C03338AEF25B7D2D7E3ACC7';

IF EXISTS (SELECT 1 FROM NguoiDungs WHERE TenDangNhap = 'admin')
BEGIN
    UPDATE NguoiDungs
    SET HoTen = N'Quản trị viên',
        Email = N'admin@garaan.local',
        SoDienThoai = '0900000000',
        MatKhauHash = @AdminHash,
        VaiTroID = @AdminRoleID,
        TrangThai = N'Hoạt động'
    WHERE TenDangNhap = 'admin';
END
ELSE
BEGIN
    INSERT INTO NguoiDungs(HoTen, Email, SoDienThoai, TenDangNhap, MatKhauHash, VaiTroID, TrangThai)
    VALUES (N'Quản trị viên', N'admin@garaan.local', '0900000000', 'admin', @AdminHash, @AdminRoleID, N'Hoạt động');
END
GO

DECLARE @CustomerHash NVARCHAR(MAX) = N'98EC654A8DF28F8F0F8F02220483D46916B85017DE0B74D8EC755C28CB8539A8';

IF EXISTS (SELECT 1 FROM KhachHangs WHERE Email = N'khach@garaan.local')
BEGIN
    UPDATE KhachHangs
    SET HoTen = N'Khách hàng mẫu',
        SoDienThoai = '0911111111',
        DiaChi = N'123 Đường Gà Rán',
        MatKhauHash = @CustomerHash,
        TrangThai = N'Hoạt động'
    WHERE Email = N'khach@garaan.local';
END
ELSE
BEGIN
    INSERT INTO KhachHangs(HoTen, Email, SoDienThoai, MatKhauHash, DiaChi, TrangThai)
    VALUES (N'Khách hàng mẫu', N'khach@garaan.local', '0911111111', @CustomerHash, N'123 Đường Gà Rán', N'Hoạt động');
END
GO

IF NOT EXISTS (SELECT 1 FROM DanhMucs)
BEGIN
    INSERT INTO DanhMucs(TenDanhMuc, MoTa, TrangThai) VALUES
    (N'Gà rán', N'Gà rán giòn nóng', N'Hiển thị'),
    (N'Burger', N'Burger no bụng', N'Hiển thị'),
    (N'Combo', N'Combo tiết kiệm', N'Hiển thị'),
    (N'Nước uống', N'Đồ uống mát lạnh', N'Hiển thị'),
    (N'Món phụ', N'Ăn kèm hấp dẫn', N'Hiển thị');
END
GO

IF NOT EXISTS (SELECT 1 FROM MonAns)
BEGIN
    INSERT INTO MonAns(DanhMucID, TenMon, Gia, HinhAnh, MoTa, TrangThai)
    SELECT DanhMucID, N'Gà rán giòn cay', 45000, NULL, N'Miếng gà rán phủ vị cay nhẹ, vỏ giòn.', N'Đang bán'
    FROM DanhMucs WHERE TenDanhMuc = N'Gà rán'
    UNION ALL
    SELECT DanhMucID, N'Burger gà phô mai', 59000, NULL, N'Burger gà giòn kèm phô mai béo thơm.', N'Đang bán'
    FROM DanhMucs WHERE TenDanhMuc = N'Burger'
    UNION ALL
    SELECT DanhMucID, N'Combo gia đình', 189000, NULL, N'Combo gà rán, khoai tây và nước uống.', N'Đang bán'
    FROM DanhMucs WHERE TenDanhMuc = N'Combo';
END
GO
