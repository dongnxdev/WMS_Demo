# WMS_DEMO - TÀI LIỆU KỸ THUẬT (SOFTWARE DEVELOPMENT DOCUMENTATION)

**Dự án:** Hệ thống Quản lý Kho (WMS)  
**Phiên bản:** 1.0.0  
**Ngày cập nhật:** 03/12/2025  
**Người thực hiện:** Nguyen Xuan Dong (DongNX Dev)

---

## 1. TỔNG QUAN (OVERVIEW)

Hệ thống được xây dựng nhằm giải quyết bài toán quản lý kho cơ bản tại các nhà máy/xưởng sản xuất quy mô vừa và nhỏ. Tập trung vào tính chính xác của dữ liệu tồn kho theo thời gian thực và vị trí lưu trữ.

### Công nghệ sử dụng (Tech Stack)
* **Framework:** .NET 8.0 (ASP.NET Core MVC)
* **Database:** SQL Server
* **ORM:** Entity Framework Core (Code First)
* **Authentication:** Microsoft Identity
* **Frontend:** Razor Views + Bootstrap 5 + jQuery

---

## 2. CẤU TRÚC DỮ LIỆU (DATABASE SCHEMA)

Mô hình dữ liệu được thiết kế tập trung xoay quanh tính toàn vẹn của giao dịch kho.

### 2.1. Master Data (Danh mục)
* **Items (Vật tư):** Quản lý mã, tên, đơn vị tính.
    * *Lưu ý:* Trường `CurrentCost` (Giá vốn) và `CurrentStock` (Tồn tổng) được cập nhật tự động qua nghiệp vụ, không sửa tay.
* **Locations (Vị trí):** Quản lý mã vị trí (Layout kho). // Cần bổ sung bảng nối giữa vật tư và vị trí để rút ngắn hàm xử lý (nếu phát triển thêm)
* **Partners:** `Suppliers` (Nhà cung cấp), `Customers` (Khách hàng).

### 2.2. Transaction Data (Giao dịch)
* **InboundReceipts (Nhập kho):**
    * Header: Ngày nhập, NCC, Người tạo.
    * Details: `ItemId`, `Quantity`, `UnitPrice`, `LocationId`.
* **OutboundReceipts (Xuất kho):**
    * Header: Ngày xuất, Khách hàng, Người tạo.
    * Details: `ItemId`, `Quantity`, `SalesPrice`, `LocationId`.
* **InventoryLogs (Nhật ký kho - Audit Trail):**
    * Lưu vết mọi biến động (+/-) của kho.
    * Dùng để truy vết (Traceability) và đối soát khi có lệch kho.

---

## 3. QUY TRÌNH NGHIỆP VỤ (BUSINESS LOGIC)

Mọi logic xử lý nằm tập trung tại `WarehouseController.cs`.

### 3.1. Nguyên tắc cốt lõi
1.  **Transactional Consistency:** Mọi thao tác Nhập/Xuất đều được gói trong `Database Transaction`. Nếu có lỗi bất kỳ -> **Rollback** toàn bộ để tránh lệch data.
2.  **No Manual Adjustment:** Không cho phép sửa trực tiếp số lượng tồn kho. Phải thông qua phiếu Nhập/Xuất hoặc phiếu Kiểm kê (nếu phát triển thêm).

### 3.2. Nghiệp vụ Nhập kho (Inbound)
* **Tính giá vốn:** Sử dụng phương pháp **Bình quân gia quyền thời điểm (Moving Average)**.
    > Công thức: `Giá Mới = [(Giá Cũ * Tồn Cũ) + (Giá Nhập * SL Nhập)] / (Tồn Cũ + SL Nhập)`
* **Cập nhật tồn kho:** Tăng `CurrentStock` tổng và ghi nhận vị trí lưu trữ.

### 3.3. Nghiệp vụ Xuất kho (Outbound)
* **Kiểm tra tồn kho (Validation):**
    1.  Check tồn kho tổng (`Item.CurrentStock`).
    2.  Check tồn kho theo vị trí (`Location`): Hệ thống tính toán realtime `(Tổng Nhập tại Location - Tổng Xuất tại Location)`. Nếu vị trí đó không đủ hàng -> Chặn xuất.
* **Ghi nhận giá vốn:** Lưu lại giá vốn (`CostPrice`) tại thời điểm xuất để tính lãi/lỗ chính xác cho từng đơn hàng.

### 3.4. Nghiệp vụ Xóa/Hủy (Revert)
* **Xóa Phiếu Nhập:** Chỉ cho phép nếu hàng chưa bị xuất đi. Hệ thống sẽ tính toán lại giá vốn ngược chiều (Reverse Calculation) để trả lại giá trị cũ.
* **Xóa Phiếu Xuất:** Hàng được hoàn trả lại vào kho.

---
## 4. Yêu cầu và cầu hình hệ thống 

### Yêu cầu hệ thống
* OS: Windows/Linux Server
* Runtime: .NET 8.0 Runtime
* DB: SQL Server 2019+

### Cấu hình (appsettings.json)
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=WMS_PROD;User Id=sa;Password=..."
}
```
---

## 5. Triển khai & Cài đặt (Deployment & Setup)

### 5.1. Cấu hình Webserver (IIS)
Đảm bảo **Internet Information Services (IIS)** đã được cài đặt và kích hoạt các feature sau trong *Server Manager*:

* **World Wide Web Services** > **Application Development Features**:
    * .NET Extensibility 4.8 (hoặc cao hơn)
    * ASP.NET 4.8
    * ISAPI Extensions
    * ISAPI Filters
    * WebSocket Protocol *(Bắt buộc nếu ứng dụng sử dụng SignalR/Realtime - emit/listener)*

### 5.2. Cài đặt Runtime
* Tải và cài đặt **[.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0)** (bao gồm .NET Runtime và IIS Support).
* *Lưu ý:* Sau khi cài đặt, nên khởi động lại IIS (lệnh `iisreset` trong CMD) để hệ thống nhận module.

### 5.3. Thiết lập Cơ sở dữ liệu (SQL Server)
**Yêu cầu:** SQL Server 2019/2022 Express & SQL Server Management Studio (SSMS).

1. **Kết nối:** Mở SSMS và kết nối vào SQL Server instance.
2. **Tạo Database:** Khởi tạo database mới cho dự án.
3. **Cấu hình Security (Quan trọng):**
    * Vào **Security** > **Logins** > Chuột phải chọn **New Login**.
    * Chọn **SQL Server authentication**: Đặt *Username* và *Password* (Lưu lại để cấu hình Connection String).
    * **User Mapping**: Map user vừa tạo với Database dự án, cấp quyền `db_owner` hoặc quyền `read/write` phù hợp.
    * **Server Properties**: Đảm bảo server đang chạy ở chế độ **SQL Server and Windows Authentication mode** (Mixed Mode).

### 5.4. Build & Publish Ứng dụng
* Thực hiện build bản release từ máy local (Dev environment):
    ** dotnet publish -c Release -o ./publish
    ** Nén toàn bộ thư mục ./publish thành file .zip.
    ** Deploy (upload) file nén lên thư mục web root trên VPS.
    
### 5.5. Cấu hình Ứng dụng trên IIS

1. **Add Website:**
    * Mở **IIS Manager** > Chuột phải vào **Sites** > **Add Website**.
    * Điền *Site name*, trỏ *Physical path* tới thư mục code vừa giải nén.

2. **Application Pool:**
    * Đảm bảo App Pool của website đang chạy chế độ **No Managed Code** (đối với .NET 8/Core trở lên).

3. **Cập nhật Connection String:**
    * Mở file `appsettings.json` (hoặc `web.config` nếu có).
    * Cập nhật chuỗi kết nối khớp với thông tin User/Pass SQL Server đã tạo ở bước 5.3.

---

## 6. Cấu hình Tên miền & Mạng (DNS & Networking)

### 6.1. Quản trị DNS (DNS Management)
Truy cập trang quản trị tên miền (Domain Provider) và cấu hình các bản ghi:

| Loại (Type) | Tên (Name/Host) | Giá trị (Value/Target) | Mô tả |
| :--- | :--- | :--- | :--- |
| **A** | `@` | `[IP_Của_VPS]` | Trỏ root domain về VPS |
| **CNAME** | `www` | `[Ten_Mien]` | Điều hướng www về root domain |

### 6.2. Cấu hình Binding trên VPS
1. Tại **IIS Manager**, chọn Site vừa tạo.
2. Chọn **Bindings** (cột bên phải).
3. Thêm/Sửa các binding cho port `80` (HTTP) với *Host name* là tên miền.

### 6.3. Bảo mật SSL (HTTPS)
Lựa chọn một trong các phương án sau để kích hoạt HTTPS:

* **Option 1: Cloudflare (Khuyên dùng)**
    * Bật Proxy (đám mây cam) trên Cloudflare để ẩn IP gốc và sử dụng SSL miễn phí của Cloudflare.
* **Option 2: Win-ACME (Let's Encrypt)**
    * Sử dụng tool **Win-ACME**.
    * Chạy `wacs.exe` > Chọn tạo certificate cho IIS site tương ứng (Auto-renew).
