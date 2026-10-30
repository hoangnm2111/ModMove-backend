# API Backend ModMove

Danh sách endpoint đã có và bảng mã lỗi, cho nhóm web/mobile và cho backend. Thêm dòng mỗi khi tạo endpoint hoặc mã lỗi mới. Thử trực tiếp ở Swagger: `http://localhost:5133/swagger`.

## Quy ước chung

- **Client (DECISIONS C11):** web cho Guest, Customer, Dispatcher, Admin; app Flutter cho Customer và Driver.
- **Route theo thư mục controller:** `api/public/*` không cần đăng nhập; `api/account/*` cần đăng nhập, mọi role; `api/customer/*`, `api/driver/*`, `api/dispatcher/*`, `api/admin/*` chỉ đúng role (Admin là role `Administrator`); `api/webhooks/*` cho PayOS.
- **Xác thực:** header `Authorization: Bearer <accessToken>`. Access token sống 15 phút; hết hạn thì gọi `refresh` với `refreshToken` (30 ngày, dùng một lần) để lấy cặp mới. Gọi lại một refresh token đã được đổi sang cặp mới sẽ đăng xuất tài khoản trên mọi máy; token bị thu hồi do đăng xuất hay đổi mật khẩu chỉ bị từ chối (401).
- **JSON:** enum là chuỗi (`"Pending"`), tiền là số VND, thời gian ISO 8601 UTC, phân trang `?page=1&pageSize=20` → `{ items, pageNumber, pageSize, totalCount }`.
- **Lỗi:** mọi lỗi là ProblemDetails `{ status, title, detail, code, traceId }`, lỗi 400 có thêm `errors` theo từng trường. Client rẽ nhánh theo `code`, không theo `detail` (tiếng Anh). App map `code` sang MSGxx tiếng Việt của SRS.
- **Giới hạn tần suất:** 10 request/phút/IP cho `api/public/auth/*`, `api/account/password` và `api/public/supportrequests` → 429 `TOO_MANY_REQUESTS`.

## Endpoint đã có

| Method | Route | Ai gọi | Ghi chú |
|---|---|---|---|
| POST | `api/public/auth/login` | Mọi role | Trả `accessToken`, `refreshToken`, `userId`, `fullName`, `role` |
| POST | `api/public/auth/refresh` | Mọi role | `{ refreshToken }` → cặp token mới |
| POST | `api/public/auth/register` | Web, app | Luôn tạo Customer; gửi OTP về email |
| POST | `api/public/auth/register-driver` | Chỉ app | Luôn tạo Driver |
| POST | `api/public/auth/verify-email` | Web, app | `{ email, code }` → 204 |
| POST | `api/public/auth/otp/resend` | Web, app | `{ email, purpose: Register \| ResetPassword }` → luôn 204 |
| POST | `api/public/auth/forgot-password` | Web, app | `{ email }` → luôn 204 |
| POST | `api/public/auth/reset-password` | Web, app | `{ email, code, newPassword }` → 204 |
| GET | `api/public/vehicletypes` | Ai cũng được | Loại xe đang dùng, sức chứa, giá |
| GET | `api/public/policies` | Ai cũng được | Chính sách (`code`, `name`, `description`) |
| GET | `api/public/serviceinfo` | Ai cũng được | Giới thiệu, khu vực, lợi ích |
| POST | `api/public/supportrequests` | Ai cũng được | `{ fullName, contact, content }` → 204 |
| POST | `api/public/fareestimate` | Ai cũng được | `{ vehicleTypeId, pickupAddress, dropoffAddress, weightKg, lengthCm, widthCm, heightCm, hasLoadingService, couponCode? }` → `{ distanceKm, durationMinutes, distanceFee, serviceFee, discountAmount, totalFee }`. Kích thước là của món lớn nhất, khối lượng là cả đơn |
| GET | `api/account/profile` | Đã đăng nhập | Hồ sơ của chính mình |
| PUT | `api/account/profile` | Đã đăng nhập | `{ fullName, phoneNumber, avatarUrl }` → 204 |
| PUT | `api/account/fcmtoken` | Chỉ app (web không dùng FCM, 9.2) | `{ token }` → 204; mỗi tài khoản giữ một token, máy đăng ký sau cùng thắng |
| POST | `api/account/password` | Đã đăng nhập | `{ currentPassword, newPassword }` → 204; thu hồi mọi refresh token, mọi máy (cả máy đang dùng) phải đăng nhập lại |
| POST | `api/account/logout` | Đã đăng nhập | 204; thu hồi mọi refresh token |
| DELETE | `api/customer/account` | Customer | 204; tài khoản thành `Inactive` |
| POST | `api/customer/orders` | Customer | Tạo đơn **tiền mặt**: `{ vehicleTypeId, pickupAddress, dropoffAddress, receiverName, receiverPhone, cargo: { cargoType, quantity, weightKg, lengthCm, widthCm, heightCm, photoUrls[1..5], specialNotes? }, hasLoadingService, couponCode?, paymentMethod: "Cash", payerRole: Sender \| Receiver, prohibitedGoodsCommitment: true }` → 201 `{ orderId, orderCode, totalFee }`. Giá tính lại ở server như báo giá |
| GET | `api/customer/orders` | Customer | `?page=1&pageSize=20&view=All\|Active\|History&search=` → `PagedResult` đơn của chính mình, mới nhất trước (`page` 1–10 000, `pageSize` ≤ 50; History = Completed/Cancelled) |
| GET | `api/customer/orders/{id}` | Customer | Chi tiết đơn: phí, hàng (ảnh), tài xế (khi đã nhận), lịch sử trạng thái. Đơn người khác → 404 |
| POST | `api/customer/uploads` | Customer | `multipart/form-data`, field `file`: một ảnh JPG/PNG ≤ 5 MB → `{ url }` (Cloudinary) dùng cho ảnh hàng khi tạo đơn |
| GET | `api/admin/pricing` | Administrator | `{ vehicleTypes: [{ id, name, maxLoadWeightKg, baseFare, pricePerKm, minimumFare, isActive }], settings: { commissionRate, loadingFee, minCodDeposit, matchingRadiusKm } }`. Có cả loại xe đang tắt. `commissionRate` là phân số (0.10 = 10%) |
| PUT | `api/admin/pricing/vehicletypes/{id}` | Administrator | `{ baseFare, pricePerKm, minimumFare }` → 204; id lạ → 404. Đơn đã đặt giữ nguyên giá (DECISIONS 4.4) |
| PATCH | `api/admin/pricing/vehicletypes/{id}/status` | Administrator | `{ isActive }` → 204; bật/tắt loại xe |
| PUT | `api/admin/pricing/settings` | Administrator | `{ commissionRate?, loadingFee?, minCodDeposit?, matchingRadiusKm? }` → 204; trường bỏ trống = không đổi, phải có ít nhất một. Đổi `commissionRate` khi còn đơn đang chạy → 422 `COMMISSION_RATE_LOCKED` (DECISIONS C8) |
| GET | `api/admin/masterdata` | Administrator | `?category={category}` → danh sách master data items (`id`, `category`, `code`, `name`, `description`, `isActive`) |
| POST | `api/admin/masterdata` | Administrator | `{ category, code, name, description? }` → 201 `{ id }` |
| PUT | `api/admin/masterdata/{id}` | Administrator | `{ name, description? }` → 204 |
| PATCH | `api/admin/masterdata/{id}/status` | Administrator | `{ isActive }` → 204; bật/tắt mục danh mục |
| GET | `api/admin/coupons` | Administrator | `?page=1&pageSize=20&isActive=true` → `{ items, pageNumber, pageSize, totalCount }` |
| POST | `api/admin/coupons` | Administrator | `{ discountCode, discountValue, minimumOrderValue, validFrom, expiresAt, usageLimit? }` → 201 `{ id }` (GB-21) |
| PATCH | `api/admin/coupons/{id}/status` | Administrator | `{ isActive }` → 204; bật/tắt coupon |
| GET | `api/admin/driver-applications/pending` | Administrator | `?page=1&pageSize=20` → `{ items, pageNumber, pageSize, totalCount }`. Hàng đợi hồ sơ nộp chờ duyệt (sắp xếp nộp trước duyệt trước, UC-ADM-08) |
| GET | `api/admin/driver-applications/{id}` | Administrator | Xem chi tiết hồ sơ tài xế: thông tin cá nhân, phương tiện, các link ảnh giấy tờ/xe, lịch sử nộp (UC-ADM-09) |
| POST | `api/admin/driver-applications/{id}/approve` | Administrator | 204; Phê duyệt hồ sơ tài xế → trạng thái thành `Approved`, cập nhật/tạo thực thể Xe, tự động tạo Ví tài xế ban đầu nếu chưa có (Rule GB-16, TEAM-PLAN 74). Gửi push notification |
| POST | `api/admin/driver-applications/{id}/reject` | Administrator | `{ reasons: ["Lý do 1", "Lý do 2"] }` → 204; Từ chối hồ sơ kèm danh sách lý do cụ thể từng mục (UC-ADM-09). Gửi push notification |



## Mã lỗi

| `code` | HTTP | Khi nào |
|---|---|---|
| `VALIDATION` | 400 | Dữ liệu sai định dạng hoặc thiếu; xem `errors` |
| `UNAUTHORIZED` | 401 | Sai thông tin đăng nhập, token hỏng hoặc refresh token không dùng được |
| `FORBIDDEN` | 403 | Không có quyền |
| `EMAIL_NOT_VERIFIED` | 403 | Đăng nhập khi chưa xác minh email |
| `ACCOUNT_INACTIVE` | 403 | Tài khoản đã xóa hoặc bị khóa |
| `NOT_FOUND` | 404 | Không tìm thấy |
| `CONFLICT` | 409 | Trùng email/số điện thoại, hoặc dữ liệu vừa bị người khác đổi: tải lại rồi thử lại |
| `TOO_MANY_REQUESTS` | 429 | Vượt giới hạn tần suất |
| `BUSINESS_RULE` | 422 | Vi phạm luật nghiệp vụ không có mã riêng |
| `OTP_INVALID` | 422 | Mã OTP sai, hết hạn, đã dùng, hoặc đã nhập sai quá 5 lần |
| `WRONG_PASSWORD` | 422 | Mật khẩu hiện tại sai khi đổi mật khẩu |
| `ACCOUNT_HAS_OPEN_ORDERS` | 422 | Xóa tài khoản khi còn đơn chưa xong |
| `ORDER_NOT_CANCELLABLE` | 422 | Khách hủy khi tài xế đã đến điểm lấy |
| `ORDER_NOT_YOURS` | 422 | Thao tác (hủy, duyệt ảnh, xác nhận...) lên đơn của người khác (DECISIONS C10) |
| `ORDER_CANCELLED` | 422 | Thanh toán về sau khi đơn đã hủy (cần hoàn tiền) |
| `PICKUP_PHOTO_LIMIT` | 422 | Gửi ảnh lấy hàng lần thứ 6 |
| `TOP_UP_TOO_SMALL` | 422 | Nạp cọc dưới 50.000đ |
| `RATING_NOT_ALLOWED` | 422 | Đánh giá đơn chưa hoàn tất |
| `RATING_WINDOW_CLOSED` | 422 | Đánh giá quá 1 ngày sau hoàn tất |
| `TOO_MANY_PHOTOS` | 422 | Bằng chứng hoàn tiền quá 5 ảnh |
| `REFUND_NOT_FOR_DISPATCHER` | 422 | Dispatcher đề xuất số tiền hay xin thêm thông tin cho khoản hoàn do hủy (DECISIONS B9: đi thẳng tới Admin) |
| `NO_INFO_REQUEST` | 422 | Trả lời khi Dispatcher chưa xin thêm thông tin |
| `PAYMENT_METHOD_NOT_SUPPORTED` | 422 | Tạo đơn PayOS khi Phase 1 chỉ nhận tiền mặt |
| `ADDRESS_NOT_FOUND` | 422 | Google Maps không tìm thấy địa chỉ lấy hoặc giao |
| `ROUTE_NOT_FOUND` | 422 | Không có đường bộ giữa điểm lấy và điểm giao |
| `VEHICLE_TOO_SMALL` | 422 | Khối lượng đơn vượt tải hoặc món lớn nhất không lọt thùng loại xe đã chọn (GB-10, 4.2) |
| `COUPON_NOT_FOUND` | 422 | Mã giảm giá không tồn tại |
| `COUPON_INACTIVE` | 422 | Mã giảm giá đang tắt |
| `COUPON_EXPIRED` | 422 | Mã giảm giá chưa tới hạn hoặc đã hết hạn |
| `COUPON_LIMIT_REACHED` | 422 | Mã giảm giá đã hết lượt |
| `COUPON_MIN_ORDER_NOT_MET` | 422 | Đơn chưa đạt giá trị tối thiểu của mã (so với cước trước giảm, gồm phí bốc xếp) |
| `COMMISSION_RATE_LOCKED` | 422 | Admin đổi tỷ lệ hoa hồng khi còn đơn từ `Accepted` tới trước `Completed` (DECISIONS C8) |
| `SERVER_ERROR` | 500 | Lỗi không lường trước (xem log theo `traceId`) |
