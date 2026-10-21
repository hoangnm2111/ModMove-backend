# Việc cần làm của 3 thành viên Backend

Nguồn: SRS (Report-3), SDS (Report-4). Chi tiết quy tắc ở [BACKEND-GUIDE.md](BACKEND-GUIDE.md), quyết định đã chốt ở [DECISIONS.md](DECISIONS.md). Đánh dấu `[x]` khi PR đã merge vào `develop`.

| Thành viên | Vai trò | Khối lượng (use case) |
|---|---|---|
| **Hiếu** | Guest, Customer, Auth dùng chung | 17 Guest + 29 Customer |
| **Hoàng** | Admin, Driver, toàn bộ Payments | 24 Admin + 23 Driver |
| **Huy** | Dispatcher | 17 Dispatcher |

Quy ước mỗi việc: một use case = một thư mục `Features/<Module>/{Commands|Queries}/<UseCase>/`; viết test trước; `dotnet build` không warning, `dotnet test` và `dotnet format --verify-no-changes` pass; chạy subagent `reviewer` trước khi mở PR.

## Việc chung (cả nhóm)

- [x] Họp chốt các quyết định thiết kế: xem [DECISIONS.md](DECISIONS.md) (còn 2 câu hỏi ở cuối file)
- [ ] Hoàng và Huy chạy `git merge develop` (entity đã đổi sang khóa `long`, base class đổi)
- [ ] Mỗi người chạy được dự án trên máy: `appsettings.Development.json` có connection string và `Jwt:SigningKey` (≥32 ký tự); đăng nhập bằng 4 tài khoản demo (mật khẩu `ModMove@123`)
- [ ] Mỗi người viết danh sách endpoint của role mình vào `docs/api/<role>.md` (method, route, role, request/response tóm tắt) trước khi sang Phase 1
- [ ] Entity dùng chung (`ShippingOrder`, `Incident`, `Refund`, `DriverWallet`): một PR nhỏ riêng cho entity, merge xong mới làm use case
- [ ] Migration: `git pull develop` ngay trước khi tạo; trùng thì xóa và tạo lại

---

## Hiếu: Guest / Customer

### Phase 0: Nền móng (chặn Hoàng và Huy)

- [ ] Push các commit trên `feature/hieu`, mở PR vào `develop`
- [x] `POST api/public/auth/login` (trả JWT) — Hoàng, Huy cần để test
- [x] Use case mẫu: `GET api/public/vehicletypes` (Query Dapper + Controller + test) và một Command mẫu
- [x] Cấu hình API chung: enum dạng chuỗi (`JsonStringEnumConverter`), trường `code` mã MSGxx trong ProblemDetails, quy ước phân trang `PagedResult<T>`
- [x] Port ở Application (`Common/Interfaces/Ports.cs`): `IFileStorage`, `IEmailSender`, `IPushNotificationService`, `IPaymentGateway`, `IMapService`, `IRealtimeNotifier`
- [x] `appsettings.Development.example.json`, mục "Chạy dự án" trong README, `.gitattributes`

### Phase 1

- [ ] **Auth** (Features/Auth): đăng ký + OTP email (UC-GST-01; GB-01 duy nhất, GB-25 OTP 5 phút, gửi tối đa 3 lần/15 phút); đăng nhập (CUS-01; GB-02 khóa 10 phút sau 5 lần sai); quên mật khẩu (CUS-02); đổi mật khẩu (CUS-04); đăng xuất (CUS-03). Đăng ký cho cả `role=Driver`
- [ ] **Users** (Features/Users): xem/sửa hồ sơ (CUS-05, 06); xóa tài khoản = đặt `Inactive` (CUS-07)
- [ ] **Public** (Controllers/Public): giới thiệu, chính sách (GST-03, 04); gửi yêu cầu hỗ trợ (GST-05, bảng `support_requests` **đã có**, B14). Loại xe (GST-02) đã có
- [ ] Adapter: Gmail SMTP (`IEmailSender`), Google Maps (Geocoding, Distance Matrix), Cloudinary (`IFileStorage`)
- [ ] **Ước tính giá** `api/public/fare-estimate` (GST-06..16): `FareCalculator` theo công thức B1 (không giờ cao điểm), kiểm tra tải trọng (GB-10), coupon qua `ValidateCoupon` của Hoàng (GB-21)
- [ ] **Orders**: tạo đơn (GST-17; GB-05, 06, 07, 28), sửa đơn trước khi có tài xế nhận (CUS-28), danh sách/tìm/chi tiết/lịch sử (CUS-08..10, 20)

### Phase 2

- [ ] Hủy đơn (CUS-29; GB-22, hoàn tiền theo B3; hỏi nhóm cửa sổ hủy trước khi code)
- [ ] Duyệt/từ chối bằng chứng lấy hàng (CUS-11, 12; GB-26)
- [ ] Xác nhận nhận hàng (CUS-19)
- [ ] **Tracking**: `TrackingHub` SignalR, vị trí tài xế và tiến trình (CUS-13, 14); ghi `driver_location_logs`
- [ ] Báo sự cố, xem hướng xử lý, yêu cầu hoàn tiền + bằng chứng (CUS-15..18); khiếu nại 24h, xem kết quả, rút khiếu nại (CUS-21..24; GB-18) — làm cùng tuần với Huy
- [ ] Đánh giá chuyến, xem đánh giá đã gửi (CUS-25, 26; GB-20)
- [ ] Tích hợp thanh toán vào luồng đặt đơn qua `IPaymentService` của Hoàng (B10)

### Phase 3

- [ ] JOB-GPS-01 (xóa GPS log > 30 ngày)
- [ ] Thêm index cho truy vấn nóng; rà phân quyền endpoint Customer/Public

---

## Hoàng: Admin / Driver / Payments

### Phase 1

- [ ] **MasterData** (ADM-14..16): danh sách, thêm/sửa, bật/tắt; cột `description` **đã có** (B13)
- [ ] **Pricing** (ADM-10..13): giá theo xe (`vehicle_types`), phí bốc xếp và hoa hồng (`platform_settings`, không giờ cao điểm, B2); cột hoa hồng trên đơn **đã có** (B4), không tạo migration cho phần này
- [ ] **Users admin** (ADM-04..07): danh sách, chi tiết, khóa/mở khóa, tạo Dispatcher (mật khẩu cố định, B7); hồ sơ/đổi mật khẩu dùng lại Auth (ADM-01..03)
- [ ] **DriverOnboarding** — phía Driver (DRV-01..07): upload giấy tờ, xem trạng thái, nộp lại tạo version mới, hồ sơ/xe (GB-03, GB-04)
- [ ] **DriverOnboarding** — phía Admin (ADM-08, 09): xem hồ sơ chờ duyệt, duyệt hoặc từ chối kèm lý do từng mục
- [ ] Repository riêng `IDriverProfileRepository` nạp đủ `Applications` và `Vehicle` (C1)
- [ ] Adapter PayOS (`IPaymentGateway`) và FCM (`IPushNotificationService`); khóa sandbox lấy từ user-secrets

### Phase 2

- [ ] **Payments**: link/QR PayOS hết hạn 10 phút (GB-13) cho CUS-27 và nạp cọc DRV-21; chọn phương thức (GST-15); coupon `ValidateCoupon` + tạo coupon; chi trả hoàn tiền `PayoutRefund` (GB-27)
- [ ] **Webhook PayOS** `Controllers/Webhooks`: xác minh chữ ký, sai chữ ký → từ chối, gọi trùng → idempotent (cần test 3 ca này)
- [ ] **Matching** (DRV-08..10): đơn gần tài xế (B5), nhận đơn trong một transaction với `row_version` (GB-08), một đơn đang chạy (GB-09), ví ≥ 200.000đ cho COD (GB-16); test hai tài xế nhận cùng lúc
- [ ] **Trips** (DRV-11..16, 18, 19): Arrived → PickedUp (≥2 ảnh, GB-11) → Delivered (ảnh POD, GB-12); báo sự cố; nhận quyết định của Dispatcher; gửi GPS qua hub
- [ ] **Wallets** (DRV-17, 20..23): thu COD (GB-15), số dư, nạp cọc, yêu cầu rút tiền, doanh thu ngày/tuần/tháng; hoa hồng (GB-14)
- [ ] Admin duyệt/từ chối hoàn tiền kèm mã tham chiếu ngân hàng (ADM-17, 18; GB-19, 27); duyệt rút tiền (ADM-24)

### Phase 3

- [ ] JOB-PAY-01 (10 phút), JOB-MATCH-01 (5 phút, GB-24), JOB-ORDER-01 (15 phút, GB-23)
- [ ] Đánh giá và lịch sử sự cố cho Admin (ADM-19, 20)
- [ ] Dashboard, doanh thu nền tảng, xuất báo cáo (ADM-21..23, xuất Excel: xin duyệt package trước)
- [ ] Test lỗi bắt buộc cho Payments/Wallet: thiếu tiền, webhook sai chữ ký, gọi trùng, ví bị đóng băng

---

## Huy: Dispatcher

### Phase 0 và 1

- [ ] Mở rộng `DemoDataSeeder` (chỉ Development): vài đơn ở các trạng thái, 1 sự cố, 1 khiếu nại để có dữ liệu xem
- [ ] Test domain còn thiếu: `InTransitIncident`, `Refund` (state machine Requested → Approved/Rejected → Processed), `ReassignDriver`
- [ ] **Giám sát** (DIS-01): danh sách mọi đơn theo trạng thái, tài xế đang hoạt động trên bản đồ, chi tiết đơn; ca đã xử lý (DIS-16)
- [ ] **Sự cố** — đọc (DIS-03, 04): danh sách, chi tiết (mô tả, ảnh, GPS, đơn/tài xế/khách)
- [ ] **Khiếu nại** — đọc (DIS-11, 12): danh sách và chi tiết
- [ ] Adapter hoặc port còn thiếu mà Dispatcher cần (gửi thông báo qua `IPushNotificationService` và `IRealtimeNotifier`)

### Phase 2

- [ ] **Quyết định xử lý sự cố** (DIS-05): tiếp tục / đổi tài xế / hủy đơn, gửi cho cả hai bên
- [ ] **Đổi tài xế** (DIS-06): `ShippingOrder.ReassignDriver`, tài xế mới không có đơn đang chạy (GB-09, GB-17)
- [ ] **Hủy đơn do sự cố** (DIS-07): kích hoạt quy trình hoàn tiền; hủy sự cố không hợp lệ (DIS-10)
- [ ] **Hoàn tiền** (DIS-08, 09, 17): xem yêu cầu, đối chiếu bằng chứng lấy hàng/sự cố/POD, đề xuất toàn bộ/một phần/không hoàn (`proposed_amount ≤ requested_amount`), gửi Admin duyệt (GB-19)
- [ ] **Khiếu nại** — xử lý (DIS-13, 14, 15): đối chiếu bằng chứng, xin thêm thông tin, đề xuất xử lý
- [ ] Kiểm tra phân quyền: mọi endpoint chỉ role Dispatcher; Dispatcher không tự chi trả được

### Phase 3

- [ ] Test handler cho luồng sự cố → đổi tài xế → hoàn tiền (dùng fake, không DB thật, xem C2)
- [ ] Hỗ trợ Hoàng/Hiếu: adapter còn dang dở (đề xuất Google Maps hoặc Gmail), nếu có thời gian

---

## Phụ thuộc giữa các việc

```
Hiếu Phase 0 (login, mẫu, ports) ──> mọi việc Phase 1 của Hoàng và Huy
Hoàng DriverOnboarding + Matching ──> đơn có tài xế ──> Huy: đổi tài xế, Hiếu: Tracking
Hiếu + Huy cùng chốt entity Incident/Refund ──> Hoàng: duyệt và chi trả
Hoàng IPaymentService ──> Hiếu: luồng đặt đơn có thanh toán
```

## Mốc kiểm tra

- **Cuối Phase 0:** ai cũng đăng nhập được; có use case mẫu; mục B đã chốt.
- **Cuối Phase 1:** cấu hình giá, hồ sơ tài xế được duyệt, đặt đơn tiền mặt chạy; Dispatcher thấy danh sách đơn/sự cố.
- **Cuối Phase 2:** một đơn đi trọn vòng đời (đặt → tài xế nhận → giao → thanh toán → hoàn tất), một sự cố được xử lý đến khi hoàn tiền.
- **Cuối Phase 3:** jobs chạy, báo cáo có số liệu, test tích hợp xanh, quy trình deploy có migration.
