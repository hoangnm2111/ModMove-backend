# Quyết định thiết kế Backend (đã chốt)

Nhóm đã thảo luận các điểm SRS/SDS chưa rõ và chốt như dưới đây. Khi code mà quyết định ở đây khác SRS/SDS, **làm theo file này**. Muốn đổi một quyết định: sửa file này trong một PR riêng, cả nhóm duyệt.

Mục "Còn cần làm rõ" ở cuối là các chỗ quyết định còn mơ hồ; đừng tự đoán, hỏi nhóm trước khi code phần đó.

## 1. Giá, hủy đơn, hoàn tiền

| # | Quyết định | Người làm | Việc phát sinh trong code |
|---|---|---|---|
| B1 | **Giá đơn** = `max(minimum_fare, base_fare + price_per_km × số km)` + phí bốc xếp (nếu chọn, `LOADING_FEE`) − giảm giá coupon. `minimum_fare` giữ lại để chặn giá quá thấp cho chuyến rất ngắn. Giá nằm ở `vehicle_types` và `platform_settings`, không có entity `FareRule` | Hiếu (tính), Hoàng (cấu hình) | `FareCalculator` trong `Domain/Services`; không có phụ phí cao điểm, không có phụ phí cồng kềnh |
| B2 | **Không có giờ cao điểm.** Giá giống nhau ở mọi thời điểm đặt đơn | Hoàng, Hiếu | Đã bỏ key `PEAK_SURCHARGE`, `BULKY_SURCHARGE` khỏi seed |
| B3 | **Hủy đơn** (khách hủy): <br>• Chưa có tài xế nhận: hoàn 100% tiền đã trả qua PayOS; nếu giao dịch PayOS chưa xong thì hủy lệnh thanh toán. <br>• Đã có tài xế nhận, tài xế chưa bấm "Đã đến điểm lấy", và khách hủy trong cửa sổ cho phép (xem "Còn cần làm rõ" #1): hoàn 100%. <br>• Tài xế đã đến điểm lấy hoặc đã lấy hàng: khách chịu 100% cước, không hoàn | Hiếu (hủy), Huy, Hoàng (chi trả) | `ShippingOrder.Cancel` cần biết thời điểm `AcceptedAt` và trạng thái; số tiền hoàn tính trong domain |
| B9 | **Hoàn tiền**: hủy đơn tính theo B3 (tự động ra số tiền). Sự cố/khiếu nại: khách nhập số tiền yêu cầu; Dispatcher đề xuất `proposed_amount ≤ requested_amount`; Admin duyệt và chi trả thủ công (GB-27). Mỗi đơn chỉ một khiếu nại mở, kiểm tra trong handler | Hiếu, Huy, Hoàng | Đã có trong entity `Refund` |
| B4 | **Hoa hồng**: khi đơn hoàn tất, `ShippingOrder.Complete(rate, …)` lưu `commission_rate` và `commission_amount` (làm tròn đến đồng). **Nền tảng chịu coupon**, nên hoa hồng và thu nhập tài xế tính trên `distance_fee + service_fee` (trước giảm giá). Đổi tỷ lệ không ảnh hưởng đơn cũ | Hoàng (dùng) | **Đã có** cột và logic (migration `AlignSchemaWithSds`); handler đọc `COMMISSION_RATE` rồi truyền vào |
| B11 | **Coupon**: `used_count` tăng khi tạo đơn thành công, cùng transaction; hủy đơn không hoàn lượt | Hiếu ↔ Hoàng | — |

## 2. Đặt đơn, thanh toán, tìm đơn

| # | Quyết định | Người làm | Việc phát sinh trong code |
|---|---|---|---|
| B10 | **Đặt đơn và thanh toán**: đơn tạo ra ở `Pending`, `PaymentStatus = Unpaid`. Đơn **PayOS** chỉ hiện cho tài xế khi `Pending` **và** `PaymentStatus = Paid` (webhook xác nhận). Đơn **tiền mặt** hiện ngay | Hiếu ↔ Hoàng | Hoàng cung cấp `CreateOrderPayment(orderId)` trả link/QR; query tìm đơn của tài xế lọc theo điều kiện trên |
| B5 | **Tìm đơn gần tài xế**: tài xế Online + Approved, không có đơn đang chạy (GB-09), đúng loại xe; đơn trong bán kính `MATCHING_RADIUS_KM` (mặc định 5 km) quanh `last_latitude/longitude`, sắp theo khoảng cách. Lọc bounding box trong SQL rồi tính Haversine | Hoàng | **Đã có** key `MATCHING_RADIUS_KM = 5` trong seed |
| B12 | **Realtime**: hub `/hubs/tracking`; group `order-{id}`; client gửi `SendLocation(lat, lng)`; server phát `LocationUpdated` và `OrderStatusChanged`; JWT qua `access_token` trên query string. Chỉ ghi `driver_location_logs` khi tài xế đang có đơn; lúc rảnh chỉ ghi đè `last_*` | Hiếu, Hoàng, Huy | Cấu hình JwtBearer đọc `access_token` cho đường `/hubs` |

## 3. Tài khoản

| # | Quyết định | Người làm | Việc phát sinh trong code |
|---|---|---|---|
| B6 | **Tài xế đăng ký chỉ trên app mobile.** Backend dùng chung endpoint đăng ký với `role = Driver` (email + OTP). `driver_profiles` tạo khi nộp hồ sơ lần đầu (UC-DRV-03); `driver_wallets` tạo khi được duyệt lần đầu | Hiếu, Hoàng | — |
| B7 | **Dispatcher**: Admin tạo tài khoản và mật khẩu; đó là mật khẩu cố định của Dispatcher (không bắt đổi lần đầu) | Hoàng | — |
| A-01 | **Đăng nhập** (đã làm): sai email, sai mật khẩu và tài khoản đang bị khóa (GB-02) trả **cùng một** lỗi 401, để không lộ email nào tồn tại. Đánh đổi chấp nhận: ai biết email cũng làm khóa được tài khoản đó 10 phút bằng 5 lần đăng nhập sai. Gửi song song nhiều lần sai có thể nhận 409 (concurrency token trên `failed_login_count`), client cứ thử lại | Hiếu | Chưa có rate limiting cho `api/public/auth/login` |
| B8 | **Xóa tài khoản**: đặt `account_status = Inactive`, giữ đơn và lịch sử, không xóa dòng | Hiếu | — |

## 4. Nội dung, hỗ trợ, báo cáo, thông báo

| # | Quyết định | Người làm | Việc phát sinh trong code |
|---|---|---|---|
| B13 | **Nội dung chính sách**: thêm cột `description` (TEXT) vào `master_data_items` | Hoàng (MasterData) | **Đã có** cột; Admin nhập nội dung qua UC-ADM-15 (seed để trống) |
| B14 | **Hỗ trợ khách**: thêm bảng `support_requests` (tên, liên hệ, nội dung, trạng thái). Dispatcher xử lý ngoài hệ thống (gọi điện, nhắn tin) | Hiếu | **Đã có** entity + bảng; còn endpoint gửi yêu cầu (Phase 1) |
| B15 | **Hóa đơn điện tử, hoàn tiền tự động**: bỏ khỏi phạm vi | — | — |
| B16 | **Xuất báo cáo**: file **Excel** | Hoàng | Cần thêm một package Excel (vd ClosedXML); xin nhóm duyệt khi bắt đầu |
| B17 | **Thông báo đẩy**: chỉ gửi FCM, không lưu lịch sử | Hiếu, Hoàng | — |

## 5. Hạ tầng và quy ước

| # | Quyết định |
|---|---|
| C1 | Mỗi aggregate cần `Include` hay truy vấn riêng có `I<Name>Repository` (mẫu: `IUserRepository`); người sở hữu module tự thêm |
| C2 | **Không** thêm test với DB thật (Testcontainers). Test domain, validator, handler với fake như `LoginTests` |
| C3 | Khóa sandbox: Hoàng tạo PayOS + FCM; Hiếu tạo Google Maps + Cloudinary + Gmail. Gửi qua kênh riêng của nhóm, không đưa lên repo |
| C5 | API: `?page=1&pageSize=20` → `PagedResult<T>`; thời gian ISO 8601 UTC; tiền là số VND; enum là chuỗi (đã cấu hình) |
| C7 | Chưa triển khai production; migration tự chạy ở Development là đủ |
| C8 | **Tham số: setting hay hằng số.** Admin sửa được, đọc từ `platform_settings`: `COMMISSION_RATE`, `LOADING_FEE`, `MIN_COD_DEPOSIT`, `MATCHING_RADIUS_KM` (domain nhận giá trị qua tham số, vd `DriverWallet.CanReceiveCod(minDeposit)`). Cố định trong `Domain/Constants/BusinessRules.cs`: mốc thời gian (PayOS 10 phút, ghép đơn 10 phút, tự hoàn tất 24h, khiếu nại 24h, GPS 30 ngày) và giới hạn OTP/đăng nhập/ảnh. Không để cùng một giá trị ở cả hai chỗ |
| C9 | **DB khớp Report-4**: collation `utf8mb4_unicode_ci`; DEFAULT ở mức DB khai báo bằng SQL trong migration, **không** dùng `HasDefaultValue` trong model (EF sẽ bỏ qua `false`/`0` khi INSERT). Ai thêm cột có DEFAULT thì làm theo mẫu trong migration `AlignSchemaWithSds`. **Ai sửa (AlterColumn) một cột đang có DEFAULT** phải `SET DEFAULT` lại trong cùng migration: EF sinh `MODIFY COLUMN` không kèm DEFAULT nên sẽ xóa mất. Dọn DB local đã seed trước 21/10: `DELETE FROM platform_settings WHERE setting_key IN ('PEAK_SURCHARGE','BULKY_SURCHARGE','MATCHING_TIMEOUT_MINUTES','PAYOS_SESSION_MINUTES','AUTO_COMPLETE_HOURS','COMPLAINT_WINDOW_HOURS');` |

## Còn cần làm rõ (hỏi nhóm trước khi code phần này)

1. **B3, cửa sổ hủy miễn phí**: "trong vòng 5–15 phút" là bao nhiêu phút chính xác (5, 10 hay 15)? Và nếu đã có tài xế nhận, tài xế **chưa** đến điểm lấy, nhưng khách hủy **sau** cửa sổ đó thì hoàn bao nhiêu?
2. **B14, hỗ trợ khách**: Dispatcher xử lý ngoài hệ thống, vậy có cần màn hình/endpoint cho Dispatcher xem danh sách `support_requests` không?
3. **B4, coupon với tiền tài xế nhận**: nền tảng chịu coupon, nên tài xế phải được tính trên `distance_fee + service_fee`, không phải `total_fee`. Đơn **PayOS**: khi cộng thu nhập, truyền `fare = distance_fee + service_fee` (không dùng `total_fee`, nếu không đơn có coupon lớn sẽ lỗi "commission > fare"). Đơn **tiền mặt**: tài xế chỉ thu `total_fee` từ khách, vậy phần `discount_amount` nền tảng bù cho tài xế bằng cách nào: cộng vào ví thu nhập, hay trừ bớt vào hoa hồng lấy từ ví cọc?
