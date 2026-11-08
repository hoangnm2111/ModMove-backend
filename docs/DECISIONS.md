# Quyết định thiết kế Backend

Đây là các quyết định nhóm đã chốt (bản cuối 2026-10-24, cập nhật kênh client và thông báo 2026-10-30, phía khách 2026-11-02). Khi SRS/SDS khác file này, **làm theo file này**. Muốn đổi một quyết định: sửa file này trong một PR riêng, cả nhóm duyệt.

- **Thiết kế DB:** Report-4 mục 3 chỉ là bản thiết kế ban đầu. Nguồn sự thật của schema là **migration trong code** cùng file này.
- **Mã quyết định:** `B*`, `C*` là từ các buổi họp trước; `M*` và `1.x`–`9.x` là số mục trong tài liệu "Các vấn đề cần nhóm thống nhất".
- File này chỉ ghi quyết định. Tiến độ (đã làm hay chưa, ai làm) theo dõi duy nhất ở [TEAM-PLAN.md](TEAM-PLAN.md).
- Gặp chỗ chưa có quyết định: đừng tự đoán, hỏi nhóm rồi thêm vào file này trước khi code phần đó.

## 1. Giá, hoa hồng, coupon

| # | Quyết định |
|---|---|
| B1 | **Giá đơn** = `max(minimum_fare, base_fare + price_per_km × km)` + phí bốc xếp (`LOADING_FEE`, nếu chọn) − coupon = `total_fee`. Giá nằm ở `vehicle_types` và `platform_settings`, không có entity `FareRule` |
| B2 | Không có giờ cao điểm, không có phụ phí cồng kềnh |
| 4.4 | Giá chốt lúc đặt đơn theo khoảng cách MapVina, không tính lại theo quãng đường thực tế. Tạo đơn gửi kèm `expectedTotalFee`; giá server khác số này thì trả 422 `FARE_CHANGED`, không tạo đơn và không tính lượt coupon (2026-11-02) |
| 4.6 | Không giới hạn phạm vi phục vụ hay số km |
| M1, M2, B4 | **Tài xế chịu coupon.** Khi đơn hoàn tất: `commission_amount = commission_rate × total_fee` (đã trừ coupon, làm tròn đến đồng); tài xế nhận `total_fee − commission_amount`. Cùng công thức cho đơn PayOS và tiền mặt. Đổi tỷ lệ không ảnh hưởng đơn cũ |
| GB-15 | Đơn tiền mặt: hoa hồng trừ vào ví cọc của tài xế. **Ví tài xế không bao giờ chặn khách:** đơn đã giao thì vẫn hoàn tất, ví cọc thiếu thì được âm, ví bị khóa vẫn được tất toán (2026-11-02) |
| B11 | `used_count` của coupon tăng khi tạo đơn thành công, cùng transaction; hủy đơn không hoàn lượt |

## 2. Đặt đơn, hủy đơn, hoàn tiền

| # | Quyết định |
|---|---|
| 4.1 | **Không sửa đơn** (bỏ UC-CUS-28): muốn đổi thì hủy rồi tạo lại |
| 4.2, GB-10 | `weight_kg` = tổng khối lượng của cả đơn, phải ≤ `vehicle_types.max_load_weight_kg`. Dài × rộng × cao khách nhập là của **món lớn nhất**. Hàng lọt thùng (cho phép xoay hàng) khi xếp 3 cạnh của món đó và 3 cạnh thùng xe (`cargo_bed_*_cm`) từ nhỏ đến lớn rồi so từng cặp: cạnh nhỏ nhất ≤ cạnh nhỏ nhất của thùng, cạnh giữa ≤ cạnh giữa, cạnh lớn nhất ≤ cạnh lớn nhất |
| 4.3 | Khách tick cam kết không chở hàng cấm khi đặt đơn. Tài xế thấy hàng cấm lúc lấy thì báo sự cố; Dispatcher hủy đơn, **không hoàn tiền** |
| 4.5 | Không có đặt lịch trước |
| 2.1, 2.2, B3 | Khách **tự hủy chỉ khi `Pending` hoặc `Accepted`**, miễn phí, không giới hạn thời gian; đơn PayOS đã trả được hoàn 100%. Từ `Arrived` trở đi khách không tự hủy: báo sự cố để Dispatcher xử lý (GB-17), không hoàn tiền. **Khách báo sự cố** (UC-CUS-15) khi đơn của mình đang `Arrived`, `PickedUp` hoặc `InTransit`; sau khi giao thì dùng khiếu nại (7.3) (2026-10-31) |
| 2.3, M3 | Không có ví khách. Đơn tiền mặt bị hủy sau khi tài xế đã đến: không thu được, không xử lý trong hệ thống. Đơn PayOS: không hoàn tiền đã trả |
| 2.4 (điểm lấy) | **Khách vắng mặt ở điểm lấy hàng:** tài xế chờ 15 phút kể từ `Arrived`; không liên hệ được khách thì báo sự cố `Other` kèm ảnh tại điểm lấy. Dispatcher hủy đơn (UC-DIS-07). Đơn tiền mặt: không thu được. Đơn PayOS: **không hoàn tiền** (theo 2.1, 2.3) |
| 2.4, 2.7 (điểm giao) | Tới điểm giao mà **không có người nhận**: tài xế để hàng tại điểm giao, gửi ảnh POD, đơn chuyển `Delivered`. Không có đếm ngược 5 phút, không có trạng thái "khách vắng mặt" (bỏ mục B7/B18 cũ) |
| 2.5 | Chỉ áp cho **sự cố giữa đường mà Dispatcher không tìm được tài xế thay thế**: Dispatcher hủy đơn; đơn PayOS được hoàn 100%; đơn tiền mặt xử lý ngoài hệ thống; tài xế cũ không nhận tiền chuyến đó; hàng trên xe xử lý theo 2.6. Không dùng cho trường hợp không có người nhận (xem 2.7) |
| 2.6 | Hàng còn trên xe sau khi hủy: tài xế báo sự cố; Dispatcher quyết định trả về điểm lấy hay giữ lại. Không tính thêm phí quay về |
| 2.8 | Phân công: Hoàng làm trạng thái chuyến và job; Hiếu làm thông báo và phản hồi của khách; Huy làm phần Dispatcher xử lý |
| 5.3 | Đơn tiền mặt người nhận trả mà người nhận không trả: người gửi trả thay cho tài xế; tranh chấp chuyển Dispatcher. Tài xế không tự bỏ tiền, giữ hàng và bằng chứng chờ hướng dẫn |
| B9, 3.3 | Hoàn tiền do **hủy**: tự động tạo với số tiền tính theo 2.1, chuyển thẳng Admin duyệt và chi trả tay (GB-27), không qua Dispatcher: khoản hoàn được tạo với `requested_amount = proposed_amount` = số tiền khách đã trả (100% theo 2.1). Hoàn tiền do **sự cố/khiếu nại**: khách nhập số tiền yêu cầu, Dispatcher đề xuất `proposed_amount ≤ requested_amount`, Admin duyệt và chi trả tay. Mỗi đơn chỉ một khiếu nại mở (kiểm tra trong handler). **Số tiền khách yêu cầu: > 0 và ≤ `total_fee` của đơn** (bồi thường lớn hơn xử lý ngoài hệ thống). **Tổng mọi khoản hoàn của một đơn không vượt `total_fee`** (`REFUND_TOTAL_EXCEEDED`): tính khoản đang mở hoặc đã chi, theo số Dispatcher đề xuất nếu đã có, không tính khoản `Rejected`, `Failed`, `Withdrawn` (2026-11-02). **Hoàn tiền do sự cố** (UC-CUS-17): chỉ cho đơn PayOS đã `Paid` (`REFUND_NOT_ALLOWED`, 2026-11-02); khách gửi cho sự cố thuộc đơn của mình, ở bất kỳ trạng thái sự cố nào; mỗi sự cố chỉ một yêu cầu hoàn đang mở. "Mở" = `Requested` hoặc `Approved`. Khách rút được khiếu nại/hoàn sự cố khi còn `Requested`; hoàn do hủy thì không rút (2026-10-31) |

## 3. Thanh toán và ví tài xế

| # | Quyết định |
|---|---|
| B10 | Đơn tạo ra ở `Pending`, `PaymentStatus = Unpaid` (đã thử đổi tên thành `PlaceOrder` rồi bỏ, 2026-11-03: API chỉ dùng `Pending`). Đơn **PayOS** chỉ hiện cho tài xế khi đã `Paid`; đơn **tiền mặt** hiện ngay |
| 3.1 | Phiên PayOS sống 10 phút (GB-13), nội dung chuyển khoản ≤ 9 ký tự (`MM{orderId}` hoặc `COC{walletId}`) để mọi ngân hàng đều tạo được link. Hết hạn mà chưa trả: web/app hiển thị MSG50 và nút tạo mã mới cho **cùng đơn**; mã cũ bị hủy trên DB và tại PayOS. Đơn PayOS vẫn `Unpaid` sau **30 phút** kể từ `created_at` thì tự hủy (JOB-PAY-01) và hủy luôn mọi phiên PayOS còn `Pending` của đơn đó trên cả DB lẫn PayOS; khi khách tự hủy đơn (`CancelOrder`) cũng hủy ngay phiên PayOS đang mở.<br>Nếu tiền thật vẫn về qua webhook hợp lệ cho mã đã `Expired`/`Cancelled`: **đơn còn mở thì vẫn nhận tiền và chuyển `Paid`**; **đơn đã `Cancelled` (hoặc thanh toán trùng lần 2) thì ghi nhận giao dịch `Paid` và tự động tạo yêu cầu hoàn tiền `Cancellation` 100% trình Admin duyệt** (không bao giờ nuốt tiền của khách).<br>`CreateOrderPayment` và `TopUpDeposit` nhận `platform` (`Web` \| `App`); server chọn `returnUrl`/`cancelUrl` từ cấu hình (`PayOS:ReturnUrls:Web` là trang chi tiết đơn, `PayOS:ReturnUrls:App` là deep link). **Client không gửi URL** (tránh open redirect). Trạng thái thanh toán **chỉ tin webhook**, không tin việc PayOS chuyển hướng về `returnUrl` |
| 3.2 | Thời gian ghép đơn 10 phút (GB-24): đơn tiền mặt tính **từ lúc tạo**, đơn PayOS **từ lúc thanh toán xong** (`shipping_orders.paid_at`) |
| 5.1 | Rút tiền: **không** giới hạn tối thiểu. Ví cọc đang âm thì phần nợ được trừ vào thu nhập trước, ví cọc về 0 và có giao dịch `CommissionDebtPaid`; thu nhập còn lại không đủ số muốn rút thì từ chối và không trừ gì. Yêu cầu rút bị từ chối chỉ hoàn số đã xin rút, nợ đã trả không hoàn lại (2026-11-02) |
| 5.2 | Nạp cọc: **tối thiểu 50.000đ** mỗi lần |
| GB-16 | Nhận đơn tiền mặt cần ví cọc ≥ `MIN_COD_DEPOSIT` (200.000đ) **và** ≥ hoa hồng của chính đơn đó (tính như M1: `COMMISSION_RATE × total_fee`, làm tròn đến đồng). Điều kiện này áp cả cho tài xế mới khi đổi tài xế (7.2). Ví cọc chỉ quyết định tài xế có xem và nhận được đơn tiền mặt hay không; ví âm thì không nhận được cho tới khi nạp thêm (GB-15) |

## 4. Ghép đơn và chuyến đi

| # | Quyết định |
|---|---|
| B5 | Tài xế Online + Approved, không có đơn đang chạy (GB-09), đúng loại xe; đơn trong bán kính `MATCHING_RADIUS_KM` (5 km) quanh vị trí cuối, sắp theo khoảng cách |
| 6.1 | Tài xế đã nhận đơn **không tự hủy**: báo sự cố, Dispatcher đổi tài xế hoặc hủy (GB-17) |
| 6.2 | Chứng từ giao hàng chỉ có ảnh + thời điểm (GB-12), không có chữ ký |
| 6.3 | Khách từ chối ảnh lấy hàng: tài xế chụp lại; **tối đa 5 lần gửi ảnh** tính cả lần đầu, sau đó báo sự cố |
| 6.4 | Từ 5 phút trở lên không gửi GPS (đủ 5 phút cũng tính): coi là mất kết nối, không hiện trên bản đồ Dispatcher và không thấy đơn mới; đang có đơn thì giữ nguyên |
| 7.2 | Đổi tài xế giữa chuyến: tài xế mới nhận toàn bộ thu nhập; Dispatcher chỉ chọn được tài xế đủ **mọi điều kiện như tài xế nhận đơn lần đầu** (B5: Online, Approved, đúng loại xe, không có đơn; đơn tiền mặt thêm GB-16). Đơn quay về `Accepted`: tài xế mới đến chỗ xe cũ nhận hàng (`Arrived`), kiểm hàng và gửi lại ảnh lấy hàng (`PickedUp`), với lượt đếm 5 lần gửi ảnh mới (6.3) |
| B12 | Realtime: hub `/hubs/tracking`, JWT qua `access_token` trên query string, từ chối user không `Active`.<br>**Khách (web/app):** gọi `JoinOrder(orderId)` khi mở màn hình theo dõi; server kiểm tra đơn là của khách đó, không phải thì lỗi `ORDER_NOT_YOURS` (C10). Kết nối lại thì client gọi lại `JoinOrder`.<br>**Tài xế (app):** gửi `SendLocation(lat, lng)`; server cập nhật vị trí cuối, ghi `driver_location_logs` chỉ khi đang có đơn, phát `LocationUpdated` tới `order-{id}` và group `dispatchers`. Tài xế không join group đơn; trạng thái đơn tới tài xế qua `Clients.User`.<br>**Dispatcher (web):** server tự thêm vào group `dispatchers` khi kết nối, theo role. Bản đồ lấy danh sách ban đầu bằng REST (tài xế Online có GPS trong 5 phút, 6.4), sau đó cập nhật bằng `LocationUpdated`. Dispatcher thấy **mọi tài xế Online**, không chỉ tài xế đang có đơn.<br>**Sự kiện server phát:** `LocationUpdated`; `OrderStatusChanged` (tới `order-{id}` và `Clients.User` của tài xế); `NotificationReceived` (B17) |

## 5. Sự cố, khiếu nại, đánh giá

| # | Quyết định |
|---|---|
| 7.1 | Không thêm loại sự cố: "trễ", "sai tuyến", "khách vắng mặt" ghi là `Other` |
| 7.3 | Khiếu nại chỉ nhận khi đơn đang `Delivered` và trong 24 giờ từ `delivered_at`; sau 24 giờ đơn tự `Completed` (GB-23) và không nhận khiếu nại nữa. **Khiếu nại còn mở (`Requested`, `Approved`) thì đơn chưa hoàn tất:** khách xác nhận nhận hàng nhận `COMPLAINT_OPEN`, JOB-ORDER-01 bỏ qua đơn đó, tài xế chưa được tất toán; đơn hoàn tất sau khi khiếu nại đóng (2026-11-02) |
| 7.4 | Dispatcher xin thêm thông tin: khách trả lời bằng một đoạn chữ (`refunds.customer_reply`) và thêm ảnh vào `evidence_urls` (tối đa 5 ảnh) |
| 7.5 | Đánh giá chỉ trong **1 ngày** sau khi đơn `Completed` (GB-20: một lần, không sửa). Điểm trung bình tính khi truy vấn. Đánh giá 1–2 sao như mọi đánh giá khác: số sao và nhận xét, không có tag hay trường bắt buộc thêm (2026-11-02) |
| CUS-23 | Khách thấy lý do của Admin (`refunds.decision_note`, trả về là `decisionReason`) khi Admin đã quyết định. `proposal_reason` của Dispatcher chỉ là ghi chú nội bộ, không trả về cho khách. `Refund.Approve`/`RejectRefund` nhận tham số `note`, **không bắt buộc**: Admin không ghi thì `decisionReason` là `null` (2026-11-02) |
| Danh mục | `cargoType`, lý do hủy và lý do khiếu nại gửi bằng **mã** master data (`CARGO_TYPE`, `CANCELLATION_REASON`, `COMPLAINT_REASON`); web và app lấy danh sách từ `api/public/lookups/...`. Mã lạ hoặc đã tắt: 400 `VALIDATION`. Lý do hủy và lý do khiếu nại chọn `OTHER` thì phải viết lý do (2026-11-02) |
| 2.9, B14 | Khách gửi yêu cầu hỗ trợ (bảng `support_requests`); **không có màn hình Dispatcher**, Dispatcher xử lý ngoài hệ thống |

## 6. Tài khoản, đăng nhập

| # | Quyết định |
|---|---|
| B6 | **Customer** đăng ký bằng `register` trên web hoặc app. **Driver** chỉ đăng ký bằng `register-driver` trên app. Dispatcher và Admin không tự đăng ký. Backend không nhận `role` từ client. Kênh client xem C11 |
| B7 | Admin tạo tài khoản và mật khẩu cố định cho Dispatcher |
| D1 | Chưa xác minh email bằng OTP thì không đăng nhập được (403 `EMAIL_NOT_VERIFIED`) |
| A-01 | Sai email, sai mật khẩu, tài khoản đang khóa tạm (GB-02): **cùng một** lỗi 401 để không lộ email. Đánh đổi chấp nhận: ai biết email có thể làm khóa tài khoản đó 10 phút |
| A-02 | OTP: 6 số, sống 5 phút, gửi tối đa 3 lần/15 phút (GB-25), chết sau 5 lần nhập sai, dùng một lần. Gửi lại/quên mật khẩu luôn 204. Gửi mail OTP lỗi thì chỉ ghi log: đăng ký vẫn thành công, người dùng gửi lại bằng `otp/resend` (2026-11-02). **Đăng ký lại email chưa xác minh** với cùng vai trò, tài khoản còn `Active`: cập nhật họ tên, số điện thoại, mật khẩu theo lần mới và gửi OTP mới, tính chung giới hạn GB-25; đã đủ 3 lần thì vẫn trả thành công nhưng không gửi và không đổi gì. Email đã xác minh, tài khoản `Inactive`/`Locked`, khác vai trò hoặc số điện thoại của tài khoản khác vẫn 409 (GB-01) (2026-11-05). **Xác minh email phải kèm đúng mật khẩu đã đăng ký** (`VERIFY_PASSWORD_MISMATCH`, không kích hoạt tài khoản, không làm mất mã): vì đăng ký lại ghi đè thông tin, người biết email có thể đặt mật khẩu của mình lên tài khoản chưa xác minh, nên chỉ OTP thì chưa đủ để chứng minh người xác minh chính là người đặt mật khẩu. Sai hoặc quên mật khẩu thì dùng quên mật khẩu (cũng xác minh email, không tính chung lượt gửi của đăng ký), hoặc đăng ký lại nếu chưa đủ 3 lần gửi. Đánh đổi được chấp nhận: người lạ có thể làm chậm việc xác minh bằng cách đăng ký lại, và họ tên, số điện thoại của họ còn lại trên tài khoản nếu chủ email chọn quên mật khẩu. **Email chỉ nhận ký tự ASCII in được** (400 `VALIDATION`) và mã luôn gửi tới email đã lưu: collation của cột bỏ qua dấu, nên địa chỉ na ná trên tên miền khác sẽ tìm ra tài khoản thật và nhận mã của nó (2026-11-05). Giới hạn 10 request/phút/IP cho `api/public/auth/*`, đổi mật khẩu và yêu cầu hỗ trợ; 10 request/phút cho mỗi user trên từng endpoint với theo dõi đơn và khách báo sự cố, 2026-11-02 (429 `TOO_MANY_REQUESTS`). Số điện thoại không được xác minh |
| 9.1 | **Refresh token 30 ngày**: lưu dạng hash (bảng `refresh_tokens`), dùng một lần rồi đổi token mới (`POST api/public/auth/refresh`); token cũ dùng lại = bị lộ, thu hồi hết. **Đổi/đặt lại mật khẩu, xóa tài khoản, đăng xuất thu hồi hết.** Admin khóa tài khoản cũng phải gọi `IRefreshTokenRepository.RevokeAllForUserAsync` |
| 9.2 | Chỉ app đăng ký FCM token; **web không dùng FCM**. Mỗi tài khoản giữ **một** token (máy đăng ký sau cùng), không cần bảng `device_tokens`. Đăng xuất trên app: app gửi kèm token khi gọi logout, server chỉ xóa token nếu đúng là token đang lưu (máy khác đã đăng ký sau thì giữ nguyên) |
| B8, 8.2 | Xóa tài khoản = `Inactive`, giữ đơn và lịch sử; Admin có thể mở lại. Không xóa được khi còn đơn chưa xong (`ACCOUNT_HAS_OPEN_ORDERS`) hoặc khoản hoàn đang mở (`ACCOUNT_HAS_OPEN_REFUNDS`, 2026-11-02). `Locked` = Admin khóa, chỉ Admin mở. Cả hai không đăng nhập và không dùng token cũ được. Khác với `Locked`: sai mật khẩu 5 lần chỉ khóa tạm (GB-02, `users.locked_until`) và tự mở sau 10 phút |

## 7. Admin, báo cáo, nội dung, thông báo

| # | Quyết định |
|---|---|
| 8.1 | Doanh thu nền tảng = tổng `commission_amount` của đơn `Completed`, theo ngày giờ Việt Nam. Không trừ khoản nào khác: tài xế đã chịu coupon (M1) |
| B13 | Nội dung chính sách và giới thiệu dịch vụ: cột `master_data_items.description`, Admin nhập |
| B15 | Hóa đơn điện tử, hoàn tiền tự động: ngoài phạm vi |
| B16 | Xuất báo cáo Excel (cần xin duyệt một package Excel khi bắt đầu) |
| B17, 8.3 | Mỗi sự kiện dưới đây gửi **hai đường**: FCM tới app, và SignalR `NotificationReceived` tới mọi kết nối của user (`Clients.User(userId)`, đọc claim `NameIdentifier`, không tự tạo group `user-{id}`). Web chỉ nhận khi đang mở trang. Gói hai đường vào một chỗ (`INotificationService` gọi `IPushNotificationService` và `IRealtimeNotifier`) để mọi người chỉ gọi một hàm. **Không lưu lịch sử, không có bảng `notifications`**: mở lại web thì khách xem trạng thái hiện tại ở danh sách và chi tiết đơn. Sự kiện tối thiểu cho **khách**: đơn được nhận, tài xế đến điểm lấy, ảnh lấy hàng chờ duyệt, đã giao, đã hoàn thành, kết quả sự cố, kết quả hoàn tiền, hệ thống tự hủy đơn (`ORDER_CANCELLED`, 2026-11-02). Cho **tài xế**: đơn mới gần, quyết định sự cố, khách báo sự cố (`INCIDENT_REPORTED`), đánh giá mới (`RATING_RECEIVED`), kết quả duyệt hồ sơ, kết quả rút tiền. Ai đổi trạng thái thì gửi thông báo đó |

## 8. Hạ tầng và quy ước

| # | Quyết định |
|---|---|
| C1 | Aggregate cần `Include` hay truy vấn riêng có `I<Name>Repository` (mẫu: `IUserRepository`); người sở hữu module tự thêm |
| C2 | Không test với DB thật. Test domain, validator và handler với fake (`tests/ModMove.IntegrationTests/Fakes.cs`) |
| C3, 3.4 | Khóa sandbox: Hoàng tạo PayOS + FCM; Hiếu tạo key MapVina (Search v2 + Directions v2, `mode=driving`; đổi từ Google Maps ngày 2026-11-08; gói Free 200 lượt geocode và 200 lượt tìm đường mỗi ngày, dùng chung key) + Cloudinary + Gmail. Gửi qua kênh riêng, không lên repo |
| C5 | `?page=1&pageSize=20` → `PagedResult<T>`; thời gian ISO 8601 UTC; tiền là số VND; enum là chuỗi |
| C7, 9.3 | Chạy và test đủ các luồng ở **local**; chưa đưa lên production. Migration tự chạy ở Development |
| C8 | Admin sửa được (`platform_settings`): `COMMISSION_RATE`, `LOADING_FEE`, `MIN_COD_DEPOSIT`, `MATCHING_RADIUS_KM`. Cố định trong `BusinessRules.cs`: mốc thời gian, giới hạn OTP/đăng nhập/ảnh, nạp cọc tối thiểu, cửa sổ đánh giá. Không để một giá trị ở cả hai chỗ. `COMMISSION_RATE` chỉ được sửa khi hệ thống bảo trì hoặc không có đơn nào đang chạy (từ `Accepted` tới trước `Delivered`), để hoa hồng của đơn đang chạy bằng đúng số đã kiểm tra lúc nhận đơn (GB-16). Đơn `Delivered` không khóa vì khiếu nại có thể giữ đơn lâu (7.3); đơn này tính theo tỷ lệ tại lúc hoàn tất, ví cọc thiếu thì được âm (GB-15) (2026-11-02) |
| C9 | Collation `utf8mb4_unicode_ci`; DEFAULT ở mức DB viết bằng SQL trong migration, không dùng `HasDefaultValue`. Ai `AlterColumn` một cột có DEFAULT phải `SET DEFAULT` lại trong cùng migration |
| C10 | Thao tác lên đơn không phải của mình: kiểm tra trong entity, trả 422 `ORDER_NOT_YOURS` |
| C11 | **Kênh client.** Web (React 19 + Vite + TypeScript + Tailwind) cho Guest, Customer, Dispatcher, Admin. Mobile (Flutter, **một app**, màn hình theo role) cho Customer và Driver. Dispatcher và Admin không dùng app. Đăng ký theo B6, thông báo theo 9.2 và B17 |
| 9.4 | API trả `detail` tiếng Anh kèm `code` ổn định. Web hiển thị tiếng Anh (có tùy chọn tiếng Việt), app hiển thị tiếng Việt theo `code`. Bảng `code` ở [api/README.md](api/README.md) |
| 9.5 | Lưu UTC; ngày trong báo cáo, mã đơn và hiển thị theo giờ Việt Nam (UTC+7) |
