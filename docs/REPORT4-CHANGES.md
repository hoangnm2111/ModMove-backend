# Chỗ cần sửa trong Report-3 (SRS) và Report-4 (SDS)

Report-4 là bản thiết kế ban đầu; schema thật là migration trong code. File này gom các chỗ code đã khác Report-4, để cập nhật file Word khi nộp. Ai đổi schema thì ghi thêm vào đây. Danh sách chỉ nói sửa gì, ở đâu.

## Phần mở đầu mục 3 (đoạn "The database runs on MySQL 8…")

- Giữ câu collation `utf8mb4_unicode_ci` (code giờ đã đặt đúng).
- Thêm một câu: "Default values are declared in the migrations as database DEFAULTs; the application also sets every value on insert."
- Số bảng: **22** thay vì 20 (thêm `support_requests`, `refresh_tokens`). Sửa cả dòng Change log "database design (20 tables)" nếu muốn khớp.

## 3.2 `otp_verifications`

Thêm một dòng sau `send_count`:

| Column | Type | Constraints | Notes |
|---|---|---|---|
| failed_attempts | INT | NOT NULL, DEFAULT 0 | Wrong guesses against the current code; the code stops working at 5 (brute-force guard) |

## 3.8 `platform_settings`

- Cột `setting_key`, ghi chú ví dụ: đổi `e.g. COMMISSION_RATE, LOADING_FEE, PEAK_SURCHARGE, MIN_COD_DEPOSIT` thành `e.g. COMMISSION_RATE, LOADING_FEE, MIN_COD_DEPOSIT, MATCHING_RADIUS_KM`. Không còn phụ phí giờ cao điểm (DECISIONS B2).

## 3.9 `master_data_items`

Thêm một dòng sau `name`:

| Column | Type | Constraints | Notes |
|---|---|---|---|
| description | TEXT | NULL | Long text, e.g. policy content shown to guests (UC-GST-04) |

## 3.10 `shipping_orders`

Thêm ba dòng sau `total_fee`:

| Column | Type | Constraints | Notes |
|---|---|---|---|
| commission_rate | DECIMAL(5,4) | NULL | Rate applied at completion; never changes afterwards (UC-ADM-13) |
| commission_amount | DECIMAL(15,2) | NULL | rate × total_fee (after the coupon); the driver bears the coupon and earns total_fee − commission |
| paid_at | DATETIME(6) | NULL | When the PayOS payment was confirmed; the matching timer of a PayOS order starts here |

## 3.21 `support_requests` (bảng mới)

Questions sent by guests before registering (UC-GST-05). Staff answer outside the system.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| id | BIGINT | PRIMARY KEY, AUTO_INCREMENT | Surrogate key |
| full_name | VARCHAR(100) | NOT NULL | |
| contact | VARCHAR(100) | NOT NULL | Phone number or email |
| content | VARCHAR(1000) | NOT NULL | |
| status | VARCHAR(20) | NOT NULL, DEFAULT 'New', INDEX | New · Handled |
| created_at | DATETIME(6) | NOT NULL, DEFAULT CURRENT_TIMESTAMP(6) | |
| updated_at | DATETIME(6) | NULL | |

## 3.13 `proof_of_deliveries`

Thêm một dòng sau `pickup_photo_urls`:

| Column | Type | Constraints | Notes |
|---|---|---|---|
| pickup_submission_count | INT | NOT NULL, DEFAULT 0 | How many times pickup photos were sent; at most 5 |

## 3.18 `refunds`

Thêm một dòng sau `info_request_note`:

| Column | Type | Constraints | Notes |
|---|---|---|---|
| customer_reply | VARCHAR(1000) | NULL | The customer's answer to the dispatcher's request (UC-DIS-14) |

## 3.22 `refresh_tokens` (bảng mới)

Long-lived sign-in: 30 days, stored only as a hash, used once then replaced.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| id | BIGINT | PRIMARY KEY, AUTO_INCREMENT | Surrogate key |
| user_id | BIGINT | NOT NULL, FK → users.id, INDEX | |
| token_hash | CHAR(64) | NOT NULL, UNIQUE | Hex SHA-256 of the token the client holds |
| expires_at | DATETIME(6) | NOT NULL | 30 days after issue |
| revoked_at | DATETIME(6) | NULL | Set when used, on logout, password change, deletion or lock |
| created_at | DATETIME(6) | NOT NULL, DEFAULT CURRENT_TIMESTAMP(6) | |

## Sơ đồ ERD (Figure 5)

- Thêm bảng `support_requests` (không có khóa ngoại) và `refresh_tokens` (khóa ngoại tới `users`).
- Thêm `commission_rate`, `commission_amount` vào hộp `shipping_orders` nếu sơ đồ có liệt kê cột tiền.

## Không cần sửa

- 3.19 `ratings.rating_stars` đã ghi `TINYINT`: code giờ khớp.
- Các bảng, cột, khóa ngoại, unique, index, check còn lại đã khớp 100% (kiểm bằng script đối chiếu ngày 2026-10-21).
- `updated_at` ghi "ON UPDATE CURRENT_TIMESTAMP(6)": DB không khai báo, ứng dụng tự gán khi cập nhật. Có thể giữ hoặc sửa ghi chú thành "set by the application on update".

## Report-3 (SRS): chỗ nên sửa theo quyết định của nhóm

- Bỏ **UC-CUS-28 Edit Delivery Request**: không sửa đơn, phải hủy rồi tạo lại (DECISIONS 4.1).
- Quy tắc dữ liệu mục 3.3 "Mandatory Proof of Delivery": bỏ "receiver signature", chỉ còn ảnh + thời điểm (6.2).
- GB-22: hủy miễn phí khi Pending/Accepted; từ Arrived khách không tự hủy, không hoàn tiền (2.1).
- UC-DRV-15 "Confirm Arrival at Destination": không có người nhận thì tài xế vẫn giao và gửi POD (2.7).
- Thêm: nạp cọc tối thiểu 50.000đ (5.2), rút tiền không giới hạn tối thiểu (5.1), đánh giá trong 1 ngày sau hoàn tất (7.5), tối đa 5 lần gửi ảnh lấy hàng (6.3), refresh token 30 ngày (9.1).
