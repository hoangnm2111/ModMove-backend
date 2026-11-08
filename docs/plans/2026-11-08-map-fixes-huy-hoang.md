# Plan: sửa lỗi bản đồ trong phần của Huy và Hoàng

Trạng thái: **chờ Hiếu duyệt**. Huy và Hoàng đã đồng ý cho Hiếu sửa (2026-11-08). Nhánh: `feature/hieu`, mỗi lỗi một commit.

Mỗi bước làm theo thứ tự: viết test, chạy thấy fail đúng lý do, sửa code, rồi chạy lại toàn bộ test.

## Lỗi 1 (Huy): danh sách tài xế thay ghép sai tọa độ

**Hiện tại.** `Incidents/Queries/GetReplacementDrivers/GetReplacementDriversQueryHandler.cs:97-98` thay vĩ độ và kinh độ riêng rẽ (`IncidentLatitude ?? PickupLatitude`). Sự cố chỉ có một tọa độ sẽ bị ghép với điểm lấy. Lệnh đổi tài xế (`ReassignIncidentDriverCommandHandler.cs:63`) thì dùng `IncidentHandlingRules.ReferencePoint`, hàm này chỉ dùng điểm sự cố khi có đủ cả hai tọa độ, nên hai chỗ cho kết quả khác nhau. Validator mới (commit `af7de50`) đã chặn dữ liệu mới, nhưng các sự cố cũ trong DB vẫn có thể thiếu một tọa độ.

**Test** (`tests/ModMove.IntegrationTests/GetReplacementDriversTests.cs`):
- `Incident_with_one_coordinate_falls_back_to_the_pickup_point`: sự cố có vĩ độ, kinh độ null; điểm lấy ở Hà Nội; tài xế đứng ngay điểm lấy. Kỳ vọng tài xế **có trong** danh sách. Hiện test này sẽ fail vì điểm gốc lệch khoảng 1.140 km.

**Sửa:**
- `IncidentHandlingRules.cs`: thêm overload `ReferencePoint(decimal? lat, decimal? lon, decimal pickupLat, decimal pickupLon)`. Overload cũ nhận `InTransitIncident` sẽ gọi sang overload mới, nên luật chỉ nằm ở một chỗ.
- `GetReplacementDriversQueryHandler.cs:97-98`: dùng overload mới.

## Lỗi 2 (Huy, chạm cả Hoàng và tracking của Hiếu): hằng số "GPS còn mới" bị định nghĩa hai lần

**Hiện tại.** Hai hằng số cùng 5 phút, xử lý mốc **đúng 5:00** ngược nhau, và mỗi bên có test khóa cách hiểu của mình:

| Nơi dùng | Hằng số | Đúng 5:00 | Test đang khóa cách hiểu |
|---|---|---|---|
| Hoàng: `DriverProfile.HasFreshLocation`, `GetNearbyOrders`, `AcceptOrder`, `GetDriverStatus`, `DriverNotifications` | `BusinessRules.GpsDisconnectAfter` | mất kết nối | `DriverProfileAvailabilityTests` (300 s → false), `AcceptOrderTests:228`, `DriverStatusTests:204`, `GetNearbyOrdersTests:373` |
| Huy: `ListOnlineDrivers` (SQL `>= @FreshSince`), `GetReplacementDrivers` (SQL `>= @FreshSince`), `ReassignIncidentDriver` (`> window`) | `IncidentHandlingRules.GpsFreshWindow` | còn kết nối | không có test cho đúng mốc 5:00 |
| Hiếu: `GetOrderTrackingQueryHandler.cs:85` (`> window`) | `IncidentHandlingRules.GpsFreshWindow` | còn kết nối | `OrderTrackingTests.Position_exactly_5_minutes_old_still_counts_6_4` |

DECISIONS 6.4 ghi "**Quá** 5 phút không gửi GPS: coi là mất kết nối". Đọc đúng chữ thì đúng 5:00 vẫn còn kết nối, ngược với code và test của Hoàng.

**Cần chốt (Q1).** Phương án A được đề xuất:

- **A: đúng 5:00 là mất kết nối** (giữ như entity và test của Hoàng).
  - Sửa chữ trong DECISIONS 6.4 thành "Từ 5 phút trở lên không gửi GPS".
  - Đổi 1 test của Hiếu: `Position_exactly_5_minutes_old_still_counts_6_4` thành `Position_exactly_5_minutes_old_is_stale_6_4`.
  - Code Huy theo luật này.
  - Ít thay đổi nhất, và luật chỉ còn nằm ở `DriverProfile.HasFreshLocation` cùng `BusinessRules`.
- **B: đúng 5:00 vẫn còn kết nối** (đúng nguyên văn DECISIONS).
  - Sửa `DriverProfile.HasFreshLocation` (entity dùng chung) và `GetNearbyOrders` / `GetDriverStatus`.
  - Đổi 4 test của Hoàng.

**Test (theo A):**
- `IncidentResolutionTests`: thêm `Reassign_refuses_a_driver_whose_gps_is_exactly_5_minutes_old_6_4`.
- `DispatcherMonitoringTests`: `ListOnlineDrivers` có `LastSql` chứa `last_location_at > @FreshSince`.
- `GetReplacementDriversTests`: test tương tự cho câu SQL tìm tài xế thay.
- `OrderTrackingTests`: test mốc 5:00 như ở phương án A.

**Sửa (theo A):**
- Xóa `IncidentHandlingRules.GpsFreshWindow`.
- `ListOnlineDriversQueryHandler.cs:46,53` và `GetReplacementDriversQueryHandler.cs:57,90`: dùng `BusinessRules.GpsDisconnectAfter`, SQL đổi `>= @FreshSince` thành `> @FreshSince`. Tham số `FreshSince` giữ nguyên là `now - 5 phút`.
- `ReassignIncidentDriverCommandHandler.cs:54-57`: thay điều kiện tự viết bằng `!newProfile.HasFreshLocation(now)`.
- `GetOrderTrackingQueryHandler.cs:85` (của Hiếu): `now - at >= BusinessRules.GpsDisconnectAfter`.
- `docs/DECISIONS.md` 6.4: sửa chữ như ở phương án A.

## Lỗi 3 (Huy và Hoàng): công thức Haversine bị viết hai lần

**Hiện tại.** `Matching/GeoDistance.cs` (Hoàng) và `IncidentHandlingRules.HaversineKm` (Huy) là hai bản giống hệt nhau. `GeoDistance` đã có unit test (`ShippingOrderMatchingTests`) và đang được `Common/Notifications/DriverNotifications.cs` dùng.

**Sửa:** giữ `GeoDistance`, xóa `IncidentHandlingRules.HaversineKm` và `ToRadians`. `GetReplacementDriversQueryHandler.cs:110` và `ReassignIncidentDriverCommandHandler.cs:64` chuyển sang gọi `GeoDistance.HaversineKm`. Đây chỉ là dọn code, hành vi không đổi, nên không cần test mới: các test hiện có của Incidents đã đủ để bắt lỗi nếu có.

Không chuyển `GeoDistance` sang `Application/Common`, vì đó là thư mục dùng chung cần PR riêng. `Common/Notifications` cũng đang gọi thẳng `Features.Matching.GeoDistance`.

## Kiểm tra

- `dotnet build` (0 warning mới), `dotnet test` (gồm ArchitectureTests), `dotnet format --verify-no-changes` cho các file đã sửa.
- Chạy agent `reviewer` trên diff.

Dự kiến 3 commit:
- `fix: replacement drivers use the incident point only when it has both coordinates`
- `fix: one GPS freshness rule (6.4) for matching, dispatcher map, reassign and tracking`
- `refactor: incidents reuse GeoDistance`

## File sẽ sửa

| File | Chủ | Lỗi |
|---|---|---|
| `Features/Incidents/IncidentHandlingRules.cs` | Huy | 1, 2, 3 |
| `Features/Incidents/Queries/GetReplacementDrivers/GetReplacementDriversQueryHandler.cs` | Huy | 1, 2, 3 |
| `Features/Incidents/Commands/ReassignIncidentDriver/ReassignIncidentDriverCommandHandler.cs` | Huy | 2, 3 |
| `Features/Monitoring/Queries/ListOnlineDrivers/ListOnlineDriversQueryHandler.cs` | Huy | 2 |
| `Features/Tracking/Queries/GetOrderTracking/GetOrderTrackingQueryHandler.cs` | Hiếu | 2 |
| `docs/DECISIONS.md` (6.4) | chung | 2 |
| Test: `GetReplacementDriversTests`, `IncidentResolutionTests`, `DispatcherMonitoringTests`, `OrderTrackingTests` | Huy, Hiếu | 1, 2 |

Code của Hoàng (`Matching/`, `Drivers/`, `DriverProfile`) **không sửa** nếu chọn A. `Fakes.cs`, entity, enum và migration không đổi.
