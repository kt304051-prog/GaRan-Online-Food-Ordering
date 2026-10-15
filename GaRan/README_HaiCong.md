# Chạy GaRan thành hai cổng web riêng, dùng chung dữ liệu

GaRan chạy hai website trên máy chủ:

- **Khách hàng:** cổng 5181.
- **Nhân viên/Admin:** cổng nội bộ 5182.

Cả hai kết nối cùng database `GaRan` trên máy chủ. Cổng khách hàng không có các route quản trị; nhân viên và Admin đăng nhập ở cổng nội bộ. Chạy cổng khách hàng trước để ứng dụng kiểm tra và bổ sung cấu trúc database; sau đó mới chạy cổng nội bộ.

## Chuẩn bị

1. Cài .NET 8 SDK và SQL Server trên máy chủ.
2. Kết nối SQL Server trong SSMS và tạo database/bảng ban đầu bằng `GaRan\Sql\GaRan_Supplement.sql` nếu chưa có database GaRan.
3. Kiểm tra `GaRan\appsettings.json`: chuỗi kết nối phải trỏ đến SQL Server trên **máy chủ** (bản này đang dùng `127.0.0.1,1433`). SQL Server chỉ cần truy cập từ ứng dụng chạy trên chính máy chủ; không mở cổng SQL ra mạng nội bộ.
4. Mọi máy cần ở cùng mạng Wi-Fi/LAN riêng tư. Tránh mạng Wi-Fi khách có bật cô lập thiết bị.

## Chạy cổng khách hàng

Mở PowerShell số 1 trong thư mục `GaRan`, rồi chạy:

```powershell
dotnet run --launch-profile customer-lan
```

Giữ cửa sổ này mở. Khi thấy ứng dụng đang nghe trên cổng 5181 và trang khách hàng mở được trên máy chủ, database đã được khởi tạo.

## Chạy cổng nội bộ

Mở PowerShell số 2 ở cùng thư mục, rồi chạy:

```powershell
dotnet run --launch-profile internal-lan
```

Cổng này là nơi Admin và nhân viên đăng nhập. Tài khoản mẫu khi chạy Development:

- Admin: `admin` / `Admin@123`
- Nhân viên: `nhanvien` / `NhanVien@123`

## Cho phép kết nối từ mạng nội bộ

Mở PowerShell bằng **Run as administrator** trên máy chủ và thêm quy tắc firewall (chỉ Private profile và Local Subnet):

```powershell
New-NetFirewallRule -DisplayName "GaRan LAN portals" -Direction Inbound -Protocol TCP -LocalPort 5181,5182 -Action Allow -Profile Private -RemoteAddress LocalSubnet
```

Chạy `ipconfig`, tìm IPv4 Address của bộ điều hợp Wi-Fi trên máy chủ. Ví dụ IP là `192.168.1.25`, gửi hai địa chỉ này cho nhóm:

- Khách hàng: `http://192.168.1.25:5181`
- Nhân viên/Admin: `http://192.168.1.25:5182`

Thay IP ví dụ bằng IP thật trên máy chủ. Từ máy chủ có thể dùng `http://localhost:5181` và `http://localhost:5182`.

## Trong lúc test

- Giữ cả hai cửa sổ PowerShell và máy chủ luôn bật; `Ctrl+C` dừng từng cổng.
- Đơn hàng tạo trên cổng khách hàng sẽ xuất hiện trong cổng nội bộ sau khi tải lại danh sách.
- Mỗi thành viên dùng trình duyệt riêng; tất cả cùng nhìn thấy dữ liệu của một database.
- Cổng dùng HTTP để thử nội bộ. Không dùng mật khẩu thật hoặc dữ liệu khách hàng thật trên mạng không tin cậy; cách chạy này không phải cấu hình triển khai công khai.
- Mô phỏng thanh toán QR hiện chỉ bật khi mở bằng `localhost`. Người truy cập qua IP LAN có thể đặt đơn QR nhưng trạng thái sẽ chờ xác nhận; admin cần xác nhận thủ công. QR tĩnh không tự kiểm tra tiền vào.

## Nếu không vào được

1. Kiểm tra hai tiến trình còn chạy và đang dùng đúng cổng 5181/5182.
2. Mở thử từng địa chỉ localhost trên máy chủ.
3. Kiểm tra IP Wi-Fi trên máy chủ và đảm bảo máy khách cùng mạng.
4. Kiểm tra mạng Windows đang đặt là **Private**, rồi xác nhận quy tắc firewall tồn tại.
5. Đảm bảo không có ứng dụng khác dùng cổng 5181 hoặc 5182.
