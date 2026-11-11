# Kế hoạch nhóm Backend

Ai làm gì, ranh giới thư mục, thứ tự làm và checklist từng người. Cách làm (layer, CQRS, mẫu use case, quy ước API) ở [ARCHITECTURE.md](ARCHITECTURE.md); quyết định nghiệp vụ ở [DECISIONS.md](DECISIONS.md). Đánh dấu `[x]` khi PR đã merge vào `develop`.

| Người | Vai trò | Controllers |
|---|---|---|
| **Hiếu** | Guest, Customer, Auth và tài khoản dùng chung | `Public/`, `Customer/`, `Account/` |
| **Hoàng** | Administrator, Driver (màn hình tài xế trên app), toàn bộ Payments | `Admin/`, `Driver/`, `Webhooks/` |
| **Huy** | Dispatcher | `Dispatcher/` |

## 1. Quy tắc làm việc

1. Nhánh `feature/<tên>` → PR vào **`develop`**. Không push thẳng hay mở PR vào `main`; `main` chỉ nhận từ `develop` khi nhóm phát hành.
2. Chỉ sửa thư mục của mình (mục 2). Cần sửa phần dùng chung: một PR nhỏ riêng, báo cả nhóm.
3. Một use case = một thư mục `Features/<Module>/{Commands|Queries}/<UseCase>/`. Viết test trước, thấy fail rồi mới code. Luật nghiệp vụ nằm trong entity; vi phạm → `DomainException(message, code)`.
4. **Migration:** `git pull develop` ngay trước khi tạo; trùng thì xóa migration của mình và tạo lại. DEFAULT ở mức DB theo DECISIONS C9.
5. Xong một việc = `dotnet build` không warning, `dotnet test` pass, `dotnet format --verify-no-changes` sạch, chạy subagent `reviewer` trên diff. Commit: `[TICKET-ID] <type>: <mô tả>`.
6. Endpoint mới: thêm vào [api/README.md](api/README.md); mã lỗi mới: thêm vào bảng mã lỗi ở đó.

## 2. Ranh giới thư mục

| Thư mục | Chủ sở hữu | Ghi chú |
|---|---|---|
| `Features/Auth`, `Features/Users`, `Controllers/Account`, `Controllers/Public`, `Controllers/Customer` | Hiếu | Auth và `Account` dùng cho mọi role |
| `Features/Orders`, `Features/Tracking`, `Features/Ratings`, `Domain/Services/FareCalculator` | Hiếu | |
| `Features/Payments`, `Features/Wallets`, `Controllers/Webhooks` | Hoàng | Thanh toán đơn, coupon, webhook PayOS, ví, rút tiền, chi trả hoàn tiền |
| `Features/DriverOnboarding`, `Features/Drivers`, `Features/Matching`, `Features/Trips`, `Controllers/Driver` | Hoàng | |
| `Features/MasterData`, `Features/Pricing`, `Features/Catalog`, `Features/Reports`, `Controllers/Admin` | Hoàng | Giá ở `vehicle_types` + `platform_settings`. **Ngoại lệ:** hai query public `Catalog/Queries/GetVehicleTypes` và `MasterData/Queries/GetPublicContent` do Hiếu viết, giữ nguyên chỗ, Hiếu sửa được |
| `Features/Incidents`, `Features/Refunds`, `Controllers/Dispatcher` | Huy | **Dùng chung**: Hiếu thêm use case phía khách, Hoàng thêm use case Admin duyệt; mỗi người chỉ tạo thư mục use case của mình |
| `Infrastructure/BackgroundJobs` | Hoàng (PAY, MATCH, ORDER), Hiếu (GPS) | Job chỉ gửi Command qua `Sender` |
| `Domain/Entities`, `Domain/Enums`, `Persistence/Migrations` | Dùng chung | Đổi entity người khác đang dùng: PR riêng, báo nhóm |

## 3. Nền móng dùng chung (đã xong)

- [x] JWT + refresh token, BCrypt, phân quyền theo thư mục (`RoleConvention`), mã lỗi `code`, rate limit, 409 khi xung đột
- [x] 22 bảng + migration + seed (4 tài khoản demo, mật khẩu `ModMove@123`)
- [x] Use case mẫu: Command `Auth/Commands/Login`, Query `Catalog/Queries/GetVehicleTypes`
- [x] Port: `IFileStorage`, `IEmailSender`, `IPushNotificationService`, `IPaymentGateway`, `IMapService`, `IRealtimeNotifier`; `IPlatformSettings`, `ICouponRepository`, `IShippingOrderRepository`, `IRefreshTokenRepository`
- [x] Fake dùng chung cho test handler: `tests/ModMove.IntegrationTests/Fakes.cs` (`TestApp`, `TestApp.SignedInAs`)
- [x] Hoàng, Huy: `git merge develop` và chạy được dự án trên máy (`appsettings.Development.json` theo file mẫu)

## 4. Hiếu: Guest / Customer

**Phase 1**
- [x] Auth: đăng ký Customer (`register`) , OTP email, xác minh, đăng nhập, refresh token, quên/đặt lại/đổi mật khẩu, đăng xuất
- [x] Tài khoản: xem/sửa hồ sơ, FCM token, xóa tài khoản (`Inactive`, chặn khi còn đơn mở)
- [x] Public: loại xe, chính sách, giới thiệu dịch vụ, gửi yêu cầu hỗ trợ
- [x] Gửi email Gmail SMTP (Development không có SMTP thì ghi OTP ra log)
- [x] MapVina (`IMapService`: Search v2 + Directions v2, DECISIONS 3.4; trước đây Google Maps) và `FareCalculator` (B1)
- [x] `POST api/public/fareestimate`: loại xe, tải trọng và lọt thùng theo món lớn nhất (GB-10, 4.2: dùng `VehicleType.CanCarry`), phí bốc xếp, coupon (GB-21) với mã lỗi riêng `COUPON_NOT_FOUND`, `COUPON_INACTIVE`, `COUPON_EXPIRED`, `COUPON_LIMIT_REACHED`, `COUPON_MIN_ORDER_NOT_MET`
- [x] Cloudinary (`IFileStorage`) + `POST api/customer/uploads` (JPG/PNG ≤ 5 MB, GB-06); ảnh hàng chỉ nhận URL thuộc domain Cloudinary của nhóm
- [x] Tạo đơn (GST-17): cam kết không hàng cấm (4.3), 1–5 ảnh, mã đơn `MMyyyyMMddNNN` giờ VN, coupon `used_count++` (B11), **chỉ tiền mặt** ở Phase 1; **không có sửa đơn** (4.1)
- [x] Danh sách, tìm, chi tiết, lịch sử đơn của khách (CUS-08..10, 20)

**Phase 2**
- [x] Hủy đơn qua `ShippingOrder.CancelByCustomer` (chỉ Pending/Accepted) + tạo hoàn tiền Cancellation cho Admin (3.3)
- [x] Duyệt/từ chối ảnh lấy hàng (GB-26, tối đa 5 lần gửi: 6.3), xác nhận nhận hàng (CUS-19)
- [x] Tracking: `TrackingHub` SignalR theo B12: `access_token` trên query string; `JoinOrder` kiểm tra đơn là của khách (`ORDER_NOT_YOURS`); `SendLocation` phát tới `order-{id}` và group `dispatchers`; Dispatcher tự vào `dispatchers` theo role
- [x] Phía khách của sự cố, hoàn tiền, khiếu nại (CUS-15..18, 21..24; 7.3, 7.4 `ReplyToInfoRequest`) — cùng tuần với Huy
- [x] Đánh giá (CUS-25, 26; trong 1 ngày sau hoàn tất, 7.5)
- [x] Nối thanh toán PayOS vào đặt đơn qua `CreateOrderPayment` của Hoàng (B10, 3.1): tạo đơn PayOS (`Pending` + `Unpaid`, tài xế chỉ thấy sau khi thanh toán) và chi tiết đơn có `payment`
- [x] `INotificationService` (B17): gửi FCM tới app và SignalR `NotificationReceived` qua `Clients.User`; không lưu lịch sử. Gửi thông báo cho khách theo bảng sự kiện 8.3
- [x] Logout nhận kèm FCM token của app, chỉ xóa token nếu khớp token đang lưu (9.2)

**Phase 3**
- [x] REST theo dõi đơn: vị trí cuối và ETA (CUS-13)
- [x] Danh mục `api/public/lookups/...`, mã loại hàng và mã lý do khi tạo đơn, hủy, khiếu nại; danh sách coupon cho khách (GST-10, 14, CUS-21, 29)
- [x] Khách thấy lý do quyết định hoàn tiền (CUS-23); tài xế nhận `INCIDENT_REPORTED`, `RATING_RECEIVED`
- [x] JOB-GPS-01 (xóa GPS log quá 30 ngày): `GpsLogCleanupJob` chạy mỗi 24 giờ; job của Hoàng kế thừa `PeriodicCommandJob`
- [x] Tài khoản nhận hoàn tiền (R-01): khai kèm khiếu nại/hoàn sự cố, `PUT api/customer/refunds/{id}/payout-account`, khách xem số đã che, `GET api/public/lookups/banks`, nhắc khi hủy đơn đã trả (`REFUND_BANK_INFO_REQUIRED`), gợi ý tài khoản PayOS (`useSuggested`, `suggestedPayoutAccount`)
- [ ] Bảng mã lỗi cho frontend trong `api/README.md` đủ cho mọi endpoint (9.4)

## 5. Hoàng: Admin / Driver / Payments

**Phase 1**
- [x] MasterData (ADM-14..16), nhập nội dung chính sách và giới thiệu (`description`)
- [x] Pricing (ADM-10..13): `vehicle_types`, `LOADING_FEE`, `COMMISSION_RATE` (đọc qua `IPlatformSettings`); chặn sửa `COMMISSION_RATE` khi còn đơn từ `Accepted` tới trước `Delivered` (C8)
- [x] Tài khoản (ADM-04..07): danh sách, chi tiết, khóa/mở (`Locked`, chỉ Admin mở; khóa thì gọi `RevokeAllForUserAsync`), tạo Dispatcher với mật khẩu cố định (B7)
- [x] Hồ sơ tài xế: phía Driver (DRV-03..07) và phía Admin duyệt (ADM-08, 09); `driver_wallets` tạo khi duyệt lần đầu
- [x] Adapter PayOS (`PayOsPaymentGateway` + `NotConfiguredPaymentGateway`)
- [x] Adapter FCM (`IPushNotificationService`)

**Phase 2**
- [x] Thanh toán đơn hàng PayOS (CUS-27): tạo lại mã khi hết 10 phút (3.1), `CreateOrderPayment` nhận `platform` (`Web` | `App`) và chọn `returnUrl` từ cấu hình `PayOS:ReturnUrls:*`, client không gửi URL (3.1), webhook `POST api/webhooks/payos` (sai chữ ký → từ chối, gọi trùng → idempotent, đối soát số tiền), `ShippingOrder.MarkPaid(now)` và cộng ví cọc khi webhook báo thành công
- [x] Tạo link nạp cọc ví tài xế qua PayOS (DRV-21: số tiền nguyên > 0, tối thiểu phần thiếu dưới `REQUIRED_DEPOSIT`)
- [x] Matching (DRV-08..10, B5): đơn PayOS chỉ hiện khi đã trả; GB-08/09; mọi đơn cần ví cọc ≥ `REQUIRED_DEPOSIT` (`DriverWallet.CanReceiveOrders`), đơn tiền mặt cần thêm ≥ hoa hồng của đơn (`DriverWallet.CanReceiveCod`, dùng cả khi đổi tài xế); ẩn tài xế mất GPS từ 5 phút trở lên (6.4)
- [x] Trips (DRV-11..16, 18, 19): không có hủy từ tài xế (6.1); không có người nhận thì vẫn giao và gửi POD (2.7)
- [x] Ví (DRV-17, 20..23): thu COD, rút tiền không tối thiểu, tối đa = `WalletTotal` (chỉ `EarningsBalance` khi đang có đơn chạy), ưu tiên trừ thu nhập trước rồi ví cọc (lưu `EarningsAmount`, `DepositAmount` để hoàn đúng nguồn khi Admin từ chối); tự bù cọc (`DepositRefill`) khi ví cọc < `REQUIRED_DEPOSIT`; thu nhập = `DriverEarning` (GB-32)
- [x] Admin duyệt hoàn tiền (cả hoàn tiền do hủy không qua Dispatcher, 3.3) và chi trả tay (`RefundsController`: `GET`, `approve`, `reject`, `payout`); truyền lý do cho khách vào tham số `note` của `Refund.Approve`/`RejectRefund` (CUS-23); trước khi duyệt gọi `order.EnsureRefundFits(0, await refunds.GetClaimedTotalForOrderAsync(order.Id))` để tổng hoàn của đơn không vượt `total_fee` (B9); gửi `CustomerEvent.RefundDecided` (8.3)
- [x] Admin duyệt rút tiền tài xế (ADM-24)

**Phase 3**
- [x] JOB-MATCH-01 (từ lúc tạo hoặc lúc trả, 3.2: `IsMatchingExpired`), tự động tạo hoàn tiền 100% cho đơn PayOS (3.3)
- [x] JOB-PAY-01 (mã hết hạn sau 10 phút; hủy đơn PayOS vẫn `Unpaid` sau 30 phút từ `created_at`, 3.1), JOB-ORDER-01 (`CanBeAutoCompleted`, bỏ qua đơn có khiếu nại mở (`HasOpenComplaintAsync`, 7.3), rồi `order.Complete(rate, System, null, now)` + `OrderSettlement.SettleAsync` trong `Features/Orders` như ConfirmReceipt của Hiếu, để ví được tất toán giống nhau)
- [x] Báo cáo, doanh thu (8.1: tổng `commission_amount` của đơn `Completed`, giờ VN), xuất Excel (B16, xin duyệt package)
- [x] Thông báo cho tài xế theo bảng sự kiện 8.3, gọi qua `INotificationService` (B17)
- [x] Thông báo cho **khách** khi chính bước của Hoàng đổi trạng thái (8.3): nhận đơn `CustomerEvent.OrderAccepted`, đến điểm lấy `DriverArrived`, gửi ảnh lấy hàng `PickupPhotosToReview`, giao hàng `OrderDelivered`, JOB-ORDER-01 `OrderCompleted`, Admin duyệt/chi hoàn tiền `RefundDecided`, job tự hủy đơn `OrderCancelled`: gọi `NotifyCustomerAsync`, và `PublishOrderStatusAsync` sau mỗi lần đổi trạng thái đơn (B12)
- [x] Tài khoản nhận hoàn tiền phía Admin/PayOS (R-01, K1–K6): seed master data `BANK` theo VietQR (`BankSeedData`), lưu tài khoản người trả từ webhook PayOS (`payment_transactions.payer_*` + migration) và nối gợi ý `useSuggested`/`suggestedPayoutAccount`, gọi `RefundBankInfoRequired` ở `CancelUnmatchedOrders` và webhook, Admin xem đầy đủ tài khoản nhận + `hasPayoutAccount`, `payout` chặn khi thiếu tài khoản (`REFUND_BANK_ACCOUNT_REQUIRED`), và endpoint `POST api/admin/refunds/{id}/payout-failed` (`REFUND_PAYOUT_FAILED`)

## 6. Huy: Dispatcher

**Phase 1**
- [x] Mở rộng `DemoDataSeeder` (chỉ Development): các đơn ở mọi trạng thái (`MMDEMO001` - `MMDEMO013`), 5 sự cố, 5 khiếu nại, hoàn tiền sự cố và 2 tài xế phụ có GPS (DemoOperationsSeeder)
- [x] Danh sách, chi tiết sự cố (DIS-03, 04)
- [x] Danh sách, chi tiết và xác minh khiếu nại (DIS-11, 12, 13)
- [x] Xử lý khiếu nại và hoàn tiền sự cố (DIS-14, 15, 08, 09, 17): xin thêm thông tin (`RequestComplaintInfo`, `RequestIncidentRefundInfo` gửi `REFUND_INFO_REQUESTED`), đề xuất hoàn tiền `proposed_amount ≤ requested_amount` lên Admin duyệt (`ProposeComplaintResolution`, `ProposeIncidentRefundResolution`)
- [x] Giám sát đơn và tài xế, danh sách ca đã xử lý (DIS-01, 16)

**Phase 2**
- [x] Quyết định xử lý sự cố (DIS-05), đổi tài xế (DIS-06, 7.2: tài xế mới đủ điều kiện như B5, đơn tiền mặt thêm GB-16), hủy đơn do sự cố (DIS-07: hàng cấm và khách vắng mặt ở điểm lấy không hoàn tiền, 4.3, 2.4; không có tài xế thay thế thì hoàn 100% đơn PayOS, 2.5), hủy sự cố (DIS-10)
- [x] Thông báo cho **khách** khi xử lý xong sự cố (8.3): `NotifyCustomerAsync(order, CustomerEvent.IncidentResolved, ct, detail)`, `detail` nói rõ quyết định; đổi tài xế hoặc hủy đơn thì gọi thêm `PublishOrderStatusAsync` (B12). Đổi tài xế: gọi cho **cả tài xế cũ và tài xế mới**, vì tài xế không vào group đơn
- [x] Hàng còn trên xe sau khi hủy: trả về hay giữ lại (2.6)
- [x] Không làm màn hình yêu cầu hỗ trợ (2.9)
- [ ] `CancelOrderByIncident` theo luật tổng hoàn (B9): khi hoàn 100%, xử lý mọi khoản hoàn đang mở của đơn (không chỉ của sự cố đang xử lý), từ chối nếu đơn đã có khoản được duyệt hoặc đã chi; tự đóng các sự cố đang mở khác của đơn

## 7. Dịch vụ ngoài

| Dịch vụ | Port | Ai làm | Trạng thái |
|---|---|---|---|
| Gmail SMTP | `IEmailSender` | Hiếu | Đã làm |
| MapVina | `IMapService` | Hiếu | Đã làm |
| Cloudinary | `IFileStorage` | Hiếu | Đã làm |
| PayOS | `IPaymentGateway` | Hoàng | Đã làm |
| FCM | `IPushNotificationService` | Hoàng | Đã làm |
| SignalR | `IRealtimeNotifier` | Hiếu | Đã làm (`WebApi/Hubs`, `/hubs/tracking`) |

Khóa để trong `appsettings.Development.json` hoặc user-secrets, không commit. Package mới phải hỏi nhóm trước (đã duyệt: `CloudinaryDotNet`, `Microsoft.Extensions.Http`).

## 8. Phụ thuộc giữa các người

```
Hiếu: tạo đơn (tiền mặt) ──> Hoàng: Matching, Trips ──> Huy: đổi tài xế, sự cố
Hoàng: CreateOrderPayment + webhook ──> Hiếu: đặt đơn PayOS
Hiếu + Huy: Incident/Refund (cùng tuần) ──> Hoàng: Admin duyệt, chi trả
Hiếu: TrackingHub ──> Hoàng: gửi GPS; Huy: bản đồ tài xế
```
