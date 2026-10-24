# API Backend ModMove

Danh sách endpoint đã có và bảng mã lỗi, cho nhóm web/mobile và cho backend. Thêm dòng mỗi khi tạo endpoint hoặc mã lỗi mới. Thử trực tiếp ở Swagger: `http://localhost:5133/swagger`.

## Quy ước chung

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
| GET | `api/account/profile` | Đã đăng nhập | Hồ sơ của chính mình |
| PUT | `api/account/profile` | Đã đăng nhập | `{ fullName, phoneNumber, avatarUrl }` → 204 |
| PUT | `api/account/fcmtoken` | App mobile | `{ token }` → 204 |
| POST | `api/account/password` | Đã đăng nhập | `{ currentPassword, newPassword }` → 204; thu hồi mọi refresh token, mọi máy (cả máy đang dùng) phải đăng nhập lại |
| POST | `api/account/logout` | Đã đăng nhập | 204; thu hồi mọi refresh token |
| DELETE | `api/customer/account` | Customer | 204; tài khoản thành `Inactive` |

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
| `SERVER_ERROR` | 500 | Lỗi không lường trước (xem log theo `traceId`) |
