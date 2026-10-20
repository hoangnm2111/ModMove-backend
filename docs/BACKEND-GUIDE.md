# Hướng dẫn phân công & triển khai Backend

Nguồn: Report-3 (SRS) và Report-4 (SDS). Quy tắc kiến trúc ở [ARCHITECTURE.md](ARCHITECTURE.md) và [CLAUDE.md](../CLAUDE.md); file này chỉ nói **ai làm gì** và **làm theo thứ tự nào**.

| Người | Vai trò phụ trách | Controllers |
|---|---|---|
| **Hiếu** | Guest + Customer (+ Auth dùng chung) | `Public/`, `Customer/` |
| **Hoàng** | Administrator + Driver (app tài xế) + toàn bộ Payments | `Admin/`, `Driver/`, `Webhooks/` |
| **Huy** | Dispatcher | `Dispatcher/` |

> Driver (UC-DRV-01..23) do Hoàng làm cùng phần Admin vì hai bên dùng chung hồ sơ, ví và đơn.

## 1. Quy tắc chung cho cả 3 người

1. Nhánh `feature/<tên>` → PR vào `develop`. Không push thẳng `develop`/`main`.
2. **Mỗi người chỉ sửa thư mục của mình** (bảng mục 2). Cần sửa file dùng chung → mở PR nhỏ riêng, báo cả nhóm.
3. Một use case = một thư mục `Features/<Module>/{Commands|Queries}/<UseCase>/` (Command/Query + Handler + Validator). Xem template trong ARCHITECTURE.md.
4. Với mỗi use case: viết test từ SRS trước → thấy fail → mới code. Ưu tiên test domain rule (GB-xx) và validator.
5. Route: `api/<role>/<resource>` (vd `api/customer/orders`, `api/admin/driver-applications`, `api/dispatcher/incidents`). Tên controller `<Resource>Controller`.
6. Business rule (GB-xx) nằm trong **entity/domain service**, vi phạm → `DomainException` (422). Ghi mã rule trong tên test, vd `Cancel_AfterArrived_ChargesFee_GB22`.
7. Xong một việc = `dotnet build` không warning + `dotnet test` pass + `dotnet format` sạch.
8. Mã lỗi/thông báo dùng System Messages (MSG01…) ở SRS mục V.2.

## 2. Ranh giới thư mục (tránh conflict)

| Thư mục | Chủ sở hữu | Ghi chú |
|---|---|---|
| `Features/Auth`, `Features/Users`, `Controllers/Public`, `Controllers/Customer` | Hiếu | Auth dùng chung cho mọi role |
| `Features/Orders`, `Features/Tracking`, `Features/Ratings` | Hiếu | Đặt/hủy đơn, theo dõi, đánh giá |
| `Features/Payments` (thanh toán đơn PayOS, COD, coupon, webhook, hoàn tiền chi trả) | Hoàng | Toàn bộ phần thanh toán; Hiếu chỉ gọi qua interface |
| `Features/DriverOnboarding`, `Features/Drivers`, `Features/MasterData`, `Features/Reports`, `Controllers/Admin`, `Controllers/Driver` | Hoàng | |
| `Features/Pricing` — **cấu hình** giá (entity `FareRule`, CRUD) | Hoàng | |
| `Domain/Services/FareCalculator` — **tính** giá | Hiếu | Đọc `FareRule` của Hoàng |
| `Features/Wallets` (ví tài xế, rút tiền, nạp cọc) | Hoàng | Cả phía Driver và Admin |
| `Features/Incidents`, `Features/Refunds` (phía Dispatcher), `Controllers/Dispatcher` | Huy | |
| `Features/Matching`, `Features/Trips`, `Infrastructure/BackgroundJobs` (MATCH, ORDER) | Hoàng | Phía Driver |
| `Controllers/Webhooks`, `BackgroundJobs` (PAY) | Hoàng | |
| `BackgroundJobs` (GPS) | Hiếu | |
| `Domain/Entities`, `Domain/Enums`, `Infrastructure/Persistence/Migrations` | **Dùng chung** | Xem mục 3 |

## 3. Việc dùng chung — làm trước, làm một lần

Làm tuần đầu, mỗi mục một PR nhỏ, merge vào `develop` trước khi ai làm use case dựa vào nó.

| # | Việc | Người làm | Ai phụ thuộc |
|---|---|---|---|
| S1 | JWT + role claim (`Customer`, `Driver`, `Dispatcher`, `Admin`), `UseAuthentication`, `[Authorize(Roles=…)]` trong `ApiControllerBase` cho từng nhóm controller, `JwtTokenService`, `BCryptPasswordHasher`, `ICurrentUserService` đọc userId/role | Hiếu | Tất cả |
| S2 | Entity lõi theo ERD (SRS mục 3): `User`, `ShippingOrder` + `OrderStatus` (Pending, Accepted, Arrived, PickedUp, InTransit, Delivered, Completed, Cancelled), `Incident`, `Refund` (SourceType: Cancellation/Incident/Complaint), `DriverWallet`, `FareRule`… | Mỗi người tự tạo entity của module mình; entity dùng chéo (`ShippingOrder`, `User`) do Hiếu tạo | Tất cả |
| S3 | **Một người** gộp migration: trước khi tạo migration mới, `git pull develop`; nếu trùng, xóa migration của mình và tạo lại | Quy ước cả nhóm | — |
| S4 | `IRealtimeNotifier` + `TrackingHub` (SignalR) | Hiếu | Huy (gửi quyết định sự cố), Driver |
| S5 | `IFileStorage` (Cloudinary), `IEmailSender` (SMTP/OTP), `IPushNotificationService` (FCM) — chỉ interface ở Application, adapter ở `Infrastructure/ExternalServices/<Provider>` | Hiếu làm Cloudinary + Email; Hoàng làm FCM | Hoàng (ảnh hồ sơ), Huy (thông báo) |

Huy và Hoàng trong lúc chờ S1/S2 làm domain + test trước (không cần HTTP).

## 4. Phần của từng người

### 4.1 Hiếu — Guest / Customer

**Public (không cần đăng nhập)** — `Controllers/Public/`
- UC-GST-02..04: danh sách loại xe, giới thiệu dịch vụ, chính sách (Query, dữ liệu từ MasterData của Hoàng).
- UC-GST-05: gửi yêu cầu hỗ trợ.
- UC-GST-06..16: **ước tính giá không cần đăng nhập** — `GET/POST api/public/fare-estimate` (địa chỉ → Geocoding, Distance Matrix → `FareCalculator`, GB-10 kiểm tra tải trọng, coupon GB-21 qua `ValidateCoupon` của Hoàng). Draft của Guest lưu ở client; server chỉ tính giá.

**Auth** — `Features/Auth` (dùng chung mọi role)
- UC-GST-01 đăng ký + OTP (GB-01, GB-25: OTP email, hết hạn 5 phút, gửi lại tối đa 3 lần/15 phút).
- UC-CUS-01 đăng nhập (GB-02: khóa 10 phút sau 5 lần sai), CUS-02 quên mật khẩu, CUS-03 đăng xuất, CUS-04 đổi mật khẩu.
- Endpoint đăng nhập dùng chung cho Admin (UC-ADM-01), Dispatcher (UC-DIS-02), Driver (UC-DRV-01); chỉ khác role claim.
- UC-CUS-05..07 xem/sửa hồ sơ, xóa tài khoản (`Features/Users`).

**Orders** — `Features/Orders`
- UC-GST-17 / UC-CUS-28 / 29: tạo, sửa (chỉ khi chưa có tài xế nhận), hủy đơn.
  - GB-05 một điểm lấy – một điểm giao; GB-06 ≤5 ảnh JPG/PNG ≤5MB; GB-07 hàng cấm; GB-10 tải trọng; GB-28 chỉ Customer Active mới gửi đơn.
  - GB-22 hủy miễn phí khi Pending/Accepted, sau khi tài xế Arrived thì tính phí. Logic nằm trong `ShippingOrder.Cancel(...)`.
- UC-CUS-08..10, 20: danh sách (lọc theo trạng thái/thời gian), chi tiết, lịch sử — Query bằng Dapper, phân trang `PagedResult<T>`.
- UC-CUS-11, 12: xem và duyệt ảnh bằng chứng lấy hàng. GB-26: chỉ sau khi Customer duyệt thì `PickedUp → InTransit`.
- UC-CUS-19: xác nhận đã nhận hàng (GB-23 tự hoàn tất sau 24h là job của Hoàng).

**Tracking** — UC-CUS-13, 14: vị trí tài xế (SignalR group `order-{id}`, độ trễ 1–3s) và tiến trình.

**Payments (Hoàng làm, Hiếu chỉ tích hợp)** — Customer chọn phương thức (GST-15) và trả đơn qua PayOS (CUS-27) là API của Hoàng: Hiếu thêm vào luồng đặt đơn bằng `IPaymentService` (hoặc Command `CreateOrderPayment`) do Hoàng cung cấp. Thống nhất sớm hợp đồng: input `orderId` + phương thức, output link/QR + hạn 10 phút (GB-13).

**Incidents / Refunds (phía Customer)** — UC-CUS-15..18 (báo sự cố, xem hướng xử lý, yêu cầu hoàn tiền + upload bằng chứng), UC-CUS-21..24 (khiếu nại trong 24h sau giao, GB-18 một khiếu nại/đơn, rút khiếu nại khi còn `Requested`).

**Ratings** — UC-CUS-25, 26 (GB-20: một đánh giá/chuyến, không sửa được).

**Thứ tự gợi ý:** S1 → Auth → Public/fare-estimate → Orders (tạo/xem) → Tracking → Orders (hủy/sửa/duyệt bằng chứng) → Ratings → Incidents/Refunds Customer.

### 4.2 Hoàng — Administrator + Driver + Payments

**Phần Driver** (`Controllers/Driver/`, `[Authorize(Roles = "Driver")]`):

- **Hồ sơ & onboarding (DRV-01..07):** đăng nhập dùng Auth chung; DRV-03 upload giấy tờ (Cloudinary), DRV-04 xem trạng thái Pending/Approved/Rejected, DRV-05 nộp lại tạo *version mới*, DRV-06/07 xem/sửa hồ sơ + xe. Ghép với ADM-08/09 trong `DriverOnboarding`. GB-03 chỉ Online khi Approved; GB-04 biển số duy nhất.
- **Matching (DRV-08..10, `Features/Matching`):** xem đơn Pending gần vị trí và đúng loại xe, xem chi tiết, nhận đơn. GB-08 người nhận đầu tiên thắng — một transaction, xử lý đồng thời (kiểm tra trạng thái + concurrency token, test hai tài xế nhận cùng lúc). GB-09 một đơn đang chạy/tài xế. GB-10 tải trọng. GB-16 ví ≥ 200.000đ mới nhận đơn COD.
- **Thực hiện chuyến (DRV-11..16, 18, 19, `Features/Trips`):** Arrived → PickedUp (GB-11 ≥2 ảnh, chờ Customer duyệt, GB-26) → Delivered (GB-12 ảnh POD + thời gian) ; báo đến nơi giao (không đổi trạng thái); báo sự cố kèm GPS; nhận quyết định của Dispatcher. Gửi GPS qua `TrackingHub` mỗi 5–10 giây. Chuyển trạng thái là method trong `ShippingOrder`, test từng chuyển trạng thái hợp lệ và không hợp lệ.
- **Ví & thu nhập (DRV-17, 20..23, `Features/Wallets`):** xác nhận thu COD (GB-15 trừ hoa hồng từ ví cọc), xem số dư, nạp cọc qua PayOS, yêu cầu rút tiền, doanh thu theo ngày/tuần/tháng. Tiền luôn `decimal`; test thiếu tiền, gọi trùng; số liệu đã quyết toán không sửa được (GB-14).
- **Background jobs:** JOB-MATCH-01 (5 phút, GB-24 hủy đơn quá 10 phút không ai nhận, hoàn tiền nếu trả trước), JOB-ORDER-01 (15 phút, GB-23 tự hoàn tất sau 24h và cộng thu nhập).

**Phần Payments** (`Features/Payments`, `Controllers/Webhooks`, `Infrastructure/ExternalServices/PayOS`) — toàn bộ thanh toán do Hoàng:

- **Trả đơn (GST-14, GST-15, CUS-27):** chọn phương thức PayOS hoặc tiền mặt; tạo link/QR PayOS trước khi đơn tới tài xế, hết hạn 10 phút (GB-13); tiền mặt thu lúc lấy hàng hoặc giao hàng (DRV-17, GB-15 trừ hoa hồng từ ví cọc).
- **Coupon (GB-21):** kiểm tra chi tiêu tối thiểu, ngày hiệu lực, giới hạn dùng. Cung cấp `ValidateCoupon` để Hiếu gọi trong `fare-estimate`; tạo coupon khuyến mãi (UC-18 trong SRS mục II) cũng ở đây.
- **Webhook PayOS (API-PAY-01):** xác minh chữ ký trước khi cập nhật, sai chữ ký → từ chối, gọi trùng → idempotent, xử lý trong 3–5 giây. Dùng chung cho thanh toán đơn và nạp cọc.
- **Chi trả thủ công (GB-27):** hoàn tiền đã duyệt và rút tiền ghi mã tham chiếu ngân hàng.
- **JOB-PAY-01** (10 phút): hết hạn giao dịch Pending quá timeout.
- Bắt buộc có test: thiếu tiền, webhook sai chữ ký, gọi trùng.

**Phần Admin** (`Controllers/Admin/`, `[Authorize(Roles = "Admin")]`):


- **Tài khoản (Users):** UC-ADM-04..07 danh sách/chi tiết/khóa-mở khóa/tạo tài khoản Dispatcher. Đăng nhập, hồ sơ, đổi mật khẩu (ADM-01..03) dùng lại Auth của Hiếu, chỉ thêm endpoint hồ sơ riêng nếu cần.
- **DriverOnboarding:** UC-ADM-08, 09 xem hồ sơ chờ duyệt, duyệt hoặc từ chối kèm lý do **từng mục**. Nộp lại hồ sơ tạo *version mới* (UC-DRV-05), nên entity `DriverApplication` có version. GB-03: tài xế chỉ Online khi Approved; GB-04 biển số duy nhất.
- **Pricing (cấu hình):** UC-ADM-10..13 giá cơ bản theo km, phụ phí (bốc xếp, giờ cao điểm, hàng cồng kềnh), tỷ lệ hoa hồng. GB-14 hoa hồng 10%; **hoa hồng mới chỉ áp cho đơn hoàn tất sau khi đổi** → lưu lịch sử có `EffectiveFrom`, không sửa bản ghi cũ (quy tắc "Driver earnings immutability"). Hiếu cần entity `FareRule` sớm: chốt hình dạng entity với Hiếu ngay đầu tuần.
- **MasterData:** UC-ADM-14..16 danh sách, thêm/sửa, bật/tắt (loại xe, chính sách…). Public endpoints của Hiếu đọc từ đây.
- **Refunds / Complaints:** UC-ADM-17, 18 xem khiếu nại; duyệt hoặc từ chối đề xuất hoàn tiền của Dispatcher (GB-19), nhập mã tham chiếu ngân hàng khi chi trả thủ công (GB-27, trong 3 ngày làm việc). Dùng entity `Refund` mà Huy đề xuất, không tạo song song.
- **Ratings / Incident history:** UC-ADM-19, 20 (chỉ Query).
- **Reports:** UC-ADM-21..23 doanh thu nền tảng theo ngày/tuần/tháng, dashboard, xuất báo cáo (Query Dapper; xuất file cần hỏi nhóm trước khi thêm package).
- **Wallets:** UC-ADM-24 duyệt yêu cầu rút tiền của tài xế: Paid (kèm mã tham chiếu) hoặc Rejected.

**Thứ tự gợi ý:** MasterData → Pricing (chốt `FareRule`) → Users → DriverOnboarding (Admin + Driver) → Matching → Trips → Wallets → Refunds duyệt → Reports/Dashboard. Phần Driver nên làm sớm vì Customer và Dispatcher cần đơn có tài xế để test.

### 4.3 Huy — Dispatcher

`Controllers/Dispatcher/`, mọi endpoint `[Authorize(Roles = "Dispatcher")]`. Tài khoản Dispatcher do Admin tạo (UC-ADM-07), đăng nhập qua Auth chung.

- **Giám sát:** UC-DIS-01 danh sách mọi đơn theo trạng thái, tài xế đang hoạt động trên bản đồ, chi tiết đơn. UC-DIS-16 các ca đã xử lý.
- **Sự cố:** UC-DIS-03, 04 danh sách và chi tiết (mô tả, ảnh, GPS, đơn/tài xế/khách). UC-DIS-05 quyết định: tiếp tục chuyến / đổi tài xế / hủy đơn, **gửi cho cả hai bên** qua `IRealtimeNotifier` + push. UC-DIS-10 hủy sự cố không hợp lệ.
- **Đổi tài xế & hủy đơn:** UC-DIS-06, 07. GB-17: chỉ Dispatcher được làm. Logic ở `ShippingOrder.Reassign(...)`/`Cancel(...)`; hủy đơn do sự cố kích hoạt quy trình hoàn tiền. Reassign phải tôn trọng GB-09 (tài xế mới không có đơn đang chạy).
- **Hoàn tiền:** UC-DIS-08, 09, 17 xem yêu cầu, đối chiếu bằng chứng lấy hàng/sự cố/POD, **đề xuất** hoàn toàn bộ / một phần / không hoàn, gửi Admin duyệt (GB-19). Dispatcher không được tự chi trả.
- **Khiếu nại:** UC-DIS-11..15 danh sách, chi tiết, đối chiếu bằng chứng, xin thêm thông tin từ khách/tài xế, đề xuất xử lý.

**Điểm cần thống nhất sớm với Hiếu:** entity `Incident`, `Refund` (state: Requested → Proposed → Approved/Rejected → Paid) và các thông báo realtime. Hiếu làm phía Customer tạo ra các bản ghi này, Huy làm phía xử lý, nên **cùng chốt entity trong một PR** rồi mỗi người làm use case của mình.

**Thứ tự gợi ý:** domain `Incident`/`Refund` + test state machine → Query danh sách/chi tiết → Decide resolution → Reassign/Cancel → Refund proposal → Complaint → Monitor + bản đồ.

## 5. Tích hợp ngoài (đặt trong `Infrastructure/ExternalServices/<Provider>`)

| Dịch vụ | Interface (Application) | Ai làm | Ghi chú |
|---|---|---|---|
| Google Maps (Geocoding, Distance Matrix, Directions) | `IMapService` | Hiếu | Fare estimate cần trước |
| PayOS | `IPaymentGateway` | Hoàng | Key để `appsettings.Development.json`/user-secrets |
| Cloudinary | `IFileStorage` | Hiếu | GB-06 giới hạn ảnh |
| SMTP Gmail | `IEmailSender` | Hiếu | OTP |
| FCM | `IPushNotificationService` | Hoàng | Dùng cho thông báo sự cố (Huy) |

Không commit secret. Không thêm NuGet package khi chưa hỏi nhóm.

## 6. Webhook và Background jobs

| Mục | Người làm | Ghi chú |
|---|---|---|
| Webhook PayOS (`Controllers/Webhooks`) | Hoàng | Xác minh chữ ký trước khi cập nhật; gọi trùng không cộng tiền hai lần. Cũng xử lý kết quả nạp cọc của Hoàng |
| JOB-PAY-01 (10 phút, GB-13) | Hoàng | Hết hạn giao dịch PayOS treo Pending |
| JOB-GPS-01 (00:00 hằng ngày) | Hiếu | Xóa GPS log > 30 ngày, trừ ca đang mở |
| JOB-MATCH-01 (5 phút, GB-24) | Hoàng | Hủy đơn không ai nhận, hoàn tiền nếu trả trước |
| JOB-ORDER-01 (15 phút, GB-23) | Hoàng | Tự hoàn tất đơn Delivered quá 24h, cộng thu nhập tài xế |

Job là `BackgroundService` đặt trong `Infrastructure/BackgroundJobs`, chỉ gửi Command qua `Sender`, không chứa logic nghiệp vụ.

## 7. Phụ thuộc giữa các người (thứ tự merge)

```
Hiếu: S1 Auth/JWT ─┬─> Hoàng: Users, DriverOnboarding, mọi Admin và Driver endpoint
                   └─> Huy:   mọi Dispatcher endpoint
Hiếu: ShippingOrder, FareCalculator <─ Hoàng: FareRule, Payments (IPaymentService, ValidateCoupon)
Hiếu: Incident/Refund Customer  ──(cùng chốt entity)──  Huy: Incident/Refund xử lý ──> Hoàng: duyệt Refund
Hiếu: IRealtimeNotifier/TrackingHub ──> Huy: gửi quyết định sự cố; Hoàng: GPS tài xế
Hoàng: Matching/Trips (đơn có tài xế) ──> Huy: Reassign, Incident; Hiếu: Tracking, hoàn tất đơn
```

## 8. Checklist trước khi mở PR

- [ ] Mỗi UC có test (domain rule / validator), test lỗi cho Payments/Wallet.
- [ ] Controller mỏng, chỉ `Sender.Send`; có `[Authorize(Roles=…)]` đúng role.
- [ ] Query dùng Dapper tham số hóa, truyền `CancellationToken`; tiền là `decimal`.
- [ ] `dotnet build` (0 warning), `dotnet test` (gồm ArchitectureTests), `dotnet format --verify-no-changes`.
- [ ] Chạy subagent `reviewer` trên diff so với `develop`.
- [ ] Commit: `[TICKET-ID] <type>: <mô tả>`.
