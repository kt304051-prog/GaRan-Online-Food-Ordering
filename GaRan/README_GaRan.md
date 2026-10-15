# Website Quản Lý Quán Gà Rán

> **Cách chạy hiện tại:** xem [README_HaiCong.md](README_HaiCong.md). Website khách chạy trên cổng 5181; cổng nhân viên/Admin chạy riêng trên cổng 5182. Cả hai dùng chung database. Các đường dẫn `/Admin/...` bên dưới chỉ khả dụng ở cổng nội bộ.

## Công nghệ

- ASP.NET Core MVC .NET 8
- Entity Framework Core
- SQL Server
- Bootstrap 5
- LINQ, Session, Cookie Authentication, Authorization

## Cấu hình database

Mở `appsettings.json` và chỉnh chuỗi kết nối nếu SQL Server của bạn khác mặc định:

```json
"DefaultConnection": "Server=.;Database=GaRan;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true"
```

Với SQL Server trong hình của bạn, nên dùng:

```json
"DefaultConnection": "Server=.;Database=GaRan;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true"
```

Nếu vẫn gặp lỗi `Failed to generate SSPI context`, mở SSMS bằng đúng Windows user hiện tại, kết nối vào `LAPTOP-28IOAQSG`, rồi thử đổi `Server=.` thành:

```json
"Server=LAPTOP-28IOAQSG;Database=GaRan;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true"
```

Nếu database `GaRan` đã có sẵn đúng bảng, chỉ cần chạy script seed/bổ sung dữ liệu. Nếu thiếu bảng hoặc cần dữ liệu mẫu, chạy script:

```sql
Sql/GaRan_Supplement.sql
```

Tài khoản admin mặc định:

- Tên đăng nhập: `admin`
- Mật khẩu: `Admin@123`

Tài khoản mẫu chỉ dùng khi khởi tạo môi trường phát triển lần đầu. Ứng dụng không ghi đè mật khẩu hay mở khóa tài khoản nội bộ khi khởi động lại. Hãy đổi mật khẩu mặc định trước khi đưa website lên môi trường thật.

## Chạy dự án

Từ thư mục `GaRan`:

```powershell
dotnet restore
dotnet build
dotnet run
```

Sau đó mở địa chỉ được in ra trong terminal, thường là:

```text
https://localhost:xxxx
```

## Chức năng chính

- Khách vãng lai: xem trang chủ, danh mục, món ăn, tìm kiếm, chi tiết món, đăng ký, đăng nhập.
- Khách hàng: hồ sơ, đổi mật khẩu, giỏ hàng session, đặt hàng, nhập mã giảm giá, thanh toán COD/Online mô phỏng, theo dõi đơn, lịch sử đơn, gửi đánh giá chờ duyệt.
- Nhân viên: quản lý đơn hàng, cập nhật trạng thái giao, xác nhận thanh toán và in hóa đơn.
- Admin: dashboard, quản lý danh mục, món ăn, upload ảnh, khuyến mãi, tài khoản, đơn hàng, giao hàng, hóa đơn, đánh giá.

## Các màn quản trị

- `/Admin/Dashboard`
- `/Admin/Category`
- `/Admin/Food`
- `/Admin/Promotion`
- `/Admin/User`
- `/Admin/User/Customers`
- `/Admin/OrderAdmin`
- `/Admin/Delivery`
- `/Admin/Invoice`
- `/Admin/ReviewAdmin`

## Ghi chú

Dashboard dùng Chart.js qua CDN. Nếu máy không có internet, biểu đồ có thể không hiển thị nhưng phần thống kê số liệu vẫn hoạt động.

## Cấu hình đặt hàng, giao hàng và thanh toán

Admin có thể chỉnh phạm vi tối đa, ngưỡng miễn phí, hai mức phí, thuế, thông tin cửa hàng/ngân hàng và bật/tắt phương thức thanh toán tại **Admin → Cấu hình phí giao hàng**. Cấu hình được lưu trong bảng `CauHinhGiaoHangs`:

- `Delivery:StoreAddress`: địa chỉ cửa hàng dự phòng nếu chưa có địa chỉ trong cấu hình Admin.
- `Store` và `Bank` trong `appsettings.json` là giá trị dự phòng; thông tin vận hành có thể nhập trực tiếp ở trang cấu hình của Admin.

Khoảng cách giao hàng được ước tính theo khu vực phường/xã, không gọi API bản đồ và không gửi địa chỉ khách tới nhà cung cấp bên ngoài. Admin cấu hình riêng khoảng cách ước tính cùng phường/khác phường, phạm vi tối đa, ngưỡng miễn phí và hai mức phí; các ước tính có thể chỉnh cho phù hợp với khu vực vận hành.

### Tự động xác nhận QR

- Đặt webhook secret SePay bằng biến môi trường `SePay__WebhookSecret`. Tạo webhook SePay loại tiền vào, xác thực HMAC-SHA256, URL `https://<ten-mien>/webhooks/sepay`, và bật xác thực mã chuyển khoản theo tiền tố đang cấu hình ở Admin.
- Bảng và index lưu giao dịch webhook được ứng dụng tự kiểm tra/tạo khi khởi động. Với tài khoản SQL production không có quyền DDL, chạy `Sql/GaRan_SePay_Webhook.sql` khi triển khai. Giao dịch có chữ ký hợp lệ nhưng không khớp tài khoản, mã đơn hoặc đơn hàng bị hủy sẽ được lưu để đối soát và không tự xác nhận thanh toán.
- Sau khi triển khai URL HTTPS công khai, dùng chức năng test webhook của SePay, sau đó thử một khoản chuyển khoản thật nhỏ và kiểm tra log/chi tiết đơn trước khi nhận đơn đại trà.
- Email xác nhận đơn/thanh toán được gửi qua SMTP. Cấu hình `Smtp__Host`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromEmail`, `Smtp__FromName` và `Smtp__EnableSsl` bằng secrets của môi trường; không lưu mật khẩu SMTP trong mã nguồn. Nếu không đặt `Smtp__FromEmail`, ứng dụng dùng email cửa hàng trong Admin rồi mới dùng `Store:Email` dự phòng. Email tạo đơn QR/chuyển khoản có số tiền, ngân hàng, số tài khoản, nội dung chuyển khoản và link VietQR khi đủ cấu hình. Email thanh toán thành công chỉ gửi sau khi Admin xác nhận hoặc webhook SePay đã đối soát đủ tiền. Lỗi gửi thư được ghi log nhưng không làm mất đơn hàng.
- Trên localhost ở môi trường Development, trang xác nhận đơn QR có nút **Mô phỏng thanh toán QR** để thử email và trạng thái. Nút này chỉ giả lập, không chuyển tiền; email mô phỏng ghi rõ không phải giao dịch thật. Tính năng bị khóa ngoài Development/localhost. QR VietQR tĩnh không tự báo tiền vào; muốn xác nhận tiền thật tự động cần webhook từ SePay hoặc nhà cung cấp/ngân hàng có tích hợp.
- Khi phát triển trên máy Windows, có thể chạy `D:\C#\GaRan\Configure-EmailSecrets.ps1` để nhập thông số SMTP và SePay; mật khẩu được nhập ẩn và lưu ngoài repository trong User Secrets của tài khoản Windows hiện tại. User Secrets không được mã hóa, chỉ dành cho phát triển cục bộ; production phải dùng secret store của nền tảng triển khai.
- Production ghi log JSON ra stdout; máy chủ cần chuyển stdout sang hệ thống log có giữ thời hạn và cảnh báo. Cấu hình SQL Server backup định kỳ ngoài ứng dụng (SQL Server Agent hoặc dịch vụ managed), giữ bản sao ngoài máy chủ database và thử khôi phục định kỳ.

## Hướng dẫn kiểm tra thủ công

1. Thêm món vào giỏ, mở trang thanh toán khi chưa đăng nhập; nhập thông tin khách và địa chỉ giao hàng.
2. Chọn phường/xã và rời trường địa chỉ để xem khoảng cách ước tính, phí ship và tổng tiền cập nhật. Bấm **Tính phí giao hàng và xem xác nhận**, kiểm tra lại chi tiết rồi mới xác nhận đơn.
3. Thử cùng phường và khác phường; chỉnh hai khoảng cách ước tính, ngưỡng miễn phí/phạm vi tối đa trong **Admin → Cấu hình phí giao hàng**. Đơn có khoảng cách ước tính vượt giới hạn admin đặt phải bị từ chối.
4. Tạo lần lượt đơn COD, QR và chuyển khoản. COD phải giữ trạng thái chưa thanh toán đến khi giao thành công; QR/chuyển khoản phải chờ nhân viên xác nhận.
5. Dùng mã đơn cùng số điện thoại hoặc email để tra cứu đơn khách vãng lai. Kiểm tra thông tin đơn, các khoản tiền và chức năng in hóa đơn.
6. Đăng nhập Admin/nhân viên để xem chi tiết đơn, xác nhận chuyển khoản hoặc thu COD sau khi giao thành công, và cập nhật trạng thái.
7. Đăng nhập nhân viên, cập nhật đơn giao thành công; kiểm tra đơn chuyển sang hoàn thành và hóa đơn vẫn chỉ có một bản ghi.
