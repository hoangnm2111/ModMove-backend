# Plan code phần Hiếu: tài khoản nhận hoàn tiền

Ngày: 2026-11-11 · Plan tổng: [2026-11-11-refund-bank-account.md](2026-11-11-refund-bank-account.md) · Chia việc: [2026-11-11-refund-bank-account-tasks.md](2026-11-11-refund-bank-account-tasks.md) · Kiểm tra trên `develop` `9c9868a`.

**Điều kiện bắt đầu:** nhóm chốt Q1, Q2, Q4–Q8. Plan này viết theo đề xuất trong plan tổng. Q3 đã chốt: tài khoản PayOS chỉ là gợi ý, khách phải xác nhận.

**Chia PR:**
- **PR A (dùng chung, báo nhóm):** T1–T3. Có `Domain/Entities`, migration, `Application/Common`, nên Hoàng và Huy phải biết trước.
- **PR B (phần khách):** T4–T8, sau khi PR A merge.

Mỗi task theo thứ tự: viết test, chạy thấy fail đúng lý do, viết code, chạy `dotnet test`.

---

## T1. Ghi luật vào DECISIONS (PR A)

**File:** `docs/DECISIONS.md`. Thêm dòng **R-01** vào mục 2, ngay sau dòng B9/3.3. Nội dung chính:

- **Q1:** tài khoản nhận lưu trên từng khoản hoàn (snapshot).
- **Q2:** khiếu nại và hoàn sự cố bắt buộc gửi kèm tài khoản; hoàn do hủy thì khai sau, có thông báo nhắc.
- **Q3:** tài khoản người trả qua PayOS chỉ là gợi ý; khách phải xác nhận.
- **Q4:** Admin được duyệt khi chưa có tài khoản; chỉ chặn bước chi (`REFUND_BANK_ACCOUNT_REQUIRED`).
- **Q5:** Admin báo chuyển lỗi thì khoản hoàn sang `Failed`; khách sửa tài khoản thì về lại `Approved`.
- **Sửa được hay khóa:**
  - Khách sửa được khi khoản hoàn đang `Requested`, `Approved` hoặc `Failed`.
  - Đã `Processed`, `Rejected` hoặc `Withdrawn` thì khóa (`REFUND_PAYOUT_LOCKED`).
- **Q6:** ngân hàng lấy từ master data `BANK`.
- **Q7:**
  - Khách xem số tài khoản đã che.
  - Admin xem đầy đủ.
  - Dispatcher và tài xế không thấy.
- **B15:** giữ nguyên.

## T2. `Refund`: tài khoản nhận (PR A)

**Test trước:** `tests/ModMove.UnitTests/Domain/RefundDomainTests.cs`

| Test | Mong đợi |
|---|---|
| `SetPayoutAccount_is_allowed_while_Requested_Approved_or_Failed_R01` (Theory) | Lưu 4 trường + `PayoutInfoUpdatedAt`, `RowVersion` tăng |
| `SetPayoutAccount_on_a_closed_refund_is_REFUND_PAYOUT_LOCKED` (Theory: Processed, Rejected, Withdrawn) | `DomainException`, code `REFUND_PAYOUT_LOCKED` |
| `SetPayoutAccount_on_a_failed_refund_returns_it_to_Approved_R01` | `Failed` → `Approved`, `PayoutFailureReason` = null |
| `MarkProcessed_without_a_payout_account_is_REFUND_BANK_ACCOUNT_REQUIRED` | code đúng, trạng thái vẫn `Approved` |
| `MarkFailed_keeps_the_reason` | `Failed`, `PayoutFailureReason` = lý do |
| `HasPayoutAccount_is_false_until_set` | |

**Code:** `src/ModMove.Domain/Entities/Refund.cs`

```csharp
public string? PayoutBankCode { get; private set; }      // master data BANK code = BIN 6 số
public string? PayoutBankName { get; private set; }      // snapshot tên lúc khai
public string? PayoutAccountNumber { get; private set; }
public string? PayoutAccountHolder { get; private set; } // chữ hoa không dấu
public DateTimeOffset? PayoutInfoUpdatedAt { get; private set; }
public string? PayoutFailureReason { get; private set; }

public bool HasPayoutAccount => PayoutAccountNumber is not null;

/// R-01: Requested/Approved/Failed; Failed goes back to Approved so the Admin can pay again (UC-ADM-18 5a).
public void SetPayoutAccount(string bankCode, string bankName, string accountNumber, string accountHolder, DateTimeOffset now)
{
    if (RefundStatus is RefundStatus.Processed or RefundStatus.Rejected or RefundStatus.Withdrawn)
        throw new DomainException("This refund is closed; its payout account can no longer change.", "REFUND_PAYOUT_LOCKED");
    (PayoutBankCode, PayoutBankName, PayoutAccountNumber, PayoutAccountHolder) = (bankCode, bankName, accountNumber, accountHolder);
    PayoutInfoUpdatedAt = now;
    if (RefundStatus == RefundStatus.Failed) { RefundStatus = RefundStatus.Approved; PayoutFailureReason = null; }
    RowVersion++;
}
```

- **`MarkProcessed`:** sau `EnsureStatus(Approved)`, nếu `!HasPayoutAccount` thì ném `DomainException(..., "REFUND_BANK_ACCOUNT_REQUIRED")`.
- **`MarkFailed`:** đổi chữ ký thành `MarkFailed(string reason)` và lưu `PayoutFailureReason`. Hiện chỉ có test gọi method này (mục "Test cũ phải sửa"); Hoàng dùng nó ở K6.

**Code:** `src/ModMove.Infrastructure/Persistence/Configurations/RefundConfiguration.cs`

```csharp
builder.Property(r => r.PayoutBankCode).HasMaxLength(20);
builder.Property(r => r.PayoutBankName).HasMaxLength(100);
builder.Property(r => r.PayoutAccountNumber).HasMaxLength(19);
builder.Property(r => r.PayoutAccountHolder).HasMaxLength(100);
builder.Property(r => r.PayoutFailureReason).HasMaxLength(1000);
```

**Test cũ phải sửa** (bước Arrange gọi `SetPayoutAccount` trước khi chi). Đây là test của người khác, nên ghi rõ trong mô tả PR:

| File | Dòng |
|---|---|
| `tests/ModMove.UnitTests/Domain/RefundDomainTests.cs` | 104–124 |
| `tests/ModMove.UnitTests/Domain/UserAndCouponTests.cs` | 277–278 (`MarkFailed()` → `MarkFailed("…")`) |
| `tests/ModMove.IntegrationTests/AdminRefundsTests.cs` | 115 và test `payout` |
| `tests/ModMove.IntegrationTests/CustomerRefundTests.cs` | 305, 352 |
| `tests/ModMove.IntegrationTests/PickupAndReceiptTests.cs` | 243–245 |

Seed demo không gọi `MarkProcessed`, nên không phải sửa.

## T3. Migration + sự kiện thông báo (PR A)

**Migration:** dùng skill `add-migration`. Trước khi tạo, `git pull origin develop`.

```bash
dotnet ef migrations add RefundPayoutAccount -p src/ModMove.Infrastructure -s src/ModMove.WebApi -o Persistence/Migrations
```

- 6 cột nullable, collation `utf8mb4_unicode_ci` (C9), không `HasDefaultValue`.
- Kiểm tra file sinh ra chỉ có `AddColumn` trên `refunds`.
- Chạy `Up` rồi `Down` trên DB tạm.
- Nếu K2 của Hoàng merge trước: tạo lại migration sau khi pull.

**Sự kiện:** `src/ModMove.Application/Common/Notifications/CustomerNotifications.cs`. Thêm 2 giá trị `CustomerEvent` để mọi người dùng chung `NotifyCustomerAsync`, không cần helper mới:

```csharp
/// R-01: a refund needs the customer's bank account before the Administrator can pay it.
RefundBankInfoRequired,   // ("REFUND_BANK_INFO_REQUIRED", "Cần tài khoản nhận hoàn tiền", "Đơn {0} có khoản hoàn tiền. Vui lòng cung cấp tài khoản ngân hàng để nhận tiền.")
/// R-01: the Administrator's transfer failed; the customer fixes the account (UC-ADM-18 5a).
RefundPayoutFailed,       // ("REFUND_PAYOUT_FAILED", "Chuyển hoàn tiền thất bại", "Chuyển hoàn tiền cho đơn {0} không thành công. Vui lòng kiểm tra lại tài khoản nhận.")
```

- **Không ghi số tài khoản vào nội dung thông báo** (security.md mục 1).
- **Test:** `tests/ModMove.UnitTests/Application/MatchingAndNotificationUnitTests.cs`, mỗi event có type đúng và body không có số.
- **Ghi chú cho Hoàng (K3) và Huy:** sau `SaveChangesAsync`, gọi `notifications.NotifyCustomerAsync(order, CustomerEvent.RefundBankInfoRequired, ct)`.

---

## T4. Tài khoản nhận dùng chung cho phía khách (PR B)

**File mới:** `src/ModMove.Application/Features/Refunds/PayoutAccountInput.cs`

```csharp
/// R-01: either the account the customer paid from through PayOS (UseSuggested) or one typed in.
public sealed record PayoutAccountInput(bool UseSuggested, string? BankCode, string? AccountNumber, string? AccountHolder);
```

**Validator dùng chung:** thêm vào `CustomerRefundRules.cs` (method `ValidPayoutAccount`).

- **`UseSuggested = true`:** ba trường kia phải null.
- **Ngược lại:**
  - `BankCode` phải có, ≤ 20.
  - `AccountNumber` khớp `^\d{6,19}$`.
  - `AccountHolder` khớp `^[A-Za-z ]{2,100}$` sau khi trim.
- Message tiếng Anh, theo mẫu `ValidEvidence`.

**Resolver** (internal, cùng file hoặc `PayoutAccountResolver.cs`). Handler gọi, rồi `refund.SetPayoutAccount(...)`:

```csharp
internal static async Task SetPayoutAccountAsync(
    this Refund refund, PayoutAccountInput input, IMasterDataRepository masterData,
    IPaymentTransactionRepository payments, DateTimeOffset now, CancellationToken ct)
```

- **Tự nhập:**
  - `masterData.RequireActiveAsync("BANK", code, "PayoutAccount.BankCode", ct)`: mã lạ hoặc đã tắt → 400.
  - Tên ngân hàng lấy từ master data.
  - Chủ tài khoản: `Trim()`, gộp khoảng trắng, `ToUpperInvariant()`.
- **`UseSuggested`:**
  - Lấy giao dịch `Paid` mới nhất của đơn có `payer_account_number`. Cần K2, xem T7 cho phần đọc.
  - Không có → `DomainException("No PayOS payer account to reuse; enter one.", "NO_SUGGESTED_PAYOUT_ACCOUNT")`.
  - BIN không có trong `BANK` → cùng mã lỗi.
  - Lấy số và tên từ giao dịch; tên đổi sang chữ hoa không dấu.
- **Vì sao phải xác nhận ở server:** gợi ý trả về cho khách đã che số, nên khách không gửi lại được số đầy đủ. Server tự chép từ giao dịch.
- **Repo:** cần method đọc giao dịch trên repo payment, thuộc mảng của Hoàng.
  - Nếu Hoàng chưa có, thêm `Task<PaymentTransaction?> GetLatestPaidWithPayerAsync(long orderId, CancellationToken ct)` vào `IPaymentTransactionRepository` và báo Hoàng.
  - Fake thêm vào `Fakes.cs`: dùng chung, ghi vào PR.

**Test:** `tests/ModMove.UnitTests/Application/CustomerValidatorTests.cs`. Chỉ thêm test mới, **không đụng các dòng WIP đang có của bạn**.

| Trường hợp | Kết quả |
|---|---|
| Số tài khoản 5 số, 20 số, có chữ | 400 |
| Tên có dấu (`NGUYỄN`), quá 100 ký tự, rỗng | 400 |
| `UseSuggested` kèm `BankCode` | 400 |
| Hợp lệ | pass |

## T5. Gửi khiếu nại, hoàn sự cố kèm tài khoản (PR B)

**Đổi command:**

```csharp
SubmitComplaintCommand(long OrderId, string ReasonCode, string? Reason, IReadOnlyList<string>? EvidenceUrls,
    decimal RequestedAmount, PayoutAccountInput PayoutAccount)
RequestIncidentRefundCommand(long IncidentId, string Reason, IReadOnlyList<string>? EvidenceUrls,
    decimal RequestedAmount, PayoutAccountInput PayoutAccount)
```

- **Validator:** `RuleFor(x => x.PayoutAccount).NotNull().ValidPayoutAccount()`. Nếu Q2 chọn "không bắt buộc lúc gửi" thì đổi thành nullable và bỏ `NotNull`.
- **Handler:** sau khi `order.FileComplaint(...)` hoặc `order.RequestIncidentRefund(...)` trả về `refund`, và trước `refunds.Add`, gọi `await refund.SetPayoutAccountAsync(request.PayoutAccount, ...)`. Không đổi chữ ký method của `ShippingOrder`.

**Test:** `tests/ModMove.IntegrationTests/CustomerRefundTests.cs` và `IncidentRefundTests.cs`.

- Gửi kèm tài khoản → refund lưu đủ 4 trường, tên chủ tài khoản viết hoa.
- Thiếu `payoutAccount` → 400.
- `bankCode` không có trong `BANK` → 400.
- `UseSuggested` khi đơn trả qua PayOS có tài khoản người trả → chép đúng số.
- `UseSuggested` khi không có tài khoản người trả → `NO_SUGGESTED_PAYOUT_ACCOUNT`.
- Sửa các test hiện có gọi 2 command này để truyền thêm `PayoutAccount`. Nên viết helper `TestPayout.Valid` trong file test.

## T6. Use case `SetRefundPayoutAccount` (PR B)

**Thư mục:** `src/ModMove.Application/Features/Refunds/Commands/SetRefundPayoutAccount/`

```csharp
public sealed record SetRefundPayoutAccountCommand(long RefundId, PayoutAccountInput PayoutAccount) : ICommand;
```

**Handler** (mẫu như `WithdrawRefund`):

1. `currentUser.LoadUserAsync(users, ct)`. Tài khoản `Inactive` hoặc `Locked` → 403 `ACCOUNT_INACTIVE`.
2. Đọc `refund` theo id, rồi đọc đơn.
   - Không có refund → 404.
   - Đơn không phải của khách → 404. Theo cách `GetMyRefundDetail` đang làm, không để lộ là refund có tồn tại.
3. `await refund.SetPayoutAccountAsync(...)`.
4. `SaveChangesAsync`: trùng thao tác với Admin thì `RowVersion` báo 409 `CONFLICT`.
5. Nếu refund vừa từ `Failed` về `Approved`: báo Admin. Kiểm tra `INotificationService` có kênh tới Admin không; không có thì bỏ qua, Admin thấy trong danh sách.

**Controller:** `src/ModMove.WebApi/Controllers/Customer/RefundsController.cs`

```csharp
/// <summary>Bank account to receive this refund (R-01); allowed until the refund is paid, rejected or withdrawn.</summary>
[HttpPut("{id:long}/payout-account")]
public async Task<IActionResult> SetPayoutAccount(long id, PayoutAccountInput request, CancellationToken cancellationToken)
{
    await Sender.Send(new SetRefundPayoutAccountCommand(id, request), cancellationToken);
    return NoContent();
}
```

**Test:** `tests/ModMove.IntegrationTests/RefundPayoutAccountTests.cs` (file mới)

| Test | Kết quả |
|---|---|
| Hoàn do hủy đang `Requested`, khách khai | 204, lưu đủ |
| `Failed` → khai lại | `Approved`, lý do lỗi bị xóa |
| `Processed` / `Rejected` / `Withdrawn` | 422 `REFUND_PAYOUT_LOCKED` |
| Khoản hoàn của khách khác | 404 |
| Chưa đăng nhập | 401 |
| Đăng nhập là Driver hoặc Dispatcher | 403 |
| Tài khoản `Locked` | 403 `ACCOUNT_INACTIVE` |
| Admin chi cùng lúc (sửa `RowVersion` giữa chừng như các test 409 hiện có) | 409 |

Với các case 401 và 403 theo route, theo mẫu test phân quyền controller đang có (xem `EV-IT-SECURITY`).

## T7. Khách xem tài khoản nhận và gợi ý (PR B)

**`RefundRow.cs`:** thêm cột vào `Columns`.

```sql
r.payout_bank_code AS PayoutBankCode, r.payout_bank_name AS PayoutBankName,
r.payout_account_number AS PayoutAccountNumber, r.payout_account_holder AS PayoutAccountHolder,
r.payout_failure_reason AS PayoutFailureReason, r.bank_reference_code AS BankReferenceCode
```

**Thuộc tính tính sẵn:**
- `NeedsPayoutAccount => PayoutAccountNumber is null && RefundStatus is "Requested" or "Approved" or "Failed"`.
- `MaskedAccountNumber => "•••• " + last4`.

**Response mới:**

```csharp
public sealed record PayoutAccountView(string BankCode, string BankName, string MaskedAccountNumber, string AccountHolder);
```

- **`MyRefundSummaryResponse`:** thêm `NeedsPayoutAccount`.
- **`MyRefundDetailResponse`:** thêm
  - `PayoutAccountView? PayoutAccount`
  - `bool NeedsPayoutAccount`
  - `string? PayoutFailureReason`
  - `string? BankReferenceCode` (chỉ khi `Processed`)
  - `PayoutAccountView? SuggestedPayoutAccount`

**Gợi ý** (chỉ ở chi tiết, chỉ khi `NeedsPayoutAccount`). Query thứ hai, có tham số:

```sql
SELECT p.payer_bank_bin AS BankCode, b.name AS BankName, p.payer_account_number AS AccountNumber, p.payer_account_name AS AccountHolder
FROM payment_transactions p
JOIN master_data_items b ON b.category = 'BANK' AND b.code = p.payer_bank_bin AND b.is_active = 1
WHERE p.order_id = @OrderId AND p.transaction_status = 'Paid' AND p.payer_account_number IS NOT NULL
ORDER BY p.paid_at DESC
LIMIT 1
```

- Trả về đã che số.
- Cột `payer_*` chưa có (K2 chưa merge) thì chưa viết query này. Để `SuggestedPayoutAccount = null`, ghi `// shortcut: waits for K2` và mở lại khi K2 xong.

**Test:** `tests/ModMove.IntegrationTests/MyRefundsQueryTests.cs`. Test query chạy được tới đâu thì tùy cách các test query Dapper hiện có làm; nếu chỉ test được map thì test hàm che số và `NeedsPayoutAccount` ở unit test.

- Danh sách và chi tiết **không bao giờ** có số đầy đủ.
- `NeedsPayoutAccount` đúng theo trạng thái.
- `BankReferenceCode` chỉ có khi `Processed`.

**Kiểm tra Q7 phía Dispatcher:** `GetComplaintById`, `GetIncidentById` không select cột `payout_*`. Grep sau khi xong T2 là đủ, phần này của Huy.

## T8. Báo khách khi hủy đơn đã trả (PR B)

**File:** `src/ModMove.Application/Features/Orders/Commands/CancelOrder/CancelOrderCommandHandler.cs`

- Khi đã `refunds.Add(...)` (dòng 38), sau `SaveChangesAsync` gọi `notifications.NotifyCustomerAsync(order, CustomerEvent.RefundBankInfoRequired, ct)`.
- **Test:** `tests/ModMove.IntegrationTests` (file test `CancelOrder` hiện có).
  - Hủy đơn PayOS đã trả → khách nhận `REFUND_BANK_INFO_REQUIRED`.
  - Đơn tiền mặt → không nhận.

## T9. Tài liệu (trong PR tương ứng)

| PR | File | Sửa |
|---|---|---|
| A | `docs/DECISIONS.md` | R-01 (T1) |
| A | `docs/api/README.md` bảng mã lỗi | `REFUND_BANK_ACCOUNT_REQUIRED`, `REFUND_PAYOUT_LOCKED`, `NO_SUGGESTED_PAYOUT_ACCOUNT`; sự kiện 8.3 `REFUND_BANK_INFO_REQUIRED`, `REFUND_PAYOUT_FAILED` |
| B | `docs/api/README.md` dòng 68–72 | Body mới có `payoutAccount`; dòng mới `PUT api/customer/refunds/{id}/payout-account`; field mới ở GET |
| B | `docs/api/README.md` lookups | `GET api/public/lookups/banks` (H4, làm khi K1 xong) |
| B | `TEAM-PLAN.md` mục 4 | Thêm và đánh `[x]` sau khi merge |
| sau B | SRS Report-3 | UC-CUS-30 (mới), UC-CUS-17, UC-CUS-21, UC-CUS-29, BF-08, GB-54, MSG69, màn chi tiết hoàn tiền |

## T10. Lookup ngân hàng (sau K1)

`LookupsController` thêm:

```csharp
/// <summary>Banks for <c>payoutAccount.bankCode</c> (code = BIN).</summary>
[HttpGet("banks")]
public Task<IReadOnlyList<ContentResponse>> Banks(CancellationToken cancellationToken) =>
    Sender.Send(new GetPublicContentQuery(PublicContentCategories.Bank), cancellationToken);
```

- Thêm `public const string Bank = "BANK";` vào `PublicContentCategories`. Hiếu sửa được (ngoại lệ trong TEAM-PLAN).
- **Thống nhất với Hoàng ở K1:** `code` là BIN 6 số, khớp `counterAccountBankId` của PayOS và `img.vietqr.io` (Q8). `name` là tên ngắn, ví dụ `Vietcombank`.
- **Test:** lookup chỉ trả mục đang bật.

---

## Kiểm tra trước mỗi PR

- `dotnet format ModMove.slnx`, `dotnet build ModMove.slnx` (không warning mới), `dotnet test ModMove.slnx`.
- Quét diff không có số tài khoản thật hay secret.
- Không đưa file WIP vào commit: `VehicleTypeTests.cs`, `CustomerCoverageGapTests.cs`, phần đang sửa của `CustomerValidatorTests.cs`, `docs/report4`, `docs/report5`, `docs/test-plan`.
- Chạy subagent `reviewer` trước khi mở PR.

## Ước lượng

| PR | Task | Khoảng |
|---|---|---|
| A | T1–T3 | 0,5 ngày |
| B | T4–T8 | 1,5 ngày |
| Sau K1/K2 | T7 (gợi ý), T10 | 0,5 ngày |
