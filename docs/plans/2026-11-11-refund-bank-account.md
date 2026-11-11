# Plan: tài khoản ngân hàng nhận hoàn tiền của khách

Ngày: 2026-11-11 · Trạng thái: **chờ nhóm chốt** (mục 3) trước khi code · Kiểm tra trên `develop` `9c9868a`.

## 1. Hiện trạng

Hoàn tiền đi theo GB-19, GB-27, GB-41: khoản hoàn được tạo, Admin duyệt rồi **tự chuyển khoản tay** và nhập mã tham chiếu. Nhưng hệ thống **không biết chuyển vào tài khoản nào**.

| Chỗ | Có gì | Thiếu gì |
|---|---|---|
| `Refund` (bảng `refunds`) | `BankReferenceCode`, `ProcessedAt` | Ngân hàng, số tài khoản, tên chủ tài khoản của khách |
| API khách `api/customer/refunds/*` | Gửi khiếu nại, hoàn sự cố, trả lời, rút, xem | Không có chỗ nhập hoặc sửa tài khoản nhận tiền |
| API Admin `api/admin/refunds/*` | Xem, duyệt, từ chối, `payout` | Chi tiết không hiện tài khoản nhận; không có thao tác "chuyển thất bại" |
| `Refund.MarkFailed()` | Có trong entity | **Không endpoint nào gọi**; trạng thái `Failed` không có đường quay lại để chuyển lại |
| Webhook PayOS | Nhận `counterAccount*` (tài khoản người trả) | Không lưu, nên không gợi ý được tài khoản để hoàn |
| Tài xế (để so sánh) | `driver_profiles.bank_*`, `PUT api/driver/wallet/bank-account`, `BANK_ACCOUNT_REQUIRED`, MSG66 | — |
| SRS | UC-ADM-18 bước 5 "chuyển vào **tài khoản ngân hàng của khách**", bước 5a "Admin chuyển lại sau" | Không UC, màn hình, trường dữ liệu, thông báo nào cho khách khai tài khoản |

Nơi tạo khoản hoàn (đều không có thông tin nhận tiền):

| Nguồn | Code | Ai tạo |
|---|---|---|
| Khách hủy đơn PayOS đã trả | `CancelOrderCommandHandler` | Hệ thống |
| Không tìm được tài xế | `CancelUnmatchedOrdersCommandHandler` (JOB-MATCH-01) | Hệ thống |
| Dispatcher hủy vì sự cố | `CancelOrderByIncidentCommandHandler` | Hệ thống |
| Tiền đến sau khi hủy, hoặc trả trùng | `ProcessPayOsWebhookCommandHandler` | Hệ thống |
| Khiếu nại | `SubmitComplaint` → `ShippingOrder` | Khách |
| Hoàn do sự cố | `RequestIncidentRefund` → `ShippingOrder` | Khách |

**Hệ quả:** Admin chỉ có số điện thoại, email của khách nên phải liên hệ ngoài hệ thống để hỏi số tài khoản. Việc này không truy vết được, dễ chuyển nhầm, và không đáp ứng được "trong 3 ngày làm việc" (GB-27). Khoản hoàn `Failed` bị kẹt vĩnh viễn.

## 2. Luồng đề xuất

```text
Tạo khoản hoàn ──► Requested ──duyệt──► Approved ──payout (cần tài khoản)──► Processed
     │                │                    │  ▲
     │                │                    │  └── khách sửa tài khoản ◄── Failed ◄── Admin báo chuyển lỗi
     └─ hệ thống tạo: báo khách REFUND_BANK_INFO_REQUIRED (gợi ý tài khoản đã trả qua PayOS nếu có)
Khách khai/sửa tài khoản được ở Requested, Approved, Failed. Processed, Rejected, Withdrawn thì khóa.
```

## 3. Cần nhóm chốt (đề xuất của tôi in đậm)

| # | Câu hỏi | Phương án |
|---|---|---|
| Q1 | Lưu ở đâu? | **Trên từng khoản hoàn** (snapshot, đổi ở khoản này không ảnh hưởng khoản khác). Không thêm vào hồ sơ khách vì M3 không có ví khách |
| Q2 | Khi nào khách khai? | **Khiếu nại, hoàn sự cố: khai luôn khi gửi (bắt buộc). Hoàn do hủy (hệ thống tạo): khai sau, có thông báo nhắc.** Phương án khác: không bắt buộc lúc gửi, chỉ chặn lúc chi |
| Q3 | Dùng tài khoản đã trả qua PayOS? | **Chỉ để gợi ý điền sẵn, khách vẫn phải xác nhận.** Phương án khác: hoàn thẳng về tài khoản nguồn, không hỏi khách (nhanh hơn, nhưng `counterAccount*` có thể trống và khách có thể đã trả hộ bằng tài khoản người khác) |
| Q4 | Admin duyệt khi khách chưa khai? | **Được duyệt; chỉ chặn bước chi** (`REFUND_BANK_ACCOUNT_REQUIRED`) |
| Q5 | Chuyển thất bại thì sao? | **Admin ghi `payout-failed` kèm lý do → `Failed`, báo khách; khách sửa tài khoản → tự về `Approved`; Admin chi lại** (đúng UC-ADM-18 bước 5a) |
| Q6 | Danh sách ngân hàng | **Master data `BANK` (mã, BIN, tên) do Admin quản lý, seed từ danh sách ngân hàng VietQR**; client chọn mã, server kiểm tra |
| Q7 | Ai được xem? | **Khách chủ đơn (số tài khoản che bớt) và Admin (đầy đủ). Dispatcher và tài xế không thấy** |
| Q8 | Có tạo mã VietQR cho Admin quét? | Tùy chọn, làm ở web (ảnh `img.vietqr.io` theo BIN, số tài khoản, số tiền, nội dung `HT{refundId}`), backend không đổi |

## 4. Thay đổi backend

Thứ tự đi theo phụ thuộc. Cột "Của" theo `.claude/rules/ownership.md`: phần dùng chung cần PR riêng và báo nhóm.

| # | Việc | File chính | Của |
|---|---|---|---|
| 1 | Ghi luật vào `DECISIONS.md` (dòng mới, ví dụ R-01) theo kết quả mục 3 | `docs/DECISIONS.md` | Chung |
| 2 | `Refund`: thêm `PayoutBankCode`, `PayoutBankName`, `PayoutAccountNumber`, `PayoutAccountHolder`, `PayoutInfoUpdatedAt`, `PayoutFailureReason`; method `SetPayoutAccount(...)` (chỉ khi `Requested`/`Approved`/`Failed`, `Failed` → `Approved`, tăng `RowVersion`); `MarkProcessed` chặn khi thiếu tài khoản; `MarkFailed(reason)` lưu lý do | `Domain/Entities/Refund.cs` | Chung |
| 3 | Migration thêm 6 cột nullable vào `refunds` (C9: collation, không `HasDefaultValue`) | `Persistence/Migrations`, `RefundConfiguration` | Chung |
| 4 | Master data `BANK` + seed; `GET api/public/lookups/banks` | `MasterData` seed, `LookupsController` | Hoàng (seed), Hiếu (lookup) |
| 5 | `SubmitComplaint`, `RequestIncidentRefund` nhận `payoutAccount { bankCode, accountNumber, accountHolder }` (theo Q2) | `Features/Refunds` phía khách | Hiếu |
| 6 | Use case mới `PUT api/customer/refunds/{id}/payout-account`: chủ đơn mới được sửa (`ORDER_NOT_YOURS`/404), sai trạng thái → `REFUND_PAYOUT_LOCKED`, trùng thao tác Admin → 409 | `Features/Refunds/Commands/SetRefundPayoutAccount`, `Controllers/Customer/RefundsController` | Hiếu |
| 7 | Khách xem: `payoutAccount` (che, ví dụ `•••• 6789`), `needsPayoutAccount`, `payoutFailureReason`, `bankReferenceCode` khi `Processed` | `GetMyRefunds`, `GetMyRefundDetail` | Hiếu |
| 8 | Lưu tài khoản người trả từ webhook PayOS vào `payment_transactions` (`payer_bank_bin`, `payer_account_number`, `payer_account_name`), trả về làm gợi ý ở mục 7 | `ProcessPayOsWebhook`, `PaymentTransaction` + migration | Hoàng |
| 9 | Báo khách `REFUND_BANK_INFO_REQUIRED` khi hệ thống tạo khoản hoàn hoặc khi Admin duyệt mà chưa có tài khoản; gom vào một helper dùng cho cả 4 nơi tạo hoàn do hủy | `CustomerNotifications`, 4 handler ở mục 1 | Chung + chủ từng handler |
| 10 | Admin: chi tiết và danh sách hiện tài khoản nhận đầy đủ + cờ `hasPayoutAccount`; `payout` trả `REFUND_BANK_ACCOUNT_REQUIRED`; endpoint mới `POST api/admin/refunds/{id}/payout-failed { reason }` → `Failed` + `REFUND_PAYOUT_FAILED` cho khách | `GetAdminRefunds`, `GetAdminRefundDetail`, `ProcessRefundPayout`, use case mới, `Controllers/Admin` | Hoàng |
| 11 | Cập nhật `docs/api/README.md` (endpoint, mã lỗi, sự kiện 8.3), `TEAM-PLAN.md` | docs | Chung |

**Kiểm tra đầu vào (security.md mục 2):** `bankCode` phải có trong master data đang bật. `accountNumber` 6–19 chữ số. `accountHolder` chỉ chữ cái không dấu và khoảng trắng, tối đa 100 ký tự, lưu chữ hoa (như tên in trên tài khoản ngân hàng). Không ghi số tài khoản vào log hay nội dung thông báo.

**Mã lỗi mới:** `REFUND_BANK_ACCOUNT_REQUIRED` (422, chi khi thiếu tài khoản), `REFUND_PAYOUT_LOCKED` (422, sửa khi đã `Processed`/`Rejected`/`Withdrawn`).
**Sự kiện mới (8.3):** `REFUND_BANK_INFO_REQUIRED`, `REFUND_PAYOUT_FAILED`.

## 5. Test (viết trước, xác nhận fail)

- **Domain `Refund`:**
  - sửa tài khoản được ở `Requested`/`Approved`/`Failed`, bị chặn ở `Processed`/`Rejected`/`Withdrawn`;
  - `Failed` → `Approved` sau khi sửa;
  - `MarkProcessed` không có tài khoản → `REFUND_BANK_ACCOUNT_REQUIRED`;
  - mỗi thay đổi tăng `RowVersion`.
- **Validator:** ngân hàng lạ hoặc đã tắt, số tài khoản sai dạng, tên có dấu hoặc quá dài → 400.
- **Handler phía khách:**
  - đơn của người khác → 404/`ORDER_NOT_YOURS`; chưa đăng nhập → 401; sai vai trò → 403; tài khoản bị khóa → 403 (security.md mục 7);
  - gửi khiếu nại thiếu tài khoản (nếu Q2 bắt buộc) → 400.
- **Handler phía Admin:**
  - chi khi thiếu tài khoản → 422;
  - `payout-failed` → `Failed` và khách nhận `REFUND_PAYOUT_FAILED`;
  - khách sửa xong, chi lại → `Processed`.
- **Thông báo và dữ liệu nhạy cảm:**
  - cả 4 nơi tạo hoàn do hủy đều gửi `REFUND_BANK_INFO_REQUIRED`;
  - Dispatcher (`GetComplaintById`, `GetIncidentById`) không thấy số tài khoản;
  - danh sách của khách chỉ thấy số đã che.
- **Webhook:** `counterAccount*` trống thì không có gợi ý và không lỗi.

## 6. Tài liệu cần sửa

| Tài liệu | Sửa |
|---|---|
| `docs/DECISIONS.md` | Dòng luật mới (mục 3); B15 giữ "hoàn tiền tự động ngoài phạm vi" |
| SRS Report-3 (bản gốc và `..._v2`) | **BF-08**: thêm bước khách khai tài khoản, Admin chuyển lỗi. **Entity Refund**: thêm thuộc tính tài khoản nhận và lý do lỗi. **UC mới UC-CUS-30** "Cung cấp tài khoản nhận hoàn tiền". **UC-CUS-17, UC-CUS-21**: thêm bước nhập tài khoản. **UC-CUS-29, UC-DIS-07**: thêm thông báo nhắc khai. **UC-ADM-18**: bước 3 hiện tài khoản nhận, bước 5a có lý do và đường quay lại, thêm bước 6b thiếu tài khoản. **GB-54** "Chỉ chi hoàn tiền khi khoản hoàn có tài khoản nhận hợp lệ; khách sửa được tới khi Processed". **MSG69** (thiếu tài khoản nhận), **MSG70** (chuyển hoàn tiền thất bại, cập nhật tài khoản). **Màn hình**: chi tiết hoàn tiền của khách có form tài khoản; màn Refund & Withdrawal Approval hiện tài khoản nhận và nút "Chuyển thất bại" |
| Report-4 thiết kế database | Bảng `refunds` thêm 6 cột; `payment_transactions` thêm 3 cột người trả (nếu làm mục 4.8); master data `BANK` |
| Report-4 thiết kế chi tiết | Sequence của UC-ADM-18 và UC-CUS-30 |
| Report-5 test | Test case cho mục 5 |
| `docs/api/README.md`, `TEAM-PLAN.md` | Như mục 4.11 |

Mã GB-54, MSG69/70, UC-CUS-30 là các số còn trống trong SRS hiện tại (lớn nhất đang là GB-53, MSG68, UC-CUS-29).

## 7. Ngoài phạm vi

- Chi hoàn tự động qua API ngân hàng hoặc PayOS payout (B15, GB-27).
- Lưu tài khoản mặc định trong hồ sơ khách để dùng lại.
- Xác minh tên chủ tài khoản qua dịch vụ tra cứu ngân hàng.

## 8. Rủi ro

- **Đổi API khách:** web và app cần form mới; nếu Q2 chọn bắt buộc lúc gửi khiếu nại thì client cũ bị 400.
- **Dữ liệu cá nhân:** số tài khoản là dữ liệu nhạy cảm, cần đúng phạm vi xem ở Q7 và không ghi log.
- **Khoản hoàn đang mở trên database thật** chưa có tài khoản: sau khi triển khai, Admin vẫn phải hỏi khách các khoản này, hoặc gửi thông báo nhắc một lần.
- **Làm song song:** migration chạm `refunds` và `payment_transactions` dùng chung, cần tạo sau migration mới nhất trên `develop` và báo nhóm.
