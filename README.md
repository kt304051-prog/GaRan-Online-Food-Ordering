# GaRan — Đặt món gà rán trực tuyến

Ứng dụng ASP.NET Core MVC hỗ trợ khách hàng đặt món và nhân viên/quản trị viên vận hành đơn hàng.

**Mã nguồn GitHub:** https://github.com/kt304051-prog/GaRan-Online-Food-Ordering

## Công nghệ

- .NET 8 / ASP.NET Core MVC
- Entity Framework Core 8 và SQL Server
- Razor Views, HTML, CSS, JavaScript
- Cookie Authentication, Session, MailKit
- Tích hợp PayOS và webhook thanh toán khi được cấu hình

## Chạy trên máy cá nhân

1. Cài .NET 8 SDK và SQL Server.
2. Sao chép `GaRan/appsettings.example.json` thành `GaRan/appsettings.json`, rồi cập nhật `ConnectionStrings:DefaultConnection` và thông tin cửa hàng.
3. Khởi tạo database theo hướng dẫn/script trong `GaRan/Sql/` và tài liệu `GaRan/README_GaRan.md`.
4. Mở terminal tại thư mục `GaRan` và chạy:

   ```powershell
   dotnet restore
   dotnet run
   ```

   Mặc định website khách hàng dùng `http://localhost:5181`.

## Chạy cổng khách hàng và cổng nội bộ

Trong thư mục `GaRan`, chạy mỗi lệnh ở một terminal riêng:

```powershell
dotnet run --launch-profile customer-lan
dotnet run --launch-profile internal-lan
```

   Cổng khách hàng là `http://localhost:5181`; cổng Admin/nhân viên là `http://localhost:5182`.

## Cấu hình bảo mật

Không commit `appsettings.json`, mật khẩu, API key hoặc secret. Cấu hình email/PayOS/SePay qua .NET User Secrets hoặc biến môi trường. Các script cấu hình trong thư mục gốc nhập giá trị bí mật mà không in chúng ra.

Tài khoản mẫu trong Development chỉ dùng để demo cục bộ. Hãy đổi trước khi triển khai và không sử dụng dữ liệu/mật khẩu thật trong môi trường thử nghiệm.

## Ghi chú

Kho mã nguồn này phục vụ học tập và trình diễn. Tính năng thanh toán, email và phạm vi giao hàng phụ thuộc cấu hình dịch vụ thực tế.
