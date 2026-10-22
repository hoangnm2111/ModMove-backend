# Quyết định thiết kế Backend

Đây là các quyết định nhóm đã chốt (bản cuối, 2026-10-23). Khi SRS/SDS khác file này, **làm theo file này**. Muốn đổi một quyết định: sửa file này trong một PR riêng, cả nhóm duyệt.

- **Thiết kế DB:** Report-4 mục 3 chỉ là bản thiết kế ban đầu. Nguồn sự thật của schema là **migration trong code** cùng file này. Đổi schema thì ghi vào [REPORT4-CHANGES.md](REPORT4-CHANGES.md).
- **Mã quyết định:** `B*`, `C*` là từ các buổi họp trước; `M*` và `1.x`–`9.x` là số mục trong tài liệu "Các vấn đề cần nhóm thống nhất".
- **"Đã làm"** ở cột cuối nghĩa là code trên `develop` hoặc `feature/hieu` đã theo quyết định, có test.
- Mục [Còn cần làm rõ](#còn-cần-làm-rõ) ở cuối liệt kê chỗ quyết định còn mâu thuẫn. Đừng tự đoán: hỏi nhóm trước khi code phần đó.

## 1. Giá, hoa hồng, coupon

| # | Quyết định | Trạng thái |
|---|---|---|
| B1 | **Giá đơn** = `max(minimum_fare, base_fare + price_per_km × km)` + phí bốc xếp (`LOADING_FEE`, nếu chọn) − coupon = `total_fee`. Giá nằm ở `vehicle_types` và `platform_settings`, không có entity `FareRule` | Chưa làm (Hiếu, `FareCalculator`) |
| B2 | Không có giờ cao điểm, không có phụ phí cồng kềnh | Đã làm (seed) |
| 4.4 | Giá chốt lúc đặt đơn theo khoảng cách Google Maps, không tính lại theo quãng đường thực tế | — |
| 4.6 | Không giới hạn phạm vi phục vụ hay số km | — |
| M1, M2, B4 | **Tài xế chịu coupon.** Khi đơn hoàn tất: `commission_amount = commission_rate × total_fee` (đã trừ coupon, làm tròn đến đồng); tài xế nhận `total_fee − commission_amount`. Cùng công thức cho đơn PayOS và tiền mặt. Đổi tỷ lệ không ảnh hưởng đơn cũ | Đã làm (`ShippingOrder.Complete`, `DriverEarning`) |
| GB-15 | Đơn tiền mặt: hoa hồng trừ vào ví cọc của tài xế | Đã làm (`DriverWallet.DeductCommissionFromDeposit`) |
| B11 | `used_count` của coupon tăng khi tạo đơn thành công, cùng transaction; hủy đơn không hoàn lượt | Chưa làm (Hiếu, tạo đơn) |

## 2. Đặt đơn, hủy đơn, hoàn tiền

| # | Quyết định | Trạng thái |
|---|---|---|
| 4.1 | **Không sửa đơn** (bỏ UC-CUS-28): muốn đổi thì hủy rồi tạo lại | — |
| 4.2 | `weight_kg` = tổng khối lượng của cả đơn (xem [Còn cần làm rõ](#còn-cần-làm-rõ) #2 về kích thước) | Chưa làm |
| 4.3 | Khách tick cam kết không chở hàng cấm khi đặt đơn. Tài xế thấy hàng cấm lúc lấy thì báo sự cố; Dispatcher hủy đơn, **không hoàn tiền** | Chưa làm |
| 4.5 | Không có đặt lịch trước | — |
| 2.1, 2.2, B3 | Khách **tự hủy chỉ khi `Pending` hoặc `Accepted`**, miễn phí, không giới hạn thời gian; đơn PayOS đã trả được hoàn 100%. Từ `Arrived` trở đi khách không tự hủy: báo sự cố để Dispatcher xử lý (GB-17), không hoàn tiền | Đã làm (`ShippingOrder.CancelByCustomer`) |
| 2.3, M3 | Không có ví khách. Đơn tiền mặt bị hủy sau khi tài xế đã đến: không thu được, không xử lý trong hệ thống. Đơn PayOS: không hoàn tiền đã trả | — |
| 2.4, 2.7 | Tới điểm giao mà **không có người nhận**: tài xế để hàng tại điểm giao, gửi ảnh POD, đơn chuyển `Delivered`. Không có đếm ngược 5 phút, không có trạng thái "khách vắng mặt" (bỏ mục B7/B18 cũ) | Đã khớp (dùng `Deliver` sẵn có) |
| 2.6 | Hàng còn trên xe sau khi hủy: tài xế báo sự cố; Dispatcher quyết định trả về điểm lấy hay giữ lại. Không tính thêm phí quay về | Chưa làm (Huy) |
| 2.8 | Phân công: Hoàng làm trạng thái chuyến và job; Hiếu làm thông báo và phản hồi của khách; Huy làm phần Dispatcher xử lý | — |
| 5.3 | Đơn tiền mặt người nhận trả mà người nhận không trả: người gửi trả thay cho tài xế; tranh chấp chuyển Dispatcher. Tài xế không tự bỏ tiền, giữ hàng và bằng chứng chờ hướng dẫn | Chưa làm |
| B9, 3.3 | Hoàn tiền do **hủy**: tự động tạo với số tiền tính theo 2.1, chuyển thẳng Admin duyệt và chi trả tay (GB-27), không qua Dispatcher. Hoàn tiền do **sự cố/khiếu nại**: khách nhập số tiền yêu cầu, Dispatcher đề xuất `proposed_amount ≤ requested_amount`, Admin duyệt và chi trả tay. Mỗi đơn chỉ một khiếu nại mở (kiểm tra trong handler) | Một phần (entity `Refund`) |

## 3. Thanh toán và ví tài xế

| # | Quyết định | Trạng thái |
|---|---|---|
| B10 | Đơn tạo ra ở `Pending`, `PaymentStatus = Unpaid`. Đơn **PayOS** chỉ hiện cho tài xế khi đã `Paid`; đơn **tiền mặt** hiện ngay | Chưa làm (Hoàng) |
| 3.1 | Phiên PayOS sống 10 phút (GB-13). Hết hạn mà chưa trả: app báo khách tạo mã mới cho **cùng đơn**; mã cũ không nhận chuyển khoản nữa | Chưa làm (Hoàng); xem [Còn cần làm rõ](#còn-cần-làm-rõ) #5 |
| 3.2 | Thời gian ghép đơn 10 phút (GB-24): đơn tiền mặt tính **từ lúc tạo**, đơn PayOS **từ lúc thanh toán xong** (`shipping_orders.paid_at`) | Đã làm (`ShippingOrder.IsMatchingExpired`, `MarkPaid`) |
| 5.1 | Rút tiền: **không** giới hạn tối thiểu | Đã làm |
| 5.2 | Nạp cọc: **tối thiểu 50.000đ** mỗi lần | Đã làm (`DriverWallet.TopUpDeposit`, code `TOP_UP_TOO_SMALL`) |
| GB-16 | Nhận đơn tiền mặt cần ví cọc ≥ `MIN_COD_DEPOSIT` (200.000đ) | Đã làm (`DriverWallet.CanReceiveCod`) |

## 4. Ghép đơn và chuyến đi

| # | Quyết định | Trạng thái |
|---|---|---|
| B5 | Tài xế Online + Approved, không có đơn đang chạy (GB-09), đúng loại xe; đơn trong bán kính `MATCHING_RADIUS_KM` (5 km) quanh vị trí cuối, sắp theo khoảng cách | Chưa làm (Hoàng); key đã có trong seed |
| 6.1 | Tài xế đã nhận đơn **không tự hủy**: báo sự cố, Dispatcher đổi tài xế hoặc hủy (GB-17) | — |
| 6.2 | Chứng từ giao hàng chỉ có ảnh + thời điểm (GB-12), không có chữ ký | Đã khớp |
| 6.3 | Khách từ chối ảnh lấy hàng: tài xế chụp lại; **tối đa 5 lần gửi ảnh** tính cả lần đầu, sau đó báo sự cố | Đã làm (`PICKUP_PHOTO_LIMIT`) |
| 6.4 | Quá 5 phút không gửi GPS: coi là mất kết nối, không hiện trên bản đồ Dispatcher và không thấy đơn mới; đang có đơn thì giữ nguyên | Chưa làm (Hoàng) |
| 7.2 | Đổi tài xế giữa chuyến: tài xế mới nhận toàn bộ thu nhập; Dispatcher chọn từ danh sách Online, đúng loại xe, không có đơn | Một phần (`ReassignDriver`) |
| B12 | Realtime: hub `/hubs/tracking`, group `order-{id}`; client gửi `SendLocation(lat, lng)`, server phát `LocationUpdated` và `OrderStatusChanged`; JWT qua `access_token` trên query string. Chỉ ghi `driver_location_logs` khi đang có đơn | Chưa làm |

## 5. Sự cố, khiếu nại, đánh giá

| # | Quyết định | Trạng thái |
|---|---|---|
| 7.1 | Không thêm loại sự cố: "trễ", "sai tuyến" ghi là `Other` | — |
| 7.3 | Khiếu nại chỉ nhận khi đơn đang `Delivered` và trong 24 giờ từ `delivered_at`; sau 24 giờ đơn tự `Completed` (GB-23) và không nhận khiếu nại nữa | Đã làm (`ShippingOrder.CanReceiveComplaint`) |
| 7.4 | Dispatcher xin thêm thông tin: khách trả lời bằng một đoạn chữ (`refunds.customer_reply`) và thêm ảnh vào `evidence_urls` (tối đa 5 ảnh) | Đã làm (`Refund.ReplyToInfoRequest`) |
| 7.5 | Đánh giá chỉ trong **1 ngày** sau khi đơn `Completed` (GB-20: một lần, không sửa). Điểm trung bình tính khi truy vấn | Đã làm (`Rating.Create`) |
| 2.9, B14 | Khách gửi yêu cầu hỗ trợ (bảng `support_requests`); **không có màn hình Dispatcher**, Dispatcher xử lý ngoài hệ thống | Đã làm (gửi yêu cầu) |

## 6. Tài khoản, đăng nhập

| # | Quyết định | Trạng thái |
|---|---|---|
| B6 | Web chỉ đăng ký **Customer** (`register`); **Driver** chỉ đăng ký trên app (`register-driver`). Backend không nhận `role` từ client. Dispatcher/Admin không tự đăng ký | Đã làm |
| B7 | Admin tạo tài khoản và mật khẩu cố định cho Dispatcher | Chưa làm (Hoàng) |
| D1 | Chưa xác minh email bằng OTP thì không đăng nhập được (403 `EMAIL_NOT_VERIFIED`) | Đã làm |
| A-01 | Sai email, sai mật khẩu, tài khoản đang khóa tạm (GB-02): **cùng một** lỗi 401 để không lộ email. Đánh đổi chấp nhận: ai biết email có thể làm khóa tài khoản đó 10 phút | Đã làm |
| A-02 | OTP: 6 số, sống 5 phút, gửi tối đa 3 lần/15 phút (GB-25), chết sau 5 lần nhập sai, dùng một lần. Gửi lại/quên mật khẩu luôn 204. Giới hạn 10 request/phút/IP cho `api/public/auth/*`, đổi mật khẩu và yêu cầu hỗ trợ (429 `TOO_MANY_REQUESTS`). Số điện thoại không được xác minh | Đã làm |
| 9.1 | **Refresh token 30 ngày**: lưu dạng hash (bảng `refresh_tokens`), dùng một lần rồi đổi token mới (`POST api/public/auth/refresh`); token cũ dùng lại = bị lộ, thu hồi hết. **Đổi/đặt lại mật khẩu, xóa tài khoản, đăng xuất thu hồi hết.** Admin khóa tài khoản cũng phải gọi `IRefreshTokenRepository.RevokeAllForUserAsync` | Đã làm (trừ phần Admin khóa: Hoàng) |
| 9.2 | Một tài khoản, một thiết bị nhận thông báo (FCM token thuộc máy đăng ký sau cùng) | Đã làm |
| B8, 8.2 | Xóa tài khoản = `Inactive`, giữ đơn và lịch sử; Admin có thể mở lại. `Locked` = Admin khóa, chỉ Admin mở. Cả hai không đăng nhập và không dùng token cũ được | Đã làm (xem [Còn cần làm rõ](#còn-cần-làm-rõ) #4) |

## 7. Admin, báo cáo, nội dung, thông báo

| # | Quyết định | Trạng thái |
|---|---|---|
| 8.1 | Doanh thu nền tảng = tổng `commission_amount` của đơn `Completed`, theo ngày giờ Việt Nam | Chưa làm (Hoàng); xem [Còn cần làm rõ](#còn-cần-làm-rõ) #3 |
| B13 | Nội dung chính sách và giới thiệu dịch vụ: cột `master_data_items.description`, Admin nhập | Đã làm (cột, seed, endpoint đọc) |
| B15 | Hóa đơn điện tử, hoàn tiền tự động: ngoài phạm vi | — |
| B16 | Xuất báo cáo Excel (cần xin duyệt một package Excel khi bắt đầu) | Chưa làm (Hoàng) |
| B17, 8.3 | Chỉ gửi FCM, không lưu lịch sử. Sự kiện tối thiểu cho **khách**: đơn được nhận, tài xế đến điểm lấy, ảnh lấy hàng chờ duyệt, đã giao, đã hoàn thành, kết quả sự cố, kết quả hoàn tiền. Cho **tài xế**: đơn mới gần, quyết định sự cố, kết quả duyệt hồ sơ, kết quả rút tiền. Ai đổi trạng thái thì gửi thông báo đó | Chưa làm |

## 8. Hạ tầng và quy ước

| # | Quyết định |
|---|---|
| C1 | Aggregate cần `Include` hay truy vấn riêng có `I<Name>Repository` (mẫu: `IUserRepository`); người sở hữu module tự thêm |
| C2 | Không test với DB thật. Test domain, validator và handler với fake (`tests/ModMove.IntegrationTests/Fakes.cs`) |
| C3, 3.4 | Khóa sandbox: Hoàng tạo PayOS + FCM; Hiếu tạo Google Maps (Hiếu đứng tên tài khoản thanh toán; dùng Routes API nếu project mới không bật được Distance Matrix) + Cloudinary + Gmail. Gửi qua kênh riêng, không lên repo |
| C5 | `?page=1&pageSize=20` → `PagedResult<T>`; thời gian ISO 8601 UTC; tiền là số VND; enum là chuỗi |
| C7, 9.3 | Chạy và test đủ các luồng ở **local**; chưa đưa lên production. Migration tự chạy ở Development |
| C8 | Admin sửa được (`platform_settings`): `COMMISSION_RATE`, `LOADING_FEE`, `MIN_COD_DEPOSIT`, `MATCHING_RADIUS_KM`. Cố định trong `BusinessRules.cs`: mốc thời gian, giới hạn OTP/đăng nhập/ảnh, nạp cọc tối thiểu, cửa sổ đánh giá. Không để một giá trị ở cả hai chỗ |
| C9 | Collation `utf8mb4_unicode_ci`; DEFAULT ở mức DB viết bằng SQL trong migration, không dùng `HasDefaultValue`. Ai `AlterColumn` một cột có DEFAULT phải `SET DEFAULT` lại trong cùng migration |
| 9.4 | API trả `detail` tiếng Anh kèm `code` ổn định. Web hiển thị tiếng Anh (có tùy chọn tiếng Việt), app hiển thị tiếng Việt theo `code`. Bảng `code` ở [api/README.md](api/README.md) |
| 9.5 | Lưu UTC; ngày trong báo cáo, mã đơn và hiển thị theo giờ Việt Nam (UTC+7) |

## Còn cần làm rõ

Các chỗ dưới đây quyết định còn mâu thuẫn. Code **chưa** làm theo bên nào; hỏi nhóm trước khi code phần đó.

1. **2.5 và 2.7, giao hàng không có người nhận.** 2.7 nói tài xế để hàng lại, đơn `Delivered`. 2.5 lại ghi "hệ thống hủy đơn hoàn lại 100% số tiền cho customer và hàng vẫn giao đến nơi". 2.5 áp cho trường hợp nào (câu hỏi gốc là "không có tài xế thay thế")? Nếu hủy và hoàn 100% thì tài xế có được trả tiền không?
2. **4.2, kích thước hàng.** Câu trả lời ghi cả "dài × rộng × cao = món lớn nhất" và "kích thước hàng là của cả đơn". Kiểm tra lọt thùng xe (GB-10) dùng kích thước nào?
3. **8.1 và M1, doanh thu.** 8.1 ghi "đã trừ tiền coupon mà nền tảng phải chịu", nhưng M1 chốt tài xế chịu coupon, nên nền tảng không chịu gì. Doanh thu là tổng hoa hồng (đề xuất), hay còn trừ gì khác?
4. **8.2, khóa tài khoản.** "Locked = Admin khóa và khóa 10 phút sau 5 lần đăng nhập sai, chỉ Admin mở." Hiện code tách hai loại: khóa tạm GB-02 tự mở sau 10 phút (`locked_until`), còn `Locked` do Admin khóa chỉ Admin mở. Giữ như vậy, hay 5 lần sai cũng phải chờ Admin mở?
5. **3.1, đơn PayOS không bao giờ trả tiền.** Khách được tạo mã mới mãi, nhưng đơn chưa trả thì không hiện cho tài xế và không hết hạn ghép đơn (3.2). Đơn đó tự hủy sau bao lâu?
6. **2.4, khách vắng mặt tại điểm lấy hàng.** "Xử lý như 2.3" nghĩa là đơn bị hủy, không thu được tiền mặt. Ai bấm hủy (tài xế báo sự cố rồi Dispatcher hủy?), sau bao lâu chờ, và đơn PayOS có hoàn tiền không?
