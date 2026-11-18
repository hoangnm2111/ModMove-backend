# Plan GPS: backend (ModMove-backend)

Ngày: 2026-11-18 · Plan tổng: [2026-11-17-gps-tracking-improvements.md](2026-11-17-gps-tracking-improvements.md) · Nhánh: `feature/hieu` → PR vào `develop` · Người làm: Hiếu (nhóm đã đồng ý).

**Làm backend trước app ở task BE-3:** app gửi `recordedAt` cho backend cũ thì backend bỏ qua trường lạ và lấy giờ server, nên điểm gửi bù cũ sẽ ghi đè vị trí mới. Các trường mới đều tùy chọn, app cũ vẫn chạy với backend mới.

Không cần migration.

## BE-1. Tài liệu và luật (PR riêng, làm đầu tiên)

Đụng file dùng chung (`docs/DECISIONS.md`, `docs/TEAM-PLAN.md`, `Domain/Entities`), nên tách PR riêng và báo nhóm.

| File | Sửa |
|---|---|
| `docs/DECISIONS.md` B12 | Tài xế (app) gửi GPS qua REST `POST api/driver/location`; hub `SendLocation` vẫn giữ cho client khác. `driver_location_logs` ghi **tối đa 1 điểm mỗi 60 giây cho mỗi đơn** |
| `docs/DECISIONS.md` mới **B18** | App tài xế chạy foreground service có thông báo khi Online, dừng khi Offline. Chỉ phát APK; lên Google Play phải khai báo foreground service loại `location` và video giải thích (Q1, Q10) |
| `docs/DECISIONS.md` mới **B19** | Điểm có `accuracy` > 100 m bị bỏ: không lưu, không đẩy SignalR, vẫn trả 204. Không có `accuracy` thì nhận như cũ (Q2). Không lọc điểm nhảy xa (M2) |
| `docs/DECISIONS.md` mới **B20** | `isMocked = true` → 422 `MOCK_LOCATION` ở mọi môi trường. App không còn tọa độ giả `DRIVER_FAKE_*` và menu mô phỏng GPS. `isMocked` do app tự báo: chặn được app Fake GPS, không chặn được client bị sửa (Q3, M1) |
| `docs/DECISIONS.md` mới **B21** | `recordedAt` tùy chọn, mặc định giờ server. Quá 30 giây ở tương lai hoặc cũ hơn 10 phút → 400. Điểm không mới hơn `last_location_at` thì không ghi đè vị trí cuối và không đẩy SignalR (Q4) |
| `docs/TEAM-PLAN.md` mục 2 | Code GPS và bản đồ ở backend, app tài xế, web Dispatcher do Hiếu làm (nhóm đồng ý 2026-11-18) |
| `src/ModMove.Domain/Entities/DriverLocationLog.cs` | Chú thích: "GPS point of a driver who has an order, at most one per minute (B12)" |
| `.claude/rules/ownership.md` | Thêm phần GPS/bản đồ vào mục "Được sửa thoải mái" |

Kiểm tra: `dotnet build` (chỉ đổi chú thích).

## BE-2. Lọc và gửi bù điểm GPS (B19, B20, B21)

**Viết test trước, xác nhận fail, rồi mới code.**

### Code

| File | Thay đổi |
|---|---|
| `Domain/Constants/BusinessRules.cs` | `GpsMaxAccuracyMeters = 100m`, `GpsMaxClockSkew = 30 giây`, `GpsMaxBacklog = 10 phút`, `GpsLogInterval = 60 giây` (dùng ở BE-3) |
| `Domain/Enums/LocationUpdateResult.cs` (mới, file dùng chung) | `Applied`, `Stale`, `Inaccurate` |
| `Domain/Entities/DriverProfile.cs` | `public LocationUpdateResult UpdateLocation(decimal latitude, decimal longitude, DateTimeOffset at, bool isMocked = false, decimal? accuracyMeters = null)`: `isMocked` → `DomainException("...", "MOCK_LOCATION")`; `accuracyMeters > GpsMaxAccuracyMeters` → `Inaccurate`, không đổi gì; `at <= LastLocationAt` → `Stale`, không đổi gì; còn lại ghi và trả `Applied`. Tham số mặc định nên 6 chỗ gọi trong test không phải sửa |
| `Features/Tracking/Commands/UpdateDriverLocation/UpdateDriverLocationCommand.cs` | Thêm `decimal? Accuracy = null, bool IsMocked = false, DateTimeOffset? RecordedAt = null` ở cuối record (test hub so sánh record vẫn đúng) |
| `.../UpdateDriverLocationCommandValidator.cs` | `Accuracy` ≥ 0 khi có |
| `.../UpdateDriverLocationCommandHandler.cs` | Sau kiểm tra tài khoản: `at = RecordedAt ?? now`; ngoài cửa sổ → `ValidationException([new ValidationFailure("RecordedAt", ...)])`. Gọi `UpdateLocation`. `Inaccurate` → return, không lưu. `Applied` → log khi có đơn, đẩy SignalR như cũ. `Stale` → log khi có đơn, không đẩy. `LocationUpdated.At` dùng `at` |
| `WebApi/Controllers/Driver/DriverController.cs` | `UpdateDriverLocationRequest(decimal Latitude, decimal Longitude, decimal? Accuracy = null, bool IsMocked = false, DateTimeOffset? RecordedAt = null)` truyền thẳng xuống command |
| `WebApi/Hubs/TrackingHub.cs` | Không đổi (B12: app dùng REST) |
| `docs/api/README.md` | Dòng `POST api/driver/location`: 3 trường mới và luật B19–B21; thêm mã `MOCK_LOCATION` (422) vào bảng mã lỗi |

### Test

`tests/ModMove.UnitTests/Domain/DriverProfileLocationTests.cs` (mới):
- `isMocked` → `DomainException` mã `MOCK_LOCATION`, vị trí giữ nguyên.
- `accuracy` 100.1 → `Inaccurate`, không đổi; đúng 100 → `Applied`; không có `accuracy` → `Applied`.
- `at` sớm hơn hoặc bằng `LastLocationAt` → `Stale`, không đổi.

`tests/ModMove.IntegrationTests/TrackingTests.cs` (thêm, dùng `TestApp.Time`):
- `accuracy` 150 khi đang có đơn: không log, không đẩy SignalR, `last_*` giữ nguyên.
- `isMocked` → 422 `MOCK_LOCATION`, không log, không đẩy.
- `recordedAt` = now + 31 giây → `ValidationException`; now − 10 phút 1 giây → `ValidationException`; now − 10 phút → nhận.
- Điểm gửi bù cũ hơn `last_location_at` khi có đơn: có log, không đẩy SignalR, `last_*` giữ nguyên.
- Toàn bộ test GPS hiện có vẫn pass không sửa (request không có trường mới chạy như cũ).

## BE-3. Lịch sử GPS 60 giây một lần (B12 mới)

| File | Thay đổi |
|---|---|
| `Application/Common/Interfaces/Persistence/IDriverLocationLogRepository.cs` | `Task<DateTimeOffset?> GetLastRecordedAtAsync(long orderId, CancellationToken ct)` |
| `Infrastructure/Persistence/Repositories/DriverLocationLogRepository.cs` | `Set.Where(l => l.OrderId == orderId).MaxAsync(l => (DateTimeOffset?)l.RecordedAt, ct)` (dùng index `(order_id, recorded_at)` có sẵn) |
| `tests/ModMove.IntegrationTests/Fakes.cs` (dùng chung) | `InMemoryLocationLogs` cài thêm hàm trên |
| `UpdateDriverLocationCommandHandler.cs` | Đổi `IRepository<DriverLocationLog>` sang `IDriverLocationLogRepository`. Chỉ thêm log khi chưa có log nào của đơn hoặc `at - lastLog >= GpsLogInterval`. Điểm cũ hơn log cuối không ghi |

Test (`TrackingTests.cs`):
- 2 điểm cách 30 giây cùng đơn → 1 log, `last_*` cập nhật cả 2 lần, SignalR đẩy cả 2 lần.
- Cách đúng 60 giây → 2 log.
- Đổi đơn khác → log ngay điểm đầu của đơn mới.
- Sửa test `Driver_with_an_order_updates_the_map_and_the_order_followers_B12` nếu cần thì chỉ đổi cách đếm, không đổi điều nó kiểm tra.

Ảnh hưởng đã xem: `GetOrderTracking.VehicleSql` (vị trí xe khi đổi tài xế, 7.2) có thể cũ tới 60 giây. Chấp nhận vì xe gặp sự cố thường đứng yên.

## BE-4. Xóa log theo đợt (JOB-GPS-01)

| File | Thay đổi |
|---|---|
| `DriverLocationLogRepository.DeleteRecordedBeforeAsync` | Lặp: xóa tối đa 10.000 dòng cũ nhất mỗi lần (`OrderBy(Id).Take(10_000).ExecuteDeleteAsync`) tới khi xóa ít hơn 10.000; trả tổng số. Interface và fake không đổi |

Kiểm tra:
- MySQL không cho `LIMIT` trong subquery `IN`. Trước khi merge, chạy job trên database local và xem SQL Pomelo sinh ra trong log. Nếu Pomelo báo lỗi thì đổi cách: lấy 10.000 `Id` trước rồi xóa theo danh sách đó.
- Test hiện có `Points_older_than_30_days_are_deleted_and_newer_ones_kept` vẫn pass.

## Xong mỗi task

`dotnet format ModMove.slnx`, `dotnet build ModMove.slnx` (không warning mới), `dotnet test ModMove.slnx` (kể cả ArchitectureTests), `dotnet list ModMove.slnx package --vulnerable --include-transitive`. Trước PR chạy agent `reviewer`. Commit `[TICKET-ID] feat|docs|test: ...` trên `feature/hieu`.
