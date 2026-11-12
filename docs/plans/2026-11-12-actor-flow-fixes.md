# Plan sửa các điểm còn mở sau đợt rà luồng theo actor

Ngày: 2026-11-12 · Kiểm tra trên `develop` `45bf32f` cộng các thay đổi chưa commit trên `feature/hieu`.

Plan này gồm 3 lỗi sửa được ngay (T1–T3) và 9 quyết định Hiếu chốt ngày 2026-11-12 (T4–T13; T4 và T10 không cần sửa code). Mỗi task theo thứ tự: viết test, chạy thấy fail đúng lý do, viết code, chạy `dotnet test ModMove.slnx`.

## Quyết định đã chốt

| # | Quyết định | Task |
|---|---|---|
| Q4 | Tiền mặt người gửi đã trả cho tài xế cũ: tài xế cũ **đưa tận tay** cho tài xế mới lúc bàn giao hàng. Hệ thống giữ nguyên: tài xế mới nhận thu nhập và bị trừ hoa hồng; không ghi nhận việc bàn giao và **không thông báo** gì thêm cho hai tài xế | T0 (chỉ ghi luật) |
| Q5 | Dispatcher đề xuất 0đ thì Admin **chỉ được từ chối**, không duyệt được | T5 |
| Q6a | Admin khóa khách: hệ thống **tự hủy đơn `Pending`** của khách. Đơn `Accepted`…`InTransit` vẫn chặn khóa | T6 |
| Q6b | Dispatcher **tự mở sự cố** trên một đơn đang chạy, để đổi tài xế hoặc hủy đơn khi không ai báo sự cố. Sự cố này **không chặn chuyến**; mô tả là ghi chú nội bộ, khách và tài xế chỉ thấy câu chung | T7 |
| Q7 | Một Dispatcher đã nhận xử lý sự cố thì Dispatcher khác không xử lý được nữa, kể cả khi hai người bấm cùng lúc. Cùng khóa chống ghi đè cho hồ sơ tài xế (hai Admin duyệt và từ chối cùng lúc) | T8 |
| Q8 | Tài xế **không tắt online được** khi đang có đơn | T9 |
| Q9 | Migration `RegistrationHeldUntilVerified` xóa hẳn tài khoản chưa xác minh email: chấp nhận | T10 |
| Q10 | Admin khóa Dispatcher: mọi sự cố người đó đang giữ (`InProgress`) tự về `Open`. Dispatcher tự **trả lại sự cố** được, ví dụ khi hết ca | T11 |
| Q11 | Khách còn khoản hoàn hoặc khiếu nại đang mở vẫn **khóa được**. Khoản hoàn giữ nguyên, Admin xử lý ngoài hệ thống. Admin thấy cảnh báo trước khi khóa | T12 |
| Q12 | Tài xế **xóa được tài khoản** (= `Inactive`, B8) khi không có đơn đang chạy, không có yêu cầu rút tiền `Pending` và ví bằng 0 | T13 |

## Chia PR và người làm

Ranh giới theo `docs/TEAM-PLAN.md` mục 2. Hiếu viết hết thì vẫn phải báo chủ phần trước khi mở PR.

- **PR A (dùng chung, cả nhóm duyệt):** T0 và mọi thay đổi ở `Domain/Entities`, `Application/Common`, migration, `Fakes.cs` của các task dưới.
- **PR B (Hoàng: ví, hoàn tiền, hồ sơ, tài xế):** T1, T3, T5, T9; controller `Controllers/Driver` của T13.
- **PR C (Huy: Dispatcher):** T7, T8; lệnh trả lại sự cố của T11.
- **PR D (Hiếu: tài khoản, WebApi):** T2, T6, T12; phần khóa Dispatcher của T11; use case xóa tài khoản tài xế của T13.

Thứ tự: T8 → T7 → T11 (cùng sửa `InTransitIncident`). T7 → T6 (thông báo lỗi của T6 nhắc tới việc Dispatcher mở sự cố). T6 → T12 (cùng sửa `UpdateUserStatusCommandHandler`). T9 → T13 (cùng dùng `GoOffline`). Các task còn lại độc lập.

---

## T0. Ghi luật vào DECISIONS (PR A)

**File:** `docs/DECISIONS.md`. Thêm vào đúng dòng, kèm ngày `2026-11-12`:

- **7.2:** tiền mặt người gửi đã trả cho tài xế cũ thì tài xế cũ đưa tận tay cho tài xế mới lúc bàn giao hàng; hệ thống không ghi nhận và không thông báo việc này.
- **B9:** đề xuất 0đ thì Admin chỉ từ chối được (`REFUND_ZERO_PROPOSAL`).
- **B8:** sửa câu đã thêm hôm nay. Khóa khách: đơn `Pending` bị hệ thống hủy (đơn PayOS đã trả được tạo hoàn 100%); đơn `Accepted`…`InTransit` vẫn chặn. Khóa tài xế đang có đơn: Dispatcher mở sự cố rồi đổi tài xế hoặc hủy đơn trước.
- **7.6:** thêm sự cố do Dispatcher mở: không chặn chuyến, mô tả là ghi chú nội bộ, khách và tài xế chỉ nhận câu chung.
- **B17/8.3 hoặc dòng mới:** sự cố chỉ một Dispatcher xử lý; người thứ hai nhận 409 `CONFLICT`.
- **B5 hoặc GB-03:** tài xế không tắt online khi đang có đơn `Accepted`…`InTransit` (`DRIVER_HAS_ACTIVE_ORDER`).
- **7.6 hoặc dòng sự cố mới:** Dispatcher trả lại sự cố đang giữ (`InProgress` → `Open`); Admin khóa hoặc ngưng Dispatcher thì các sự cố người đó giữ tự về `Open`.
- **B8:** khách còn khoản hoàn/khiếu nại mở vẫn khóa được; Admin xử lý khoản hoàn ngoài hệ thống. Tài xế xóa được tài khoản khi không có đơn đang chạy, không có rút tiền `Pending`, ví bằng 0 (`ACCOUNT_HAS_PENDING_WITHDRAWAL`, `ACCOUNT_HAS_WALLET_BALANCE`).

**File:** `docs/api/README.md`. Thêm mã lỗi `REFUND_ZERO_PROPOSAL`, `DRIVER_HAS_ACTIVE_ORDER`, `ACCOUNT_HAS_PENDING_WITHDRAWAL`, `ACCOUNT_HAS_WALLET_BALANCE`; endpoint mới của T7, T11, T13; trường mới của T12; ghi chú 403 `ACCOUNT_INACTIVE` cho mọi endpoint Dispatcher/Admin (T2).

---

## T1. Duyệt và từ chối rút tiền cùng lúc (PR B)

**Lỗi:** `DriverWallet.CompleteWithdrawal` không tăng `RowVersion`. Admin A xác nhận đã chuyển khoản, Admin B từ chối cùng lúc: lần lưu của B vẫn qua, tài xế vừa nhận tiền vừa được trả lại số dư.

**Test trước:** `tests/ModMove.UnitTests/Domain/DriverWalletTests.cs`

| Test | Mong đợi |
|---|---|
| `CompleteWithdrawal_moves_the_wallet_version` | `RowVersion` tăng 1 sau khi hoàn tất |

**Code:** `src/ModMove.Domain/Entities/DriverWallet.cs`, thêm `RowVersion++;` vào `CompleteWithdrawal`, giống `RejectWithdrawal`.

---

## T2. Dispatcher và Admin bị khóa vẫn dùng được token cũ (PR D)

**Lỗi:** các query của Dispatcher/Admin và 8 command của Admin không kiểm tra trạng thái tài khoản. Token cũ còn sống tối đa 15 phút, trái B8.

**Cách sửa:** một chỗ duy nhất, không sửa từng handler.

- Thêm `src/ModMove.WebApi/Filters/ActiveAccountFilter.cs` (`IAsyncActionFilter`): gửi `IsActiveAccountQuery(userId)` đã có sẵn (hub đang dùng); không Active thì ném `ForbiddenAccessException("The account is not active.", "ACCOUNT_INACTIVE")`.
- `src/ModMove.WebApi/Controllers/RoleConvention.cs`: gắn filter cho thư mục `Dispatcher` và `Admin`. Thư mục `Customer` và `Driver` không cần, handler ở đó đã kiểm tra.

**Test trước:** `tests/ModMove.IntegrationTests/WebApiConventionTests.cs`

| Test | Mong đợi |
|---|---|
| `Dispatcher_and_Admin_controllers_check_the_account_is_active_B8` | Mọi controller trong hai thư mục có `ActiveAccountFilter` |
| `ActiveAccountFilter_refuses_a_locked_account_with_ACCOUNT_INACTIVE` | Tài khoản `Locked`/`Inactive` → 403 đúng mã; `Active` → đi tiếp |

**Đánh đổi:** mỗi request của Dispatcher/Admin tốn thêm một câu `SELECT account_status`.

---

## T3. Hồ sơ tài xế nhận URL giấy tờ bất kỳ (PR B)

**Lỗi:** `SubmitDriverApplicationCommandValidator` chỉ yêu cầu http/https. Mọi ảnh khác đều bắt buộc là ảnh Cloudinary của nhóm.

**Test trước:** `tests/ModMove.IntegrationTests/DriverOnboardingSubmissionTests.cs`

| Test | Mong đợi |
|---|---|
| `Document_urls_outside_our_Cloudinary_are_a_validation_error` | 400 `VALIDATION` |

**Code:** validator nhận `IFileStorage storage`, đổi luật thành `.Must(storage.IsOwnedUrl)` như `ConfirmPickupCommandValidator`.

**Kéo theo:** các test đang dùng URL kiểu `https://cdn/1.jpg` phải đổi sang `https://res.cloudinary.com/<CloudName>/...` của fake.

**Phía app:** người code app tự chuyển việc upload giấy tờ sang `api/driver/uploads` (Cloudinary). Backend chỉ chặn URL lạ, không sửa gì ở app. Merge T3 cùng lúc hoặc sau khi app đã đổi, nếu không app cũ sẽ bị 400 khi nộp hồ sơ.

---

## T4. Tiền mặt khi đổi tài xế (Q4)

Không sửa code, không thêm thông báo. Code hiện tại đã đúng quyết định: tài xế mới nhận thu nhập và bị trừ hoa hồng khi đơn hoàn tất. Chỉ ghi luật vào DECISIONS ở T0.

---

## T5. Đề xuất 0đ thì chỉ được từ chối (PR B, Q5)

**Lỗi:** Admin duyệt được khoản hoàn 0đ. Khiếu nại `Approved` vẫn tính là còn mở, nên đơn không hoàn tất và tài xế không được tất toán.

**Test trước**

| File | Test | Mong đợi |
|---|---|---|
| `RefundDomainTests.cs` | `Approve_with_a_zero_proposal_is_REFUND_ZERO_PROPOSAL` | `DomainException`, vẫn `Requested` |
| `AdminRefundsTests.cs` | `Admin_cannot_approve_a_zero_proposal_but_can_reject_it_B9` | Duyệt → 422; từ chối → `Rejected` |
| `PickupAndReceiptTests.cs` | `A_complaint_proposed_at_zero_and_rejected_lets_the_order_complete_7_3` | Sau khi từ chối, khách xác nhận nhận hàng được |

**Code:** `src/ModMove.Domain/Entities/Refund.cs`, trong `Approve`:

```csharp
if (ProposedAmount == 0)
    throw new DomainException("A 0 VND proposal can only be rejected.", "REFUND_ZERO_PROPOSAL");
```

---

## T6. Khóa khách thì tự hủy đơn `Pending` (PR D, Q6a)

**Hiện tại:** khách có đơn `Pending`…`InTransit` thì không khóa được. Khách cứ giữ một đơn `Pending` là tránh được khóa.

**Sau khi sửa:** `Accepted`…`InTransit` vẫn chặn (`ACCOUNT_HAS_OPEN_ORDERS`). Đơn `Pending` bị hủy trong cùng lần lưu với việc khóa.

**Test trước:** `tests/ModMove.IntegrationTests/AdminUserManagementTests.cs`

| Test | Mong đợi |
|---|---|
| `Locking_a_customer_cancels_their_pending_orders_B8` | Đơn `Cancelled`, người đổi là `Admin`, lý do "Tài khoản bị khóa"; tài khoản `Locked`; một lần lưu |
| `Locking_a_customer_with_a_paid_pending_PayOS_order_queues_a_full_refund_B8` | Có khoản hoàn `Cancellation` bằng `TotalFee` |
| `Locking_a_customer_cancels_the_open_PayOS_code_of_a_pending_order` | Phiên PayOS `Pending` bị hủy ở DB và ở cổng |
| `A_customer_with_an_accepted_order_still_cannot_be_locked_B8` | 422, không đơn nào bị hủy |

Sửa test hôm nay `A_customer_with_an_order_not_yet_delivered_cannot_be_locked_B8`: đổi đơn sang `Accepted`.

**Code**

- `IShippingOrderRepository`: đổi `HasUndeliveredOrdersForCustomerAsync` thành `HasOrdersWithDriverForCustomerAsync` (`Accepted`…`InTransit`); thêm `GetPendingForCustomerAsync(customerId)`. Sửa `ShippingOrderRepository` và `InMemoryOrders`.
- `UpdateUserStatusCommandHandler`: sau khi qua bước chặn, với mỗi đơn `Pending` của khách:
  - `order.Cancel("Tài khoản bị khóa.", ChangedByRole.Admin, admin.Id, now)`;
  - đơn PayOS đã `Paid`: thêm `Refund.Request(..., RefundSourceType.Cancellation, ..., order.TotalFee)` như `CancelUnmatchedOrdersCommandHandler`;
  - hủy phiên PayOS còn `Pending` như `CancelOrderCommandHandler`.
- Sau khi lưu: `PublishOrderStatusAsync` cho từng đơn.

Đoạn hủy phiên PayOS đang có ở `CancelOrderCommandHandler`; chuyển thành một hàm dùng chung trong `Features/Orders` rồi gọi ở cả hai nơi, không chép lần thứ ba.

**Điểm cần biết:** khách bị khóa không đăng nhập được, nên không khai được tài khoản nhận cho khoản hoàn vừa tạo. Khoản hoàn chờ tới khi mở khóa, hoặc xử lý ngoài hệ thống nếu khóa hẳn.

---

## T7. Dispatcher tự mở sự cố (PR C, Q6b)

Use case mới `Features/Incidents/Commands/ReportIncidentByDispatcher/`, làm theo skill `new-use-case`.

**API:** `POST api/dispatcher/orders/{id}/incidents`, body `{ incidentType, description }` → 201 `{ id }`.

**Luật**

- Chỉ khi đơn `Accepted`…`InTransit`, sai thì 422 `INCIDENT_NOT_ALLOWED`.
- Sự cố tạo ra ở `InProgress`, `HandledBy` là Dispatcher vừa mở.
- **Không chặn chuyến:** `IncidentGuard` chỉ xét sự cố do tài xế đang giữ đơn báo, nên không cần sửa.
- Mô tả là ghi chú nội bộ: khách và tài xế chỉ nhận `INCIDENT_IN_PROGRESS` với câu chung.

**Test trước**

| File | Test | Mong đợi |
|---|---|---|
| `ShippingOrderTests.cs` | `A_dispatcher_can_open_an_incident_only_on_a_running_order` (Theory) | `Pending`, `Delivered`, `Completed`, `Cancelled` → `INCIDENT_NOT_ALLOWED` |
| `DispatcherIncidentTests.cs` (mới) | `Dispatcher_opens_an_incident_already_in_their_hands` | `InProgress`, `HandledBy` đúng |
| | `A_dispatcher_incident_does_not_hold_the_trip_7_6` | Tài xế vẫn `arrive`/`pickup`/`deliver` được |
| | `A_dispatcher_incident_can_reassign_or_cancel_the_order` | Hai lệnh sẵn có chạy được trên sự cố này |
| | `Reassign_on_a_dispatcher_incident_looks_near_the_vehicle_7_2` | Dùng GPS cuối của tài xế đang giữ đơn |
| | `The_dispatchers_note_is_not_shown_to_the_customer_or_the_driver_8_3` | Thông báo và các query phía khách/tài xế không chứa mô tả |
| | `Another_dispatcher_cannot_handle_it` | 409 |
| | Truy cập trái phép | Chưa đăng nhập 401, sai vai trò 403, tài khoản khóa 403 |

**Code**

- `ShippingOrder.ReportIncidentByDispatcher(dispatcherId, type, description)`.
- Bốn chỗ đang coi "không phải khách báo" là "tài xế báo" phải đổi thành "người báo có vai trò `Driver`":
  - `IncidentNotifications.DriverConcernedAsync` và `IncidentHandlingRules.ReferencePoint` (handler nạp người báo qua `IUserRepository`);
  - SQL của `GetReplacementDrivers` và `GetOrderTracking` (join `users` lấy `role`).
- Ba query hiển thị:
  - `GetMyIncidents` (khách): chỉ trả mô tả và ảnh khi người báo là khách hoặc tài xế.
  - `GetDriverTripDetail`, `GetDriverIncidents` (tài xế): câu "Khách hàng báo cáo sự cố…" đổi thành câu không nêu người báo khi người báo là Dispatcher.

Không thêm cột: vai trò người báo lấy từ bảng `users`. Nếu Huy muốn có cột `reporter_role` trên `in_transit_incidents` thì đó là một migration riêng.

---

## T8. Sự cố chỉ một Dispatcher xử lý (PR C, Q7)

**Hiện tại:** sự cố `InProgress` đã chặn Dispatcher khác (409). Còn hở khi hai người thao tác cùng lúc trên sự cố `Open`: cả hai đều lưu được, người lưu sau ghi đè.

**Test trước**

| File | Test | Mong đợi |
|---|---|---|
| `PersistenceModelTests.cs` | `Incident_status_is_a_concurrency_token` | `ResolutionStatus` là concurrency token |
| `IncidentResolutionTests.cs` | `A_decision_racing_another_dispatcher_is_a_409` (Theory: start, resolve, reassign, cancel-order, cancel) | `ThrowConflictOnSave` → `ConflictException`, không gửi thông báo nào |

**Code:** `InTransitIncidentConfiguration`: `builder.Property(i => i.ResolutionStatus).IsConcurrencyToken();`, giống `PaymentTransactionConfiguration`. Tạo migration bằng skill `add-migration`; migration có thể rỗng, chỉ đổi snapshot.

**Kèm theo, cùng kỹ thuật (báo Hoàng):** `DriverApplication.ApplicationStatus` cũng thành concurrency token, để hai Admin duyệt và từ chối cùng một hồ sơ không cùng thành công. Gộp chung migration.

---

## T9. Tài xế không tắt online khi đang có đơn (PR B, Q8)

**Test trước**

| File | Test | Mong đợi |
|---|---|---|
| `DriverProfileAvailabilityTests.cs` | `Going_offline_with_an_active_order_is_DRIVER_HAS_ACTIVE_ORDER` | `DomainException`, vẫn `Online` |
| `DriverStatusTests.cs` | `A_driver_cannot_go_offline_mid_trip` (Theory: `Accepted`, `Arrived`, `PickedUp`, `InTransit`) | 422 đúng mã |
| | `A_driver_can_go_offline_once_the_order_is_delivered_or_reassigned` | 204 |

**Code**

- `DriverProfile.GoOffline(bool hasActiveOrder = false)`: có đơn thì ném `DomainException(..., "DRIVER_HAS_ACTIVE_ORDER")`. Cùng kiểu tham số với `DriverWallet.RequestWithdrawal`.
- `SetOnlineStatusCommandHandler`: nhận thêm `IShippingOrderRepository`, truyền `GetActiveOrderIdForDriverAsync(profile.Id) is not null`.
- `UpdateUserStatusCommandHandler` gọi `GoOffline()` không tham số: Admin chỉ khóa được tài xế khi không còn đơn.

---

## T10. Migration xóa tài khoản chưa xác minh (Q9)

Không sửa code. Trước khi deploy bản có `RegistrationHeldUntilVerified` lên production: sao lưu database, vì các dòng `users` có `email_verified = 0` bị xóa và không hoàn tác được. Thêm một dòng nhắc vào `deploy/README.md` trong cùng PR với migration đó.

---

## T11. Trả lại sự cố, và khóa Dispatcher thì nhả sự cố (PR C + PR D, Q10)

**Lỗi đang có:** sự cố `InProgress` chỉ người đã nhận mới xử lý được. Người đó bị khóa hoặc hết ca thì sự cố kẹt, và nếu là sự cố tài xế báo thì chuyến cũng dừng theo (7.6).

**Luật**

- Chỉ Dispatcher đang giữ sự cố mới trả lại được. Người khác nhận 409 `CONFLICT`; sự cố không ở `InProgress` thì 422.
- Trả lại: `InProgress` → `Open`, `HandledBy` = null. Chuyến của tài xế vẫn dừng nếu đó là sự cố tài xế báo.
- Admin chuyển Dispatcher sang `Locked` hoặc `Inactive`: mọi sự cố người đó đang giữ về `Open` trong cùng lần lưu.
- Sau khi lưu, gửi `INCIDENT_RELEASED` tới group Dispatcher (`NotifyDispatchersAsync`) để người khác nhận. Không gửi cho khách và tài xế: với họ sự cố vẫn đang được xử lý.

**Test trước**

| File | Test | Mong đợi |
|---|---|---|
| `InTransitIncidentTests.cs` | `Release_puts_the_incident_back_to_Open_for_another_dispatcher` | `Open`, `HandledBy` null |
| | `Only_the_dispatcher_holding_it_can_release_it` | Người khác → `ConflictException`; `Open`/`Resolved`/`Cancelled` → `DomainException` |
| `IncidentResolutionTests.cs` | `Dispatcher_releases_an_incident_and_another_one_takes_it` | Người thứ hai `start` được; group Dispatcher nhận `INCIDENT_RELEASED` |
| | `A_released_driver_incident_still_holds_the_trip_7_6` | Tài xế vẫn nhận `INCIDENT_PENDING` |
| `AdminUserManagementTests.cs` | `Locking_a_dispatcher_releases_the_incidents_they_hold` (Theory: `Locked`, `Inactive`) | Sự cố của người đó về `Open`; sự cố của Dispatcher khác không đổi |
| | Truy cập trái phép cho endpoint mới | 401, 403 sai vai trò, 403 tài khoản khóa |

**Code**

- `InTransitIncident.Release(long dispatcherId)`, dùng chung kiểm tra người giữ với `IncidentHandlingRules.EnsureCanHandle`.
- Use case mới `Features/Incidents/Commands/ReleaseIncident/`; endpoint `POST api/dispatcher/incidents/{id}/release` → 204.
- `IIncidentRepository.GetInProgressHandledByAsync(long dispatcherId)`; sửa `IncidentRepository` và fake.
- `UpdateUserStatusCommandHandler`: target là Dispatcher và trạng thái mới là `Locked`/`Inactive` thì gọi `Release` cho từng sự cố, rồi gửi `INCIDENT_RELEASED` sau khi lưu.

---

## T12. Khóa khách còn khoản hoàn hoặc khiếu nại mở (PR D, Q11)

Không chặn khóa. Chỉ cho Admin biết trước để xử lý ngoài hệ thống.

**Test trước**

| File | Test | Mong đợi |
|---|---|---|
| `AdminUserManagementTests.cs` | `A_customer_with_an_open_refund_can_still_be_locked_B8` | `Locked`; khoản hoàn giữ nguyên trạng thái |
| | `User_detail_shows_how_many_refunds_are_still_open` | `openRefundCount` đúng (`Requested`, `Approved`, `Failed`) |

**Code**

- `GetUserDetailQueryHandler`: thêm `openRefundCount` (đếm khoản hoàn đang mở trên đơn của khách, 0 với vai trò khác). Web dùng số này để hiện cảnh báo trước khi khóa.
- Không đổi `UpdateUserStatusCommandHandler`.

**Cách Admin xử lý ngoài hệ thống** (ghi vào DECISIONS ở T0):

- Đã có tài khoản nhận: chi như bình thường (`payout`).
- Chưa có tài khoản và khách bị khóa hẳn: Admin từ chối khoản hoàn với ghi chú "Xử lý ngoài hệ thống". Khiếu nại cũng đóng theo cách này, để đơn còn hoàn tất và tài xế được tất toán (7.3).

---

## T13. Tài xế xóa tài khoản (PR B + PR D, Q12)

**Luật**

- Không có đơn `Accepted`…`InTransit` → nếu có thì 422 `ACCOUNT_HAS_OPEN_ORDERS`.
- Không có yêu cầu rút tiền `Pending` → nếu có thì 422 `ACCOUNT_HAS_PENDING_WITHDRAWAL`.
- `WalletTotal` bằng 0 → nếu khác 0 thì 422 `ACCOUNT_HAS_WALLET_BALANCE`. Số dương thì tài xế rút hết trước. Số âm (cọc âm vì hoa hồng đơn tiền mặt, GB-15) thì tài xế nạp cho đủ, hoặc Admin xử lý.
- Xóa: `Inactive`, chuyển offline, xóa FCM token, thu hồi mọi refresh token (9.1). Giữ hồ sơ, xe, ví và lịch sử. Admin mở lại được.

**Test trước**

| File | Test | Mong đợi |
|---|---|---|
| `DriverWalletTests.cs` | `HasPendingWithdrawal_is_true_only_while_a_withdrawal_waits` | |
| `UsersTests.cs` (hoặc file mới `DriverAccountTests.cs`) | `A_driver_with_nothing_open_deletes_the_account_B8` | `Inactive`, `Offline`, token bị thu hồi |
| | `A_driver_with_a_running_order_cannot_delete_the_account` | `ACCOUNT_HAS_OPEN_ORDERS` |
| | `A_driver_with_a_pending_withdrawal_cannot_delete_the_account` | `ACCOUNT_HAS_PENDING_WITHDRAWAL` |
| | `A_driver_with_money_left_or_owed_cannot_delete_the_account` (Theory: dương, âm) | `ACCOUNT_HAS_WALLET_BALANCE` |
| | `A_driver_without_a_profile_deletes_the_account` | Chưa nộp hồ sơ: xóa được |
| | Truy cập trái phép | 401, 403 sai vai trò |

**Code**

- `DriverWallet.HasPendingWithdrawal` (dùng lại điều kiện đang có trong `RequestWithdrawal`).
- Use case mới `Features/Users/Commands/DeleteMyDriverAccount/`, không gộp vào `DeleteMyAccount` của khách vì điều kiện khác hẳn.
- Endpoint `DELETE api/driver/account` trong `Controllers/Driver/AccountController.cs` (phần của Hoàng).
- `GoOffline()` gọi sau khi đã kiểm tra không còn đơn, nên luật T9 không chặn.

---

## Định nghĩa xong

- `dotnet build ModMove.slnx` không warning mới; `dotnet test ModMove.slnx` pass cả `ModMove.ArchitectureTests`; `dotnet format ModMove.slnx --verify-no-changes` sạch.
- `docs/DECISIONS.md` và `docs/api/README.md` khớp code trong cùng PR.
- Chạy `reviewer` trước khi mở từng PR.
- T7 và T8 có SQL và migration mới: chạy thử trên MySQL local, FakeSql không chạy câu SQL thật.
