# Backend: chỗ còn thiếu và plan thực hiện

Dành cho Hiếu, Hoàng, Huy. Đọc cùng [BACKEND-GUIDE.md](BACKEND-GUIDE.md) (ai làm gì) và [ARCHITECTURE.md](ARCHITECTURE.md) (cách làm).

## 0. Trạng thái hiện tại

| Đã có | Chưa có |
|---|---|
| 20 entity + migration + seed (đã merge `develop`, PR #15) | Endpoint nào cũng chưa có: **chưa ai lấy được token** |
| JWT, BCrypt, route và quyền theo thư mục role (commit `05ac2fd`, **chưa push**) | Use case mẫu để copy (cả repo chưa có handler thật) |
| Lỗi đồng thời/trùng khóa trả 409 | Port cho Cloudinary, Email, FCM, PayOS, Google Maps, realtime |
| Test kiến trúc, 54 test pass | Test với DB thật; hướng dẫn chạy dự án trên máy mới |

`feature/hoang` chưa merge PR #15, nên entity trên nhánh đó còn là bản cũ (`Guid`). Hoàng và Huy phải `git merge develop` trước khi viết code mới.

## A. Việc chặn người khác (làm trước, Hiếu)

| # | Việc | Vì sao chặn |
|---|---|---|
| A1 | Push `05ac2fd`, mở PR vào `develop` | JWT và route theo role nằm ở đó |
| A2 | `POST api/public/auth/login` (và register + OTP) | Không có token thì Admin và Dispatcher không test được endpoint nào. Tạm thời login là đủ, dùng 4 tài khoản demo |
| A3 | Một use case mẫu hoàn chỉnh: `Public/VehicleTypes` (Query Dapper + controller + test) và một Command mẫu | Cả nhóm copy cấu trúc, không mỗi người một kiểu |
| A4 | Cấu hình API chung: enum trả về dạng chuỗi (`JsonStringEnumConverter`, hiện đang trả số), trường `code` trong ProblemDetails cho mã MSGxx, quy ước phân trang | Client (web, mobile) viết theo contract này, đổi sau rất tốn |
| A5 | Các port (chỉ interface, ở Application): `IFileStorage`, `IEmailSender`, `IPushNotificationService`, `IPaymentGateway`, `IMapService`, `IRealtimeNotifier` | Hoàng (PayOS, FCM, Cloudinary) và Huy (thông báo quyết định sự cố) cần compile được trước khi adapter xong |
| A6 | `appsettings.Development.example.json` (không chứa khóa) + mục "Chạy dự án" trong README + `.gitattributes` | Người mới không chạy được: thiếu `Jwt:SigningKey`, connection string; mỗi lần commit đều có cảnh báo LF/CRLF |

## B. Quyết định thiết kế chưa chốt

SRS/SDS không nói rõ các điểm sau. Cột "Đề xuất" là giá trị mặc định để các bạn không phải chờ; cả nhóm gật hoặc sửa trong buổi họp đầu tuần.

| # | Câu hỏi | Ai bị ảnh hưởng | Đề xuất |
|---|---|---|---|
| B1 | **Công thức giá** | Hiếu (tính), Hoàng (cấu hình) | `distance_fee = max(minimum_fare, base_fare + price_per_km × km)`; `service_fee = LOADING_FEE (nếu chọn) + PEAK_SURCHARGE (giờ cao điểm) + BULKY_SURCHARGE (hàng vượt ngưỡng)`; `total = distance_fee + service_fee − discount`. Giá nằm ở `vehicle_types` và `platform_settings`, **không cần entity `FareRule`** (BACKEND-GUIDE đã ghi sai chỗ này, đã sửa) |
| B2 | Giờ cao điểm, ngưỡng "hàng cồng kềnh" | Hoàng, Hiếu | Thêm key `PEAK_HOURS` (`07:00-09:00,17:00-19:00`) và `BULKY_WEIGHT_KG` vào `platform_settings`. Giờ theo UTC+7 |
| B3 | **Phí hủy đơn** (GB-22 nói "tính phí" nhưng không nói bao nhiêu) | Hiếu (hủy), Huy (hoàn tiền) | Miễn phí khi Pending/Accepted. Sau khi tài xế Arrived: 20% `distance_fee`, tối thiểu 20.000đ. Key `CANCEL_FEE_RATE`, `CANCEL_FEE_MIN` |
| B4 | **Hoa hồng đổi theo thời điểm** (UC-ADM-13) nhưng `platform_settings` chỉ giữ một giá trị | Hoàng | Khi đơn hoàn tất, lưu `commission_rate` và `commission_amount` vào `shipping_orders` (thêm 2 cột, migration của Hoàng). Đơn cũ không bao giờ đổi |
| B5 | **Tìm đơn gần tài xế** (UC-DRV-08) | Hoàng | Tài xế Online + Approved, không có đơn đang chạy (GB-09), đúng loại xe, đơn Pending trong bán kính `MATCHING_RADIUS_KM` (mặc định 5km) quanh `last_latitude/longitude`; sắp theo khoảng cách. Lọc bounding box trong SQL rồi tính Haversine |
| B6 | **Tài xế đăng ký thế nào** | Hiếu (Auth), Hoàng | BF-02 nói "đăng ký bằng số điện thoại", nhưng `users.email` bắt buộc và OTP gửi qua email (GB-25). Dùng chung endpoint register với `role=Driver` (email + OTP). `driver_profiles` tạo ở lần nộp hồ sơ đầu (UC-DRV-03); `driver_wallets` tạo khi duyệt lần đầu |
| B7 | Tài khoản Dispatcher do Admin tạo: mật khẩu ban đầu? | Hoàng | Admin nhập mật khẩu ban đầu, báo trực tiếp; schema chưa có "bắt đổi mật khẩu lần đầu" nên tạm bỏ qua |
| B8 | Xóa tài khoản (UC-CUS-07) | Hiếu | Đặt `account_status = Inactive`, giữ nguyên đơn và lịch sử; không xóa dòng |
| B9 | **Hoàn tiền**: ai tính `requested_amount`, hoàn bao nhiêu khi hủy | Hiếu, Huy, Hoàng | Hủy: tự tính = tiền đã trả − phí hủy (B3). Sự cố/khiếu nại: khách nhập số tiền yêu cầu, Dispatcher đề xuất `proposed_amount ≤ requested_amount`, Admin duyệt và chi trả thủ công (GB-27). "Một khiếu nại mở cho mỗi đơn" kiểm tra trong handler vì MySQL không có unique index một phần |
| B10 | Hợp đồng giữa **đặt đơn và thanh toán** | Hiếu ↔ Hoàng | `Orders` tạo đơn trạng thái Pending, `PaymentStatus=Unpaid`. Đơn PayOS: Hoàng cung cấp `IPaymentService.CreateOrderPayment(orderId)` trả link/QR; webhook báo Paid thì đơn mới hiện cho tài xế. Đơn Cash: hiện ngay |
| B11 | Số lần dùng coupon (`used_count`) tăng ở bước nào | Hiếu ↔ Hoàng | Tăng khi tạo đơn thành công trong cùng transaction; hủy đơn không hoàn lại lượt (đơn giản) |
| B12 | **Realtime**: tên hub, group, message, xác thực WebSocket | Hiếu, Hoàng, Huy | Hub `/hubs/tracking`; group `order-{id}`; client gửi `SendLocation(lat, lng)`, server phát `LocationUpdated` và `OrderStatusChanged`; JWT truyền qua `access_token` ở query string cho WebSocket. Chỉ ghi `driver_location_logs` khi tài xế đang có đơn; lúc rảnh chỉ ghi đè `last_*` |
| B13 | Nội dung chính sách (UC-GST-04) | Hiếu, Hoàng | `master_data_items` chỉ có `code` và `name`, **không có chỗ chứa nội dung**. Thêm cột `description` (TEXT) |
| B14 | Hỗ trợ khách (UC-GST-05) | Hiếu | **Không có bảng nào.** Thêm bảng `support_requests` (tên, liên hệ, nội dung, trạng thái) |
| B15 | Hóa đơn điện tử, hoàn tiền tự động (BF-06, BF-08 nhắc tới) | Hoàng | Mâu thuẫn với GB-27 (chi trả thủ công) và không có use case. **Bỏ khỏi phạm vi** |
| B16 | Xuất báo cáo (UC-ADM-23) | Hoàng | Định dạng nào? Đề xuất CSV (không cần thêm package); Excel/PDF cần hỏi nhóm |
| B17 | Thông báo đẩy có lưu lịch sử không | Hiếu, Hoàng | Không có bảng `notifications` trong SDS → chỉ gửi FCM, không lưu |

## C. Thiếu hạ tầng và quy ước

| # | Mục | Đề xuất |
|---|---|---|
| C1 | Repository riêng cho aggregate. `IRepository<T>` chỉ có `GetByIdAsync`, **không `Include`**. `DriverProfile.Submit` (chỉ một hồ sơ Pending) và `DriverWallet` chỉ đúng khi collection được nạp | Mỗi aggregate có `I<Name>Repository` với method nạp đủ (vd `GetWithApplicationsAsync`). Người sở hữu module tự thêm |
| C2 | Test với DB thật. Không có `Microsoft.AspNetCore.Mvc.Testing`, `WebApplicationFactory<Program>` chưa dùng được dù CLAUDE.md nhắc tới | Xin thêm `Microsoft.AspNetCore.Mvc.Testing` và `Testcontainers.MySql` (máy cả nhóm có Docker; runner CI Ubuntu có sẵn Docker). **Cần nhóm duyệt package** |
| C3 | Khóa dịch vụ ngoài (PayOS sandbox, Google Maps, Cloudinary, Firebase, Gmail) | Mỗi dịch vụ một người tạo tài khoản sandbox và gửi khóa qua kênh riêng của nhóm, không bao giờ lên repo. Hoàng: PayOS + FCM. Hiếu: Google Maps + Cloudinary + Gmail |
| C4 | Dữ liệu demo cho Huy: chưa có đơn, sự cố, hoàn tiền nào để thấy màn hình Dispatcher | Huy mở rộng `DemoDataSeeder` (chỉ Development): vài đơn ở các trạng thái, 1 sự cố, 1 khiếu nại |
| C5 | Quy ước API: phân trang `?page=1&pageSize=20` → `PagedResult<T>`; thời gian ISO 8601 UTC; tiền là số VND; enum là chuỗi | Ghi vào ARCHITECTURE.md khi làm A4 |
| C6 | CI: bước format đang chỉ cảnh báo (`\|\| echo`), không chạy DB; CD chưa xem | Rà lại workflow khi có test Testcontainers |
| C7 | Migration trên production | Chạy như bước deploy, không tự chạy lúc app khởi động (hiện chỉ tự chạy ở Development) |

## D. Plan thực hiện

Mỗi phase kết thúc khi `develop` build sạch, test pass, và các mục "xong khi" được đánh dấu. Mốc thời gian là ước lượng cho nhóm 3 người làm song song; điều chỉnh theo hạn nộp.

### Phase 0: Nền móng (2–3 ngày, Hiếu chính; Hoàng và Huy làm việc song song được ghi ở cột bên)

| Người | Việc | Xong khi |
|---|---|---|
| Hiếu | A1 → A2 → A3 → A4 → A5 → A6 (theo thứ tự) | Login trả token; `GET api/public/vehicle-types` chạy, có test; một PR chung vào `develop` cho mỗi mục |
| Cả nhóm | Họp 30 phút chốt mục B (đặc biệt B1, B3, B4, B6, B10, B12); ghi kết quả vào file này | Mọi dòng B có chữ "Đã chốt" |
| Hoàng, Huy | `git merge develop`; đọc BACKEND-GUIDE; chạy dự án trên máy (cần `Jwt:SigningKey`) | Mỗi người gọi được `login` bằng tài khoản demo |
| Huy | C4: seed dữ liệu demo cho Dispatcher | Có dữ liệu để xem danh sách đơn/sự cố |

### Phase 1: Các module độc lập (khoảng 1 tuần)

| Người | Việc | Phụ thuộc |
|---|---|---|
| Hiếu | `fare-estimate` (Google Maps + `FareCalculator` + coupon), tạo/xem/sửa đơn (tiền mặt trước) | A5, B1, B2 |
| Hoàng | MasterData (UC-ADM-14..16), cấu hình giá và hoa hồng (ADM-10..13), danh sách/khóa tài khoản + tạo Dispatcher (ADM-04..07), adapter Cloudinary | A2 |
| Hoàng | Driver: hồ sơ, nộp giấy tờ, duyệt hồ sơ (DRV-01..07 + ADM-08/09) | B6, C1 |
| Huy | Phía đọc của Dispatcher: danh sách đơn theo trạng thái, danh sách/chi tiết sự cố, danh sách khiếu nại (DIS-01, 03, 04, 11, 12) bằng Dapper | C4 |
| Huy | Test domain `Incident`, `Refund` (state machine) còn thiếu | — |

### Phase 2: Luồng chính của đơn (khoảng 1–2 tuần)

| Người | Việc | Phụ thuộc |
|---|---|---|
| Hoàng | Payments: PayOS (link/QR, webhook có chữ ký, idempotent), coupon, thu COD | B10, adapter PayOS |
| Hoàng | Matching: xem đơn gần, nhận đơn (GB-08, transaction + `row_version`, test hai tài xế cùng nhận) | B5, Phase 1 xong |
| Hoàng | Trips: Arrived → PickedUp → Delivered; ví tài xế (nạp cọc, rút tiền, doanh thu) | Matching |
| Hiếu | Hủy đơn (B3), duyệt bằng chứng lấy hàng, Tracking (SignalR), xác nhận nhận hàng, đánh giá | Trips (để có đơn chạy thật) |
| Huy | Quyết định xử lý sự cố, đổi tài xế, hủy đơn do sự cố, đề xuất hoàn tiền/khiếu nại (DIS-05..10, 13..17) | Entity `Refund` chốt với Hiếu và Hoàng |
| Hiếu | Khách: báo sự cố, yêu cầu hoàn tiền, khiếu nại (CUS-15..18, 21..24) | Cùng entity Incident/Refund với Huy; làm **cùng tuần** |
| Hoàng | Admin duyệt hoàn tiền + chi trả thủ công (ADM-17, 18), duyệt rút tiền (ADM-24) | Huy đề xuất xong |

### Phase 3: Hoàn thiện (khoảng 1 tuần)

- Background jobs: JOB-PAY-01 (Hoàng), JOB-MATCH-01 và JOB-ORDER-01 (Hoàng), JOB-GPS-01 (Hiếu). Mỗi job chỉ gửi Command, có test.
- Báo cáo và dashboard Admin (ADM-21..23), thêm index cho các truy vấn nóng (đơn Pending theo loại xe, giao dịch Pending hết hạn, GPS theo thời gian).
- Test tích hợp với DB thật (C2), rà bảo mật (phân quyền từng endpoint, webhook), tài liệu API (Swagger có nút nhập JWT).
- Quy trình triển khai: migration như bước deploy (C7), biến môi trường cho khóa.

### Cách làm việc để không dẫm chân nhau

1. Hai người cùng đụng một entity (Incident, Refund, ShippingOrder, DriverWallet): **một PR nhỏ riêng cho entity trước**, merge xong mới làm use case.
2. Migration: `git pull develop` ngay trước khi tạo; trùng thì xóa migration của mình, tạo lại.
3. Họp 15 phút đầu mỗi tuần: ai bị chặn, entity nào sắp đổi.
4. Mỗi PR chạy subagent `reviewer` và dán kết quả vào mô tả PR.

## E. Rủi ro cần để mắt

- **Hiếu đang là điểm nghẽn**: Auth, Orders, Maps, ports đều ở Hiếu; Phase 0 trễ thì cả nhóm chờ. Nếu cần, chuyển adapter Google Maps hoặc Gmail cho Huy khi Huy rảnh ở Phase 1.
- **Hoàng nặng nhất** sau Phase 1 (Admin + Driver + Payments + jobs). Nếu trễ, cắt theo thứ tự: báo cáo Excel/PDF, rút tiền, thống kê doanh thu.
- **Phụ thuộc vào dịch vụ ngoài**: PayOS và Google Maps cần tài khoản sandbox ngay từ Phase 0, nếu không Phase 2 bị trễ.
