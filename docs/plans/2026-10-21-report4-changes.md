# Chỗ cần sửa trong Report-4 (Software Design Specification), mục I.3 Database Design

Report-4 là bản thiết kế ban đầu; schema thật là migration trong code. File này gom các chỗ code đã khác Report-4, để cập nhật file Word khi nộp. Ai đổi schema thì ghi thêm vào đây. Danh sách chỉ nói sửa gì, ở đâu.

## Phần mở đầu mục 3 (đoạn "The database runs on MySQL 8…")

- Giữ câu collation `utf8mb4_unicode_ci` (code giờ đã đặt đúng).
- Thêm một câu: "Default values are declared in the migrations as database DEFAULTs; the application also sets every value on insert."
- Số bảng: **21** thay vì 20 (thêm `support_requests`). Sửa cả dòng Change log "database design (20 tables)" nếu muốn khớp.

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

Thêm hai dòng sau `total_fee`:

| Column | Type | Constraints | Notes |
|---|---|---|---|
| commission_rate | DECIMAL(5,4) | NULL | Rate applied at completion; never changes afterwards (UC-ADM-13) |
| commission_amount | DECIMAL(15,2) | NULL | rate × (distance_fee + service_fee); the platform funds the coupon |

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

## Sơ đồ ERD (Figure 5)

- Thêm bảng `support_requests` (không có khóa ngoại).
- Thêm `commission_rate`, `commission_amount` vào hộp `shipping_orders` nếu sơ đồ có liệt kê cột tiền.

## Không cần sửa

- 3.19 `ratings.rating_stars` đã ghi `TINYINT`: code giờ khớp.
- Các bảng, cột, khóa ngoại, unique, index, check còn lại đã khớp 100% (kiểm bằng script đối chiếu ngày 2026-10-21).
- `updated_at` ghi "ON UPDATE CURRENT_TIMESTAMP(6)": DB không khai báo, ứng dụng tự gán khi cập nhật. Có thể giữ hoặc sửa ghi chú thành "set by the application on update".
