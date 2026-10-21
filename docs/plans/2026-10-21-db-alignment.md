# Plan: khớp DB với Report-4 và DECISIONS

Ngày: 2026-10-21 · Người làm: Hiếu · Nhánh: `feature/hieu`

Kết quả kiểm tra (Report-4 mục 3 so với migration): 20/20 bảng, 246/246 cột đúng tên, kiểu và nullable; 33/33 khóa ngoại; mọi unique/index/check đều có. Plan này xử lý phần còn lệch và các quyết định nhóm đã chốt (Q1–Q8, buổi review 2026-10-21).

**Một migration duy nhất** cho bước 2–6: `AlignSchemaWithSds`. Không ai khác tạo migration cho đến khi PR này merge vào `develop`.

## Cần chốt trước bước 3 (B4)

- **Hoa hồng tính trên số nào, coupon ai chịu?** `total_fee` đã trừ coupon. Đề xuất: nền tảng chịu coupon; hoa hồng và thu nhập tài xế tính trên `distance_fee + service_fee` (trước giảm giá). `commission_amount = round(rate × (distance_fee + service_fee), 0)` theo VND. Nếu nhóm chọn khác, chỉ đổi một dòng trong `ShippingOrder.Complete`.

## Bước 1 — Một nguồn sự thật cho tham số (Q1)

Admin sửa được (đọc từ `platform_settings`): `COMMISSION_RATE`, `LOADING_FEE`, `MIN_COD_DEPOSIT`, `MATCHING_RADIUS_KM`. Cố định trong code (`BusinessRules`): mốc thời gian, giới hạn OTP/đăng nhập/ảnh.

| File | Sửa |
|---|---|
| `src/ModMove.Domain/Constants/BusinessRules.cs` | Xóa `MinCodDepositBalance`, `DefaultCommissionRate` (giờ là setting) |
| `src/ModMove.Domain/Entities/DriverWallet.cs` | `CanReceiveCod` → `CanReceiveCod(decimal minDeposit)`; handler đọc `MIN_COD_DEPOSIT` rồi truyền vào |
| `src/ModMove.Infrastructure/Persistence/Seed/ReferenceDataSeeder.cs` | Bỏ `MATCHING_TIMEOUT_MINUTES`, `PAYOS_SESSION_MINUTES`, `AUTO_COMPLETE_HOURS`, `COMPLAINT_WINDOW_HOURS`; thêm `MATCHING_RADIUS_KM = 5` |
| `tests/ModMove.UnitTests/Domain/DriverWalletTests.cs` | Test GB-16 truyền `200_000` vào `CanReceiveCod` |
| `docs/DECISIONS.md` | Thêm C8: bảng "setting hay hằng số"; B5 ghi key đã có trong seed |

Không làm ở bước này: service đọc `platform_settings` (người dùng đầu tiên là Hoàng ở Pricing/Matching, thêm khi cần).

## Bước 2 — `rating_stars` thành `TINYINT` (Q4)

| File | Sửa |
|---|---|
| `src/ModMove.Infrastructure/Persistence/Configurations/RatingConfiguration.cs` | `.HasColumnType("tinyint")` cho `RatingStars` (giữ `int` trong C#) |

Kiểm: đọc/ghi một `Rating` thật trên MySQL local (MySqlConnector đọc `tinyint` vào `int` được).

## Bước 3 — Hoa hồng trên đơn (B4)

| File | Sửa |
|---|---|
| `src/ModMove.Domain/Entities/ShippingOrder.cs` | Thêm `CommissionRate` (`decimal?`), `CommissionAmount` (`decimal?`); `Complete(by, userId, now, commissionRate)` gán một lần, đơn đã hoàn tất không đổi nữa |
| `src/ModMove.Infrastructure/Persistence/Configurations/ShippingOrderConfiguration.cs` | `commission_rate DECIMAL(5,4) NULL`, `commission_amount DECIMAL(15,2) NULL` |
| `tests/ModMove.UnitTests/Domain/ShippingOrderTests.cs` | Test: hoàn tất lưu đúng tỷ lệ và số tiền; tỷ lệ ngoài 0..1 bị từ chối |

## Bước 4 — Nội dung chính sách (B13)

| File | Sửa |
|---|---|
| `src/ModMove.Domain/Entities/MasterDataItem.cs` | Thêm `Description` (`string?`); `Create(..., description = null)`, `UpdateDescription` |
| `src/ModMove.Infrastructure/Persistence/Configurations/MasterDataItemConfiguration.cs` | `description TEXT NULL` |
| `src/ModMove.Infrastructure/Persistence/Seed/ReferenceDataSeeder.cs` | Giữ 4 dòng `POLICY`, `description` để trống (Admin nhập nội dung thật qua UC-ADM-15) |

## Bước 5 — Bảng `support_requests` (B14)

| File | Sửa |
|---|---|
| `src/ModMove.Domain/Enums/SupportRequestStatus.cs` (mới) | `New`, `Handled` |
| `src/ModMove.Domain/Entities/SupportRequest.cs` (mới) | `FullName`, `Contact` (số điện thoại hoặc email), `Content`, `Status`; `Submit(...)`, `MarkHandled()` |
| `src/ModMove.Infrastructure/Persistence/Configurations/SupportRequestConfiguration.cs` (mới) | `support_requests`: `full_name VARCHAR(100)`, `contact VARCHAR(100)`, `content VARCHAR(1000)`, `status VARCHAR(20)`, index `status` |
| `tests/ModMove.UnitTests/Domain/...` | Test: nội dung rỗng bị từ chối |

Endpoint gửi yêu cầu (UC-GST-05) làm sau, trong Phase 1. Dispatcher có cần xem danh sách không: còn là câu hỏi mở #3 trong DECISIONS.

## Bước 6 — Collation và DEFAULT ở mức DB (Q2, Q3)

| File | Sửa |
|---|---|
| `src/ModMove.Infrastructure/Persistence/ApplicationDbContext.cs` | `modelBuilder.UseCollation("utf8mb4_unicode_ci")` (Pomelo đặt cho database, bảng và cột) |
| Migration `AlignSchemaWithSds` (sinh bằng `dotnet ef`, rồi **thêm tay** phần SQL bên dưới vào `Up`) | `ALTER TABLE … ALTER COLUMN … SET DEFAULT …` cho 26 cột có DEFAULT trong Report-4 (trạng thái `'Active'`/`'Pending'`/`'Offline'`/`'Unpaid'`/`'Open'`/`'Requested'`, cờ `1`/`0`, bộ đếm và số dư `0`, `send_count 1`, `row_version 0`) và `created_at DEFAULT CURRENT_TIMESTAMP(6)` cho 20 bảng; `Down` gỡ lại |

Vì sao DEFAULT chỉ nằm trong SQL migration, không khai báo `HasDefaultValue` trong model: với cột `bool`/số, EF bỏ qua giá trị bằng mặc định C# (`false`, `0`) khi INSERT. Ví dụ tạo coupon `is_active = false` sẽ bị DB đổi thành `1`. Để model không có default, EF luôn gửi đủ giá trị; DEFAULT trong DB chỉ phục vụ ai INSERT bằng SQL tay.

Bỏ qua: `ON UPDATE CURRENT_TIMESTAMP(6)` cho `updated_at` (interceptor đã gán).

## Bước 7 — Dọn DB local (Q8)

Chạy một lần trên DB của Hiếu (không phải migration, các DB mới không có các dòng này):

```sql
DELETE FROM platform_settings WHERE setting_key IN
  ('PEAK_SURCHARGE','BULKY_SURCHARGE','MATCHING_TIMEOUT_MINUTES','PAYOS_SESSION_MINUTES','AUTO_COMPLETE_HOURS','COMPLAINT_WINDOW_HOURS');
```

## Bước 8 — Tài liệu

| File | Sửa |
|---|---|
| `docs/DECISIONS.md` | B1: giữ `minimum_fare`, công thức `max(minimum_fare, base_fare + price_per_km × km) + phí bốc xếp − giảm giá` (Q6), xóa câu hỏi mở #2; thêm C8 (bước 1); ghi lựa chọn hoa hồng/coupon |
| `docs/BACKEND-TASKS.md` | Đánh dấu B4/B13/B14 đã có schema; Hoàng chỉ làm use case, không tạo migration cho các phần này |
| `docs/plans/2026-10-21-report4-changes.md` (mới) | Danh sách chỗ Report-4 mục 3 cần sửa để Hiếu tự sửa file Word (Q7): thêm 2 cột hoa hồng vào 3.10, cột `description` vào 3.9, bảng mới `support_requests`, bỏ `PEAK_SURCHARGE`/`BULKY_SURCHARGE` khỏi ví dụ ở 3.8, ghi chú collation/DEFAULT |

## Kiểm tra cuối

1. `dotnet build` không warning, `dotnet test` pass, `dotnet format --verify-no-changes`.
2. Chạy app ở Development trên DB local: migration áp dụng được, seed chạy lại không lỗi.
3. Chạy lại script đối chiếu cột với Report-4 + DECISIONS: chỉ còn các khác biệt đã ghi ở bước 8.
4. Subagent `reviewer` trên diff, rồi PR vào `develop`.

## Thứ tự và rủi ro

- Bước 1 → 5 sửa model, bước 6 sinh migration một lần cuối cùng (không sinh migration nhiều lần).
- Đổi collation làm migration `ALTER` mọi cột chuỗi: trên DB local thì nhanh; DB của Hoàng/Huy chỉ cần `dotnet run` lại.
- `ShippingOrder.Complete` đổi chữ ký: hiện chưa có handler nào gọi, nên không vỡ code của ai.
