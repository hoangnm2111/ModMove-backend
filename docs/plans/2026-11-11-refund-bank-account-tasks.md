# Chia việc: tài khoản nhận hoàn tiền của khách

Ngày: 2026-11-11 · Plan chi tiết: [2026-11-11-refund-bank-account.md](2026-11-11-refund-bank-account.md) · Ranh giới thư mục: `TEAM-PLAN.md` mục 2.

**Đã chốt Q3:** tài khoản người trả mà PayOS gửi trong webhook (`counterAccount*`) chỉ dùng để **gợi ý điền sẵn**. Khách bấm xác nhận "hoàn về tài khoản này" hoặc sửa sang tài khoản khác. PayOS không gửi thì khách tự nhập. Admin vẫn chuyển khoản tay (GB-27).

**Cần cả nhóm chốt trước khi code:** Q1, Q2, Q4–Q8 ở mục 3 của plan. Các việc bên dưới viết theo đề xuất trong plan.

## 1. Thỏa thuận chung (chốt trước, hai bên code theo)

| Thứ | Tên |
|---|---|
| Cột `refunds` | `payout_bank_code`, `payout_bank_name`, `payout_account_number`, `payout_account_holder`, `payout_info_updated_at`, `payout_failure_reason` |
| Cột `payment_transactions` | `payer_bank_bin`, `payer_account_number`, `payer_account_name` (nullable; PayOS gửi chuỗi rỗng thì lưu `null`) |
| Method `Refund` | `SetPayoutAccount(bankCode, bankName, accountNumber, accountHolder, now)`, `HasPayoutAccount`, `MarkProcessed` chặn khi thiếu tài khoản, `MarkFailed(reason)` |
| Mã lỗi | `REFUND_BANK_ACCOUNT_REQUIRED` (422), `REFUND_PAYOUT_LOCKED` (422) |
| Sự kiện cho khách (8.3) | `REFUND_BANK_INFO_REQUIRED`, `REFUND_PAYOUT_FAILED` |
| Helper thông báo | `CustomerNotifications.RefundBankInfoRequiredAsync(...)`, mọi nơi tạo hoàn do hủy cùng gọi |

## 2. Hiếu

| # | Việc | Chỗ sửa | Ghi chú |
|---|---|---|---|
| H1 | Ghi luật mới vào `DECISIONS.md` (Q1–Q8 sau khi chốt) | `docs/DECISIONS.md` | Làm đầu tiên |
| H2 | `Refund`: 6 trường tài khoản nhận và các method ở mục 1, kèm unit test | `Domain/Entities/Refund.cs`, `RefundConfiguration` | Dùng chung: **PR riêng, báo nhóm** |
| H3 | Migration thêm 6 cột vào `refunds` | `Persistence/Migrations` | Gộp chung PR với H2; tạo sau migration mới nhất trên `develop` |
| H4 | `GET api/public/lookups/banks` đọc master data `BANK` | `Controllers/Public/LookupsController` | Cần K1 |
| H5 | `SubmitComplaint`, `RequestIncidentRefund` nhận `payoutAccount { bankCode, accountNumber, accountHolder }` | `Features/Refunds` (phía khách) | Cần H2, K1 |
| H6 | Use case mới `PUT api/customer/refunds/{id}/payout-account` | `Features/Refunds/Commands/SetRefundPayoutAccount`, `Controllers/Customer/RefundsController` | Chủ đơn mới sửa được; `Failed` → `Approved` |
| H7 | Khách xem: tài khoản đã che (`•••• 6789`), `needsPayoutAccount`, `payoutFailureReason`, **`suggestedPayoutAccount`** lấy từ `payer_*` của giao dịch `Paid` | `GetMyRefunds`, `GetMyRefundDetail` | Gợi ý cần K2 |
| H8 | Helper `RefundBankInfoRequiredAsync` + gọi trong `CancelOrder` | `Common/Notifications`, `Features/Orders` | `Common` dùng chung: báo nhóm |
| H9 | Sửa tài liệu phía khách: `docs/api/README.md`; SRS UC-CUS-30 (mới), UC-CUS-17, UC-CUS-21, UC-CUS-29, BF-08, GB-54, MSG69, màn hình chi tiết hoàn tiền | docs | |

## 3. Hoàng

| # | Việc | Chỗ sửa | Ghi chú |
|---|---|---|---|
| K1 | Master data `BANK` (mã, BIN, tên) + seed từ danh sách ngân hàng VietQR | `Features/MasterData`, seed | Làm sớm, H4 và H5 cần |
| K2 | Lưu tài khoản người trả từ webhook vào `payment_transactions`, có migration | `PaymentTransaction`, `ProcessPayOsWebhook`, `Persistence/Migrations` | Test: `counterAccount*` rỗng thì lưu `null`, không lỗi |
| K3 | Gọi `RefundBankInfoRequiredAsync` khi tạo hoàn do hủy ở JOB-MATCH-01 và ở webhook (tiền đến sau khi hủy, trả trùng) | `CancelUnmatchedOrders`, `ProcessPayOsWebhook` | Cần H8 |
| K4 | Admin xem đầy đủ tài khoản nhận + `hasPayoutAccount` | `GetAdminRefunds`, `GetAdminRefundDetail` | Cần H2 |
| K5 | `payout` trả `REFUND_BANK_ACCOUNT_REQUIRED` khi thiếu tài khoản (luật nằm sẵn trong `Refund.MarkProcessed`, thêm test handler) | `ProcessRefundPayout` | Cần H2 |
| K6 | Use case mới `POST api/admin/refunds/{id}/payout-failed { reason }` → `Failed` + báo khách `REFUND_PAYOUT_FAILED` | `Features/Refunds` (phía Admin), `Controllers/Admin/RefundsController` | Cần H2 |
| K7 | Sửa tài liệu phía Admin: SRS UC-ADM-18 (bước 3, 5a, 6b), MSG70, màn Refund & Withdrawal Approval; Report-4 thiết kế database (`refunds`, `payment_transactions`, `BANK`) | docs | |

## 4. Huy (việc nhỏ)

- Gọi `RefundBankInfoRequiredAsync` trong `CancelOrderByIncident` (cần H8).
- Kiểm tra `GetComplaintById`, `GetIncidentById` của Dispatcher **không** trả số tài khoản (Q7).
- SRS UC-DIS-07: thêm bước thông báo khách khai tài khoản.

## 5. Thứ tự

```text
H1 (luật) ─► H2+H3 (Refund + migration) ─┬─► H5, H6, H7, H8 ─► K3, Huy
K1 (BANK) ─────────────────────────────┤
K2 (payer) ────────────────────────────┴─► H7 (gợi ý)
                                          └─► K4, K5, K6
```

- **Làm song song ngay:** H1, K1, K2.
- **Migration:** H3 và K2 cùng sửa snapshot. Ai merge sau thì `git pull develop` rồi tạo lại migration của mình.
- **Mỗi PR** có test lỗi (thiếu tài khoản, sai trạng thái, đơn của người khác, gọi trùng → 409) và sửa tài liệu trong cùng PR.
