# Kế hoạch nhóm Backend

Ai làm gì, ranh giới thư mục, thứ tự làm và checklist từng người. Cách làm (layer, CQRS, mẫu use case, quy ước API) ở [ARCHITECTURE.md](ARCHITECTURE.md); quyết định nghiệp vụ ở [DECISIONS.md](DECISIONS.md). Đánh dấu `[x]` khi PR đã merge vào `develop`.

| Người | Vai trò | Controllers |
|---|---|---|
| **Hiếu** | Guest, Customer, Auth và tài khoản dùng chung | `Public/`, `Customer/`, `Account/` |
| **Hoàng** | Administrator, Driver (app tài xế), toàn bộ Payments | `Admin/`, `Driver/`, `Webhooks/` |
| **Huy** | Dispatcher | `Dispatcher/` |

## 1. Quy tắc làm việc

1. Nhánh `feature/<tên>` → PR vào **`develop`**. Không push thẳng hay mở PR vào `main`; `main` chỉ nhận từ `develop` khi nhóm phát hành.
2. Chỉ sửa thư mục của mình (mục 2). Cần sửa phần dùng chung: một PR nhỏ riêng, báo cả nhóm.
3. Một use case = một thư mục `Features/<Module>/{Commands|Queries}/<UseCase>/`. Viết test trước, thấy fail rồi mới code. Luật nghiệp vụ nằm trong entity; vi phạm → `DomainException(message, code)`.
4. **Migration:** `git pull develop` ngay trước khi tạo; trùng thì xóa migration của mình và tạo lại. DEFAULT ở mức DB theo DECISIONS C9. Đổi schema thì ghi thêm vào [REPORT4-CHANGES.md](REPORT4-CHANGES.md).
5. Xong một việc = `dotnet build` không warning, `dotnet test` pass, `dotnet format --verify-no-changes` sạch, chạy subagent `reviewer` trên diff. Commit: `[TICKET-ID] <type>: <mô tả>`.
6. Endpoint mới: thêm vào [api/README.md](api/README.md); mã lỗi mới: thêm vào bảng mã lỗi ở đó.

## 2. Ranh giới thư mục

| Thư mục | Chủ sở hữu | Ghi chú |
|---|---|---|
| `Features/Auth`, `Features/Users`, `Controllers/Account`, `Controllers/Public`, `Controllers/Customer` | Hiếu | Auth và `Account` dùng cho mọi role |
| `Features/Orders`, `Features/Tracking`, `Features/Ratings`, `Domain/Services/FareCalculator` | Hiếu | |
| `Features/Payments`, `Features/Wallets`, `Controllers/Webhooks` | Hoàng | Thanh toán đơn, coupon, webhook PayOS, ví, rút tiền, chi trả hoàn tiền |
| `Features/DriverOnboarding`, `Features/Drivers`, `Features/Matching`, `Features/Trips`, `Controllers/Driver` | Hoàng | |
| `Features/MasterData`, `Features/Pricing`, `Features/Catalog`, `Features/Reports`, `Controllers/Admin` | Hoàng | Giá ở `vehicle_types` + `platform_settings` |
| `Features/Incidents`, `Features/Refunds`, `Controllers/Dispatcher` | Huy | **Dùng chung**: Hiếu thêm use case phía khách, Hoàng thêm use case Admin duyệt; mỗi người chỉ tạo thư mục use case của mình |
| `Infrastructure/BackgroundJobs` | Hoàng (PAY, MATCH, ORDER), Hiếu (GPS) | Job chỉ gửi Command qua `Sender` |
| `Domain/Entities`, `Domain/Enums`, `Persistence/Migrations` | Dùng chung | Đổi entity người khác đang dùng: PR riêng, báo nhóm |

## 3. Nền móng dùng chung (đã xong)

- [x] JWT + refresh token, BCrypt, phân quyền theo thư mục (`RoleConvention`), mã lỗi `code`, rate limit, 409 khi xung đột
- [x] 21 bảng + migration + seed (4 tài khoản demo, mật khẩu `ModMove@123`)
- [x] Use case mẫu: Command `Auth/Commands/Login`, Query `Catalog/Queries/GetVehicleTypes`
- [x] Port: `IFileStorage`, `IEmailSender`, `IPushNotificationService`, `IPaymentGateway`, `IMapService`, `IRealtimeNotifier`; `IPlatformSettings`, `ICouponRepository`, `IShippingOrderRepository`, `IRefreshTokenRepository`
- [x] Fake dùng chung cho test handler: `tests/ModMove.IntegrationTests/Fakes.cs` (`TestApp`, `TestApp.SignedInAs`)
- [ ] Hoàng, Huy: `git merge develop` và chạy được dự án trên máy (`appsettings.Development.json` theo file mẫu)

## 4. Hiếu: Guest / Customer

**Phase 1**
- [x] Auth: đăng ký Customer (`register`) và Driver (`register-driver`), OTP email, xác minh, đăng nhập, refresh token, quên/đặt lại/đổi mật khẩu, đăng xuất
- [x] Tài khoản: xem/sửa hồ sơ, FCM token, xóa tài khoản (`Inactive`, chặn khi còn đơn mở)
- [x] Public: loại xe, chính sách, giới thiệu dịch vụ, gửi yêu cầu hỗ trợ
- [x] Gửi email Gmail SMTP (Development không có SMTP thì ghi OTP ra log)
- [ ] Google Maps (`IMapService`: Geocoding + Distance Matrix hoặc Routes API, DECISIONS 3.4) và `FareCalculator` (B1)
- [ ] `POST api/public/fare-estimate`: loại xe, tải trọng (GB-10, chờ [Còn cần làm rõ](DECISIONS.md#còn-cần-làm-rõ) #2), phí bốc xếp, coupon (GB-21)
- [ ] Cloudinary (`IFileStorage`) + `POST api/customer/uploads` (JPG/PNG ≤ 5 MB, GB-06); ảnh đại diện và ảnh hàng chỉ nhận URL thuộc domain Cloudinary của nhóm
- [ ] Tạo đơn (GST-17): cam kết không hàng cấm (4.3), 1–5 ảnh, mã đơn `MMyyyyMMddNNN` giờ VN, coupon `used_count++` (B11), **chỉ tiền mặt** ở Phase 1; **không có sửa đơn** (4.1)
- [ ] Danh sách, tìm, chi tiết, lịch sử đơn của khách (CUS-08..10, 20)

**Phase 2**
- [ ] Hủy đơn qua `ShippingOrder.CancelByCustomer` (chỉ Pending/Accepted) + tạo hoàn tiền Cancellation cho Admin (3.3)
- [ ] Duyệt/từ chối ảnh lấy hàng (GB-26, tối đa 5 lần gửi: 6.3), xác nhận nhận hàng (CUS-19)
- [ ] Tracking: `TrackingHub` SignalR (B12), đọc `access_token` trên query string cho `/hubs`
- [ ] Phía khách của sự cố, hoàn tiền, khiếu nại (CUS-15..18, 21..24; 7.3, 7.4 `ReplyToInfoRequest`) — cùng tuần với Huy
- [ ] Đánh giá (CUS-25, 26; trong 1 ngày sau hoàn tất, 7.5)
- [ ] Nối thanh toán PayOS vào đặt đơn qua `CreateOrderPayment` của Hoàng (B10, 3.1)
- [ ] Gửi thông báo cho khách theo bảng sự kiện 8.3

**Phase 3**
- [ ] JOB-GPS-01 (xóa GPS log quá 30 ngày)
- [ ] Bảng mã lỗi cho frontend trong `api/README.md` đủ cho mọi endpoint (9.4)

## 5. Hoàng: Admin / Driver / Payments

**Phase 1**
- [ ] MasterData (ADM-14..16), nhập nội dung chính sách và giới thiệu (`description`)
- [ ] Pricing (ADM-10..13): `vehicle_types`, `LOADING_FEE`, `COMMISSION_RATE` (đọc qua `IPlatformSettings`)
- [ ] Tài khoản (ADM-04..07): danh sách, chi tiết, khóa/mở (`Locked`, chỉ Admin mở; khóa thì gọi `RevokeAllForUserAsync`), tạo Dispatcher với mật khẩu cố định (B7)
- [ ] Hồ sơ tài xế: phía Driver (DRV-03..07) và phía Admin duyệt (ADM-08, 09); `driver_wallets` tạo khi duyệt lần đầu
- [ ] Adapter PayOS và FCM

**Phase 2**
- [ ] Thanh toán PayOS (CUS-27, nạp cọc DRV-21 ≥ 50.000đ), tạo lại mã khi hết 10 phút (3.1), webhook (sai chữ ký → từ chối, gọi trùng → idempotent), `ShippingOrder.MarkPaid(now)`
- [ ] Matching (DRV-08..10, B5): đơn PayOS chỉ hiện khi đã trả; GB-08/09/16; ẩn tài xế mất GPS quá 5 phút (6.4)
- [ ] Trips (DRV-11..16, 18, 19): không có hủy từ tài xế (6.1); không có người nhận thì vẫn giao và gửi POD (2.7)
- [ ] Ví (DRV-17, 20..23): thu COD, rút tiền không tối thiểu (5.1), thu nhập = `DriverEarning`
- [ ] Admin duyệt hoàn tiền (cả hoàn tiền do hủy không qua Dispatcher, 3.3) và chi trả tay; duyệt rút tiền (ADM-24)

**Phase 3**
- [ ] JOB-PAY-01, JOB-MATCH-01 (từ lúc tạo hoặc lúc trả, 3.2: `IsMatchingExpired`), JOB-ORDER-01 (`CanBeAutoCompleted`)
- [ ] Báo cáo, doanh thu (8.1, chờ [Còn cần làm rõ](DECISIONS.md#còn-cần-làm-rõ) #3), xuất Excel (B16, xin duyệt package)
- [ ] Thông báo cho tài xế theo bảng sự kiện 8.3

## 6. Huy: Dispatcher

**Phase 1**
- [ ] Mở rộng `DemoDataSeeder` (chỉ Development): vài đơn ở các trạng thái, 1 sự cố, 1 khiếu nại
- [ ] Giám sát đơn và tài xế (DIS-01, 16); danh sách, chi tiết sự cố và khiếu nại (DIS-03, 04, 11, 12)

**Phase 2**
- [ ] Quyết định xử lý sự cố (DIS-05), đổi tài xế (DIS-06, 7.2), hủy đơn do sự cố (DIS-07; hàng cấm không hoàn tiền, 4.3), hủy sự cố (DIS-10)
- [ ] Hàng còn trên xe sau khi hủy: trả về hay giữ lại (2.6)
- [ ] Hoàn tiền và khiếu nại (DIS-08, 09, 13..15, 17): đề xuất `proposed_amount ≤ requested_amount`, xin thêm thông tin (`RequestMoreInfo`, khách trả lời qua 7.4)
- [ ] Không làm màn hình yêu cầu hỗ trợ (2.9)

## 7. Dịch vụ ngoài

| Dịch vụ | Port | Ai làm | Trạng thái |
|---|---|---|---|
| Gmail SMTP | `IEmailSender` | Hiếu | Đã làm |
| Google Maps | `IMapService` | Hiếu | Chưa |
| Cloudinary | `IFileStorage` | Hiếu | Chưa |
| PayOS | `IPaymentGateway` | Hoàng | Chưa |
| FCM | `IPushNotificationService` | Hoàng | Chưa |
| SignalR | `IRealtimeNotifier` | Hiếu | Chưa |

Khóa để trong `appsettings.Development.json` hoặc user-secrets, không commit. Package mới phải hỏi nhóm trước (đã duyệt: `CloudinaryDotNet`, `Microsoft.Extensions.Http`).

## 8. Phụ thuộc giữa các người

```
Hiếu: tạo đơn (tiền mặt) ──> Hoàng: Matching, Trips ──> Huy: đổi tài xế, sự cố
Hoàng: CreateOrderPayment + webhook ──> Hiếu: đặt đơn PayOS
Hiếu + Huy: Incident/Refund (cùng tuần) ──> Hoàng: Admin duyệt, chi trả
Hiếu: TrackingHub ──> Hoàng: gửi GPS; Huy: bản đồ tài xế
```
