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
| POST | `api/account/logout` | Đã đăng nhập | App: `{ fcmToken }`; web: không body → 204. Thu hồi mọi refresh token (9.1); chỉ xóa FCM token khi `fcmToken` gửi lên đúng là token đang lưu, máy đăng ký sau vẫn nhận thông báo (9.2) |
| DELETE | `api/customer/account` | Customer | 204; tài khoản thành `Inactive` |
| GET | `api/dispatcher/orders` | Dispatcher | Danh sách giám sát mọi đơn (`status`, `search` theo mã đơn / SĐT khách / tên hoặc biển số tài xế, `from`, `to`, `page`, `pageSize`), mới nhất trước, kèm cờ `matchingTimeoutSoon` (Pending ≥ 8 phút theo 3.2), `awaitingPickupApproval` (PickedUp chờ duyệt ảnh), `openIncidentId` (UC-DIS-01) |
| GET | `api/dispatcher/orders/summary` | Dispatcher | Số lượng đơn theo 8 trạng thái (`total`, `pending`, `accepted`, `arrived`, `pickedUp`, `inTransit`, `delivered`, `completed`, `cancelled`) (UC-DIS-01) |
| GET | `api/dispatcher/orders/{id}` | Dispatcher | Chi tiết đầy đủ đơn cho Dispatcher: timeline, khách, người nhận, tài xế + xe + GPS cuối, tuyến, hàng + ảnh, ảnh lấy hàng + POD, lịch sử thanh toán, sự cố, hoàn tiền (UC-DIS-01) |
| GET | `api/dispatcher/drivers/online` | Dispatcher | Mọi tài xế `Online` + `Approved` + `Active` có GPS trong 5 phút gần nhất (6.4, B12), kèm đơn đang chạy nếu có; cập nhật tiếp qua SignalR `LocationUpdated` (UC-DIS-01) |
| GET | `api/dispatcher/handled-cases` | Dispatcher | `?type=Incident\|Complaint\|IncidentRefund\|Refund&page=1&pageSize=20` → các sự cố (`handled_by = tôi`) và đề xuất hoàn tiền (`proposed_by = tôi`) đã xử lý, mới nhất trước (UC-DIS-16) |
| GET | `api/dispatcher/incidents` | Dispatcher | Danh sách sự cố (`status`, `incidentType`, `search`, `page`, `pageSize`) |
| GET | `api/dispatcher/incidents/{id}` | Dispatcher | Chi tiết sự cố: đơn, hàng (kèm `photoUrls` khách khai báo), khách, tài xế, `proof` (ảnh lấy hàng, ảnh giao, trạng thái; `null` trước khi lấy hàng), lịch sử, `refund` (lý do, ảnh bằng chứng, câu hỏi/trả lời, đề xuất; `null` nếu chưa có) |
| POST | `api/dispatcher/incidents/{id}/start` | Dispatcher | → 204; nhận xử lý sự cố (`Open` → `InProgress`). Đang `InProgress` bởi Dispatcher khác → 409 `CONFLICT` (UC-DIS-05) |
| POST | `api/dispatcher/incidents/{id}/resolve` | Dispatcher | `{ reason }` → 204; quyết định `ContinueTrip`, đơn giữ nguyên trạng thái (`ORDER_NOT_ACTIVE` nếu chuyến không còn chạy). Gửi `INCIDENT_RESOLVED` cho khách và `INCIDENT_DECIDED` cho tài xế (UC-DIS-05) |
| GET | `api/dispatcher/incidents/{id}/replacement-drivers` | Dispatcher | Danh sách tài xế thay thế đủ điều kiện quanh điểm sự cố (hoặc điểm lấy hàng): `Online`, `Approved`, GPS ≤ 5 phút (6.4), không kẹt đơn (GB-09), đúng loại xe (B5, 7.2), trong bán kính `MATCHING_RADIUS_KM`, đủ ví cọc COD nếu đơn tiền mặt (GB-16); sắp theo khoảng cách tăng dần (UC-DIS-06) |
| POST | `api/dispatcher/incidents/{id}/reassign` | Dispatcher | `{ newDriverProfileId, reason }` → 204; kiểm tra lại đủ điều kiện (`DRIVER_NOT_ELIGIBLE`), chuyển đơn về `Accepted` cho tài xế mới và reset số lần gửi ảnh lấy hàng (7.2), đóng sự cố `ReassignDriver`, phát `OrderStatusChanged` + thông báo cho khách và cả 2 tài xế (UC-DIS-06) |
| POST | `api/dispatcher/incidents/{id}/cancel-order` | Dispatcher | `{ reason, refund, cargoDisposition?: ReturnToPickup \| Keep }` → 204; hủy đơn đang chạy (`Accepted`…`InTransit`; ngoài khoảng này → `ORDER_NOT_CANCELLABLE`). Khi `PickedUp`/`InTransit` bắt buộc `cargoDisposition` (`CARGO_DISPOSITION_REQUIRED`, 2.6). `refund: true` chỉ cho đơn PayOS đã `Paid` (`REFUND_NOT_ALLOWED`, 2.5), tự tạo khoản hoàn `Cancellation` 100% trình Admin (UC-DIS-07) |
| POST | `api/dispatcher/incidents/{id}/cancel` | Dispatcher | `{ reason }` → 204; hủy báo cáo sự cố không hợp lệ (`Cancelled`), tự đề xuất `0đ` lên Admin nếu đang có yêu cầu hoàn tiền sự cố `Requested`, thông báo cho khách và tài xế (UC-DIS-10) |
| GET | `api/dispatcher/complaints` | Dispatcher | Danh sách khiếu nại (`stage`, `search`, `page`, `pageSize`) |
| GET | `api/dispatcher/complaints/{id}` | Dispatcher | Chi tiết khiếu nại kèm hàng khách khai báo (`order.cargo.photoUrls`), POD, đánh giá đơn, lịch sử sự cố để đối soát |
| POST | `api/dispatcher/complaints/{id}/request-info` | Dispatcher | `{ note }` (≤ 1000) → 204; xin thêm thông tin từ khách (7.4). Khách nhận `REFUND_INFO_REQUESTED` (MSG60) |
| POST | `api/dispatcher/complaints/{id}/proposal` | Dispatcher | `{ amount, reason }` → 204; gửi đề xuất hoàn tiền lên Admin duyệt (0 ≤ `amount` ≤ số khách yêu cầu) |
| POST | `api/dispatcher/incidents/{id}/refund/request-info` | Dispatcher | `{ note }` → 204; như trên nhưng cho khoản hoàn tiền mở của **sự cố** (`id` là id sự cố; chưa có khoản hoàn mở → 404). Hoàn do hủy → `REFUND_NOT_FOR_DISPATCHER` |
| POST | `api/dispatcher/incidents/{id}/refund/proposal` | Dispatcher | `{ amount, reason }` → 204; đề xuất cho khoản hoàn mở của sự cố |
| POST | `api/customer/orders` | Customer | Tạo đơn **tiền mặt**: `{ vehicleTypeId, pickupAddress, dropoffAddress, receiverName, receiverPhone, cargo: { cargoType, quantity, weightKg, lengthCm, widthCm, heightCm, photoUrls[1..5], specialNotes? }, hasLoadingService, couponCode?, paymentMethod: "Cash", payerRole: Sender \| Receiver, prohibitedGoodsCommitment: true }` → 201 `{ orderId, orderCode, totalFee }`. Giá tính lại ở server như báo giá |
| GET | `api/customer/orders` | Customer | `?page=1&pageSize=20&view=All\|Active\|History\|Completed\|Cancelled&search=&status=&fromDate=&toDate=&receiverPhone=` → `PagedResult` đơn của chính mình, mới nhất trước (`page` 1–10 000, `pageSize` ≤ 50; History = Completed + Cancelled; `search` khớp mã đơn, địa chỉ, tên người nhận). Lọc (CUS-09): `status` (tên trạng thái), `fromDate`/`toDate` dạng `yyyy-MM-dd` theo ngày giờ Việt Nam, tính cả hai ngày, trong khoảng 2020-01-01 – 2100-12-31, `fromDate` > `toDate` → 400; `receiverPhone` 1–15 chữ số, khớp một phần. Mỗi dòng có thêm `receiverName`, `receiverPhone` |
| GET | `api/customer/orders/{id}` | Customer | Chi tiết đơn: phí, hàng (ảnh), tài xế (khi đã nhận), `proof` (ảnh lấy hàng, chỉ hiện nút duyệt khi `orderStatus == PickedUp` **và** `proofStatus == Pending`; sau khi đổi tài xế đơn về `Accepted` nhưng `proof` còn ảnh cũ, `pickupSubmissionCount`, `rejectionReason`, ảnh giao hàng; `null` trước khi lấy hàng), lịch sử trạng thái, `couponCode` (`null` nếu không dùng), `cargo.cargoTypeName` (tên loại hàng trong master data `CARGO_TYPE`). `payment`: lần thanh toán PayOS mới nhất `{ transactionStatus, checkoutUrl, expiresAt, paidAt, isExpired }`, `null` với đơn tiền mặt hoặc khi chưa tạo link; `checkoutUrl` chỉ có khi còn trả được, `isExpired = true` (giao dịch `Expired`, hoặc còn `Pending` mà đã quá `expiresAt`) thì hiện nút tạo mã mới (3.1). Đơn người khác → 404 |
| POST | `api/customer/orders/{id}/cancel` | Customer | `{ reason }` (≤ 500) → 204. Chỉ khi `Pending`/`Accepted` (`ORDER_NOT_CANCELLABLE`); đơn người khác → `ORDER_NOT_YOURS`. Đơn PayOS đã trả: tạo hoàn tiền Cancellation 100% chờ Admin (B9). Không hoàn lượt coupon (B11). Đơn đã có tài xế: tài xế nhận thông báo `ORDER_CANCELLED` |
| POST | `api/customer/orders/{id}/pickup-evidence` | Customer | `{ approve, reason? }` → 204. Duyệt ảnh lấy hàng: đơn sang `InTransit` (GB-26). Từ chối: bắt buộc `reason` (≤ 500), đơn giữ `PickedUp`, tài xế chụp lại (tối đa 5 lần gửi, 6.3). Chỉ khi có ảnh đang chờ duyệt (`BUSINESS_RULE`); đơn người khác → `ORDER_NOT_YOURS`. Tài xế nhận `PICKUP_APPROVED`/`PICKUP_REJECTED` |
| POST | `api/customer/orders/{id}/confirm-receipt` | Customer | → 204. Chỉ khi `Delivered`: đơn `Completed`, chốt hoa hồng `COMMISSION_RATE × total_fee` (M1) và cập nhật ví tài xế cùng lúc (PayOS: cộng thu nhập; tiền mặt: trừ hoa hồng từ ví cọc, GB-15). Đơn người khác → `ORDER_NOT_YOURS`; trùng với job tự hoàn tất → 409. Tài xế nhận `ORDER_COMPLETED` |
| POST | `api/customer/ratings` | Customer | `{ orderId, stars (1–5), comment? (≤ 1000) }` → 201 `{ ratingId }`. Chỉ đơn `Completed` của chính mình (`RATING_NOT_ALLOWED`, `ORDER_NOT_YOURS`), trong 1 ngày sau hoàn tất (`RATING_WINDOW_CLOSED`, 7.5). Mỗi đơn một lần, không sửa (GB-20): lần hai → 409 `CONFLICT` |
| GET | `api/customer/ratings` | Customer | `?page=1&pageSize=20` → `PagedResult` đánh giá đã gửi `{ id, orderId, orderCode, ratingStars, reviewComment, driverName, createdAt }`, mới nhất trước (UC-CUS-26) |
| POST | `api/customer/refunds/complaints` | Customer | `{ orderId, reason (≤ 1000), evidenceUrls? (≤ 5, Cloudinary), requestedAmount (số nguyên VND) }` → 201 `{ refundId }`. Chỉ khi đơn `Delivered` và trong 24 giờ từ lúc giao (`COMPLAINT_WINDOW_CLOSED`, 7.3); số tiền > 0 và ≤ `total_fee` (`REFUND_AMOUNT_INVALID`, B9); mỗi đơn một khiếu nại mở (409); đơn người khác → `ORDER_NOT_YOURS`. Dispatcher nhận `COMPLAINT_SUBMITTED` |
| POST | `api/customer/refunds/incidents` | Customer | `{ incidentId, reason, evidenceUrls?, requestedAmount }` → 201 `{ refundId }`. Sự cố thuộc đơn của mình, ở mọi trạng thái sự cố; mỗi sự cố một yêu cầu mở (409); cùng luật số tiền (B9) |
| POST | `api/customer/refunds/{id}/reply` | Customer | `{ reply (≤ 1000), photoUrls? }` → 204. Trả lời câu hỏi của Dispatcher (7.4); chưa có câu hỏi → `NO_INFO_REQUEST`; tổng ảnh > 5 → `TOO_MANY_PHOTOS` |
| POST | `api/customer/refunds/{id}/withdraw` | Customer | → 204. Rút khiếu nại/hoàn sự cố còn `Requested`; hoàn do hủy không rút được (`BUSINESS_RULE`) |
| GET | `api/customer/refunds`, `api/customer/refunds/{id}` | Customer | Hoàn tiền trên đơn của mình (hủy, sự cố, khiếu nại), mới nhất trước. `approvedAmount` chỉ có khi Admin đã duyệt (đề xuất của Dispatcher chưa phải số cuối); `waitingForYourReply` = Dispatcher đang chờ khách trả lời; chi tiết có `evidenceUrls`, `infoRequestNote`, `customerReply`. Hoàn của người khác → 404 |
| POST | `api/customer/incidents` | Customer | `{ orderId, incidentType (VehicleBreakdown\|DamagedCargo\|Accident\|Other), description (≤ 1000), photoUrls? (≤ 5, Cloudinary), latitude?, longitude? }` → 201 `{ incidentId }`. Chỉ khi đơn của mình đang `Arrived`/`PickedUp`/`InTransit` (`INCIDENT_NOT_ALLOWED`; sau khi giao dùng khiếu nại); "trễ", "sai tuyến" ghi là `Other` (7.1). Dispatcher nhận `INCIDENT_REPORTED` |
| GET | `api/customer/incidents` | Customer | `?page=1&pageSize=20` → `PagedResult` sự cố trên đơn của mình (khách hay tài xế báo), mới nhất trước: `{ id, orderId, orderCode, incidentType, description, photoUrls, reportedByYou, resolutionStatus, resolutionDecision, resolutionNote, createdAt, resolvedAt }` (UC-CUS-16) |
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
| GET | `api/driver/onboarding/status` | Driver | `{ hasApplication, canSubmit, profileApprovalStatus, applicationId, latestVersionNumber, submittedAt, reviewedAt, vehicleTypeId, vehicleTypeName, licensePlate, maxLoadWeightKg, citizenIdNumber, driverLicenseNumber, documentUrls, rejectionReasons }`. Chưa nộp: `hasApplication=false`, `canSubmit=true`; `canSubmit=true` khi chưa có hồ sơ hoặc phiên bản mới nhất bị `Rejected` (UC-DRV-04) |
| POST | `api/driver/onboarding` | Driver | `{ vehicleTypeId, citizenIdNumber, driverLicenseNumber, licensePlate, maxLoadWeightKg, documentUrls: [bằng lái mặt trước, mặt sau, đăng ký xe, đăng kiểm, ảnh xe] }` → 201 `{ id }` (id phiên bản mới). Mỗi lần nộp là một version mới (UC-DRV-03, DRV-05). Đang `Pending`/`Approved` → 422 `APPLICATION_NOT_EDITABLE`; CCCD, GPLX hoặc biển số đã dùng bởi tài xế khác → 409 (GB-01, GB-04); email chưa xác minh → 403 `EMAIL_NOT_VERIFIED` |
| GET | `api/admin/users` | Administrator | `?page=1&pageSize=20&search=&role=&status=` → `{ items, pageNumber, pageSize, totalCount }`. Danh sách người dùng phân trang, lọc theo role (`Customer`, `Driver`, `Dispatcher`, `Administrator`), trạng thái (`Active`, `Inactive`, `Locked`), tìm kiếm theo tên/SĐT/email (UC-ADM-04) |
| GET | `api/admin/users/{id}` | Administrator | Xem chi tiết hồ sơ tài khoản, trạng thái, thông tin tài xế/biển số xe (nếu là Driver) và thống kê đơn hàng (UC-ADM-05) |
| PATCH | `api/admin/users/{id}/status` | Administrator | `{ status: Active \| Inactive \| Locked, reason }` → 204; Đổi trạng thái tài khoản kèm lý do bắt buộc (UC-ADM-06). Khi chuyển sang `Inactive` hoặc `Locked`, thu hồi mọi refresh token (`RevokeAllForUserAsync`) và xóa FCM token (DECISIONS B8, 8.2). Tự đổi tài khoản của chính mình → 422 `CANNOT_CHANGE_OWN_STATUS` (MSG65) |
| POST | `api/admin/users/dispatchers` | Administrator | `{ fullName, phoneNumber, email, password }` → 201 `{ id }`; Tạo tài khoản Điều phối viên (`Role=Dispatcher`, `AccountStatus=Active`, `EmailVerified=true`) để đăng nhập ngay không cần OTP (UC-ADM-07, DECISIONS B7). Trùng email/SĐT → 409 (GB-01) |

## Realtime: SignalR `/hubs/tracking` (DECISIONS B12, B17)

Kết nối với JWT trên query string: `/hubs/tracking?access_token=<accessToken>` (WebSocket không đặt được header). Tài khoản không `Active` bị ngắt ngay khi kết nối. Kết nối tự đóng khi access token hết hạn: client lấy token mới (`refresh`) rồi kết nối lại và gọi lại `JoinOrder`. Lỗi trả về `HubException` có message bắt đầu bằng mã ổn định, ví dụ `ORDER_NOT_YOURS: ...`, `ACCOUNT_INACTIVE: ...`, `VALIDATION: ...`; riêng gọi method sai vai trò thì SignalR trả lỗi "unauthorized" chung, không có mã.

| Hướng | Tên | Ai | Mô tả |
|---|---|---|---|
| client → server | `JoinOrder(orderId)` | Customer | Theo dõi đơn của mình (vào group `order-{id}`); đơn người khác → `ORDER_NOT_YOURS`. Kết nối lại thì gọi lại |
| client → server | `SendLocation(latitude, longitude)` | Driver (đã duyệt) | Gửi GPS vài giây một lần. Server lưu vị trí cuối; ghi `driver_location_logs` khi đang có đơn (`Accepted`…`InTransit`) |
| server → client | `LocationUpdated` | khách theo dõi đơn, Dispatcher | `{ driverProfileId, orderId?, latitude, longitude, at }`. Tới group `order-{id}` khi tài xế đang chở đơn đó; tới mọi Dispatcher khi tài xế `Online` (Dispatcher tự vào group `dispatchers` khi kết nối). Quá 5 phút không có GPS: coi là mất kết nối (6.4) |
| server → client | `NotificationReceived` | mọi user (Dispatcher: thông báo chung) | `{ type, title, body, data }` (B17): cùng nội dung FCM, tới mọi kết nối của user. Khách (8.3): `ORDER_ACCEPTED`, `DRIVER_ARRIVED`, `PICKUP_PHOTOS_TO_REVIEW`, `ORDER_DELIVERED`, `ORDER_COMPLETED`, `INCIDENT_RESOLVED`, `REFUND_DECIDED`, `REFUND_INFO_REQUESTED` (Dispatcher xin thêm thông tin, 7.4). Tài xế: `ORDER_CANCELLED`, `PICKUP_APPROVED`, `PICKUP_REJECTED`, `ORDER_COMPLETED`, `INCIDENT_DECIDED` (kết quả xử lý sự cố từ Dispatcher, kèm `data.incidentId`, `data.decision`). Dispatcher: `COMPLAINT_SUBMITTED`, `INCIDENT_REFUND_REQUESTED`, `REFUND_INFO_REPLIED`, `INCIDENT_REPORTED`. `data.orderId` luôn có. `ORDER_COMPLETED` gửi cho cả khách và tài xế với nội dung khác: app (một app cho mọi role) xét thêm role của người đang đăng nhập |
| server → client | `OrderStatusChanged` | khách theo dõi đơn, tài xế của đơn | `{ orderId, orderStatus }` sau mỗi lần đổi trạng thái đơn: tới group `order-{id}` và tới tài xế qua `Clients.User` (B12) |

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
| `ORDER_NOT_CANCELLABLE` | 422 | Khách hủy đơn không còn `Pending`/`Accepted` (tài xế đã đến, đã giao, đã hủy...) |
| `ORDER_NOT_YOURS` | 422 | Thao tác (hủy, duyệt ảnh, xác nhận...) lên đơn của người khác (DECISIONS C10) |
| `ORDER_CANCELLED` | 422 | Thanh toán về sau khi đơn đã hủy (cần hoàn tiền) |
| `PICKUP_PHOTO_LIMIT` | 422 | Gửi ảnh lấy hàng lần thứ 6 |
| `TOP_UP_TOO_SMALL` | 422 | Nạp cọc dưới 50.000đ |
| `RATING_NOT_ALLOWED` | 422 | Đánh giá đơn chưa hoàn tất |
| `RATING_WINDOW_CLOSED` | 422 | Đánh giá quá 1 ngày sau hoàn tất |
| `COMPLAINT_WINDOW_CLOSED` | 422 | Khiếu nại khi đơn chưa `Delivered` hoặc đã quá 24 giờ từ lúc giao (7.3) |
| `REFUND_AMOUNT_INVALID` | 422 | Số tiền yêu cầu hoàn ≤ 0 hoặc lớn hơn tổng tiền đơn (B9) |
| `INCIDENT_NOT_ALLOWED` | 422 | Khách báo sự cố khi đơn không ở `Arrived`/`PickedUp`/`InTransit` (2.2) |
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
| `APPLICATION_NOT_EDITABLE` | 422 | Tài xế nộp hồ sơ khi phiên bản mới nhất đang `Pending` hoặc đã `Approved` (MSG07) |
| `CANNOT_CHANGE_OWN_STATUS` | 422 | Admin tự khóa hoặc vô hiệu hóa tài khoản của chính mình (UC-ADM-06, MSG65) |
| `ORDER_NOT_ACTIVE` | 422 | Dispatcher chọn `ContinueTrip` khi đơn không còn đang chạy (`Accepted`…`InTransit`) |
| `DRIVER_NOT_ELIGIBLE` | 422 | Tài xế thay thế không đủ điều kiện (không Online/Approved/Active, sai loại xe, mất GPS > 5 phút, ngoài bán kính, đang có đơn khác hoặc thiếu ví cọc COD) (UC-DIS-06, 7.2) |
| `CARGO_DISPOSITION_REQUIRED` | 422 | Hủy đơn do sự cố khi hàng đã trên xe (`PickedUp`/`InTransit`) nhưng chưa chọn hướng xử lý hàng `ReturnToPickup` hay `Keep` (2.6) |
| `REFUND_NOT_ALLOWED` | 422 | Chọn hoàn tiền khi hủy đơn sự cố cho đơn tiền mặt hoặc đơn PayOS chưa thanh toán (2.5) |
| `SERVER_ERROR` | 500 | Lỗi không lường trước (xem log theo `traceId`) |
