# Plan GPS: app mobile (ModMove-mobile)

Ngày: 2026-11-18 · Plan tổng: [2026-11-17-gps-tracking-improvements.md](2026-11-17-gps-tracking-improvements.md) · Nhánh: `feature/hieu` (cắt từ `spike/mapvina-gl`, xem MB-0) · Người làm: Hiếu (nhóm đã đồng ý).

Trạng thái: **code xong, test xanh (analyze sạch, `flutter test` 231 pass / 4 skip); chưa thử tay trên máy ảo** (xem "Kết quả" cuối file).

## MB-0. Chuẩn bị nhánh (làm trước mọi task) ✅

Repo lúc đầu ở `spike/mapvina-gl` với 24 file chưa commit (bản đồ MapVina GL), trong đó có `app_config.dart` và `driver_api.dart` mà plan này cũng sửa.

1. Đã commit phần dở lên `spike/mapvina-gl` (`93799af`). Không xóa, không stash bỏ.
2. **Sửa so với plan cũ:** không cắt từ `develop` được. `develop` thiếu hẳn `driver_location_source.dart`, `realtime_source.dart`, `order_detail_*` và `test/customer/` (spike đi trước `develop` 37 commit, `develop` đi trước spike 0). Nên `feature/hieu` cắt từ đầu spike. Không merge gì vào `develop`, không push.
3. `flutter pub get`, `flutter test` xanh trên nhánh mới trước khi sửa: 219 pass, 4 skip.

**Việc còn lại của nhóm:** PR của `feature/hieu` sẽ kéo theo cả 37 commit của spike nếu spike chưa vào `develop`. Merge spike vào `develop` trước (hoặc đổi nhánh đích của PR sang spike).

Chạy lệnh flutter bằng `D:\flutter_windows_3.44.1-stable\flutter\bin\flutter.bat` (chưa có trong PATH).

## MB-1. Gửi GPS lỗi không làm hỏng vòng đồng bộ (A2) ✅

| File | Thay đổi |
|---|---|
| `lib/features/driver_trip/data/repositories/api_driver_trip_repository.dart` `_syncOnce` | `_pushLocation()` nằm trong `try` riêng: lỗi mạng/5xx thì điểm vẫn nằm trong hàng đợi (MB-4) và vòng đi tiếp tải chuyến, đơn, ca. `ACCOUNT_INACTIVE` (mọi status) vẫn ném lên để tắt Online như cũ |

Test (`test/driver_backend_test.dart`, nhóm `GPS tài xế`): `sendLocation` trả 500 hoặc mất mạng → vẫn gọi `trips/active` và `matching/orders`; `ACCOUNT_INACTIVE` tắt Online và hủy nghe GPS.

Lệch plan cũ: test nằm ở `driver_backend_test.dart`, không phải `driver_trip_repository_test.dart` (file đó chỉ test mock repository).

## MB-2. Luồng vị trí theo thời gian (A3) ✅

| File | Thay đổi |
|---|---|
| `driver_location_source.dart` | Thêm `GpsSample` (toạ độ, `accuracy`, `isMocked`, `at`). `DriverLocationSource` có `current()` (lúc bật Online, có xin quyền) và `watch()` (`Geolocator.getPositionStream` với `AndroidSettings(accuracy: high, intervalDuration: 5 giây, distanceFilter: 0)`). **`distanceFilter` phải là 0**: xe đứng yên vẫn cần điểm mới, nếu không nút GB-52 khóa sau 30 giây và BE coi tài xế mất kết nối sau 5 phút (6.4) |
| `api_driver_trip_repository.dart` | Online ⇔ đang nghe `watch()`: `_online` là setter gọi `_syncWatch()` nên mọi chỗ đổi trạng thái (status từ BE, `setOnline`, đăng xuất, tài khoản bị khóa, `close`) bật/tắt luồng. Mỗi điểm: `_fix = GpsFix(điểm, giờ đo)` và vào hàng đợi. Lỗi luồng (tắt GPS, mất quyền): `gpsEnabled = false`, vòng đồng bộ sau thử nghe lại |

Test: nguồn giả phát 3 điểm → `_fix` là điểm cuối, `fix.at` là giờ đo; `setOnline(false)` → hết người nghe.

Ruling: mỗi vòng đồng bộ chỉ gửi những gì đã có trong hàng đợi, không gọi `getCurrentPosition` nữa (gọi lặp sẽ hiện hộp xin quyền mỗi 5 giây nếu người dùng từ chối). Hệ quả: lần mở app khi BE đã ghi Online thì điểm đầu tiên đến sau vài giây, không phải ngay trong vòng đầu.

## MB-3. Foreground service khi Online (B1, B18) ✅ code · ⏳ chưa thử tay

| File | Thay đổi |
|---|---|
| `android/app/src/main/AndroidManifest.xml` | Thêm `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`. `POST_NOTIFICATIONS` đã có sẵn từ FCM. Không thêm `ACCESS_BACKGROUND_LOCATION`. Service `foregroundServiceType="location"` do `geolocator_android` 5.2.1 tự khai báo trong manifest của nó |
| `driver_location_source.dart` `watch()` | `foregroundNotificationConfig: ForegroundNotificationConfig(notificationTitle: 'ModMove', notificationText: 'ModMove đang chia sẻ vị trí khi bạn Online', enableWakeLock: true, setOngoing: true)` |
| `api_driver_trip_repository.dart` | `setForeground(false)` chỉ dừng timer khi Offline; Online ra nền vẫn đồng bộ. Offline thì `_syncWatch` hủy luồng và dừng timer nền |
| `README.md` | Mục "Driver GPS (backend thật)": B18 chỉ APK, lên Play phải khai báo loại `location` và video |

Thử tay trên Pixel_9_Pro (**chưa làm**, xem Kết quả):
- Online, về màn hình chính 6 phút: dispatcher vẫn thấy tài xế, có thông báo thường trực.
- Offline: thông báo tắt, không gửi điểm.
- Từ chối quyền thông báo: vẫn gửi được GPS (thông báo chỉ là bắt buộc của Android; code không phụ thuộc quyền này).

## MB-4. Gửi bù khi mất mạng (B3, B21) ✅

**Cần backend BE-2** (có trong `feature/hieu` của repo backend: `UpdateDriverLocationCommand` đã nhận `Accuracy`, `IsMocked`, `RecordedAt`). Merge BE-2 vào `develop` trước khi merge PR mobile này, vì app gửi `recordedAt` cho backend cũ thì điểm cũ ghi đè vị trí mới.

| File | Thay đổi |
|---|---|
| `driver_api.dart` `sendLocation` | Thêm `accuracy` (bỏ qua nếu null), `isMocked`, `recordedAt` (ISO 8601 UTC) |
| `api_driver_trip_repository.dart` | Hàng đợi FIFO trong bộ nhớ (`Queue<GpsSample>`). **Điểm vào hàng đợi ngay khi stream phát**, không đợi vòng đồng bộ (mất mạng thì `status()` đã lỗi từ đầu vòng, nên vào hàng đợi ở bước gửi sẽ không bao giờ có điểm). Mỗi vòng gửi từ đầu hàng đợi: lỗi mạng/5xx → dừng và giữ (đúng thứ tự); 400/422 → bỏ điểm đó, không thử lại; điểm cũ hơn 10 phút bị bỏ trước khi gửi; tối đa 120 điểm (bỏ điểm cũ nhất). Offline hoặc đăng xuất thì xóa hàng đợi |

Test: mạng lỗi 3 vòng rồi có lại → gửi 4 điểm đúng thứ tự, có `recordedAt`; điểm cũ hơn 10 phút bị bỏ; giữ 120 điểm mới nhất; 400 không thử lại; body có `accuracy`, `isMocked`, `recordedAt` UTC.

Hàng đợi chỉ nằm trong bộ nhớ: tắt app là mất. Đủ cho demo.

## MB-5. Lọc điểm và bỏ giả lập (B2, B4, B19, B20) ✅ code · ⏳ chưa kiểm tra isMocked trên máy ảo

| File | Thay đổi |
|---|---|
| `driver_location_source.dart` | `GpsSample` mang `accuracy` và `isMocked` của `Position`. Bỏ nhánh `AppConfig.driverFakeLocation` |
| `lib/core/config/app_config.dart` | Bỏ `DRIVER_FAKE_LAT/LNG` và `driverFakeLocation`. Giữ `USE_MOCK_DRIVER` |
| `api_driver_trip_repository.dart` | `supported` trả `{}`, `simulate` luôn ném `TripFailure`; bỏ `_override`, `_gpsOff` và các nhánh `nearPickup/advanceRoute/nearDropoff/gpsLost/gpsRestored` |
| `driver_simulation_sheet.dart` + 6 màn (dashboard, job details, pickup navigation, cargo inspection, pickup approval, delivery) | Thêm `tripSimulationAction(context)`: trả `null` khi `supported` rỗng. Màn truyền `onHelp: tripSimulationAction(context)`; `null` thì nút `?` rơi về sheet trợ giúp mặc định của `AppHeader`, nút "Kịch bản thử" của dashboard ẩn. Mock repository vẫn thấy menu. Nhãn "Mô phỏng • GPS…" ở `driver_home_screen.dart` chỉ hiện khi có nút kịch bản |
| `api_driver_trip_repository.dart` | 422 `MOCK_LOCATION`: tắt Online, hủy nghe GPS, `notice = "Tắt ứng dụng giả lập vị trí để nhận đơn"`, và báo Offline lên BE (`availabilityGateway` hoặc `api.setOnline(false)`, lỗi thì bỏ qua) để vòng sau không bật lại từ `status` |
| `README.md`, `docs/driver-pickup-mock.md`, `docs/mapvina-android.md` | Bỏ hướng dẫn `DRIVER_FAKE_*`; ghi cách test: Extended Controls → Location (đặt điểm hoặc Routes → Play route) |

**Kiểm tra trước khi merge (chưa làm được):** trên Pixel_9_Pro đặt vị trí bằng Extended Controls, in `Position.isMocked`. Nếu là `true` thì dừng lại và báo nhóm, vì B20 sẽ chặn luôn máy ảo. Máy này không có AVD nào (`flutter emulators` trống) và chưa chấp nhận license Android SDK.

Test: `isMocked` được gửi đúng; 422 `MOCK_LOCATION` tắt Online, hiện thông báo, báo Offline lên BE; repository thật `supported` rỗng; `driver_arrival_radius_test.dart` vẫn pass (test đó chỉ dùng `ActiveTripState`, không dựa vào `_override`). Hai test cũ dựa vào `_override` (kịch bản `nearPickup` giữ vị trí giả, kịch bản giả lập trên máy ảo) đã bỏ vì tính năng không còn.

## MB-6. Khách nhận vị trí tài xế realtime (A1) ✅

Payload thật của BE: `LocationUpdated { driverProfileId, orderId?, latitude, longitude, at }`. Hub chỉ có `JoinOrder` và `SendLocation`, **không có `LeaveOrder`**.

| File | Thay đổi |
|---|---|
| `lib/core/notifications/realtime_source.dart` | `RealtimeSource` thêm `Stream<Map<String, Object?>> locations`, `Future<void> joinOrder(int)`, `void leaveOrder(int)`. Lớp `OrderRooms` nhớ các đơn đã join và gọi lại `JoinOrder` ở `start()` (sau khi nối xong) và `onreconnected` (B12). `leaveOrder` chỉ quên đơn trong danh sách nhớ (không có `LeaveOrder` trên hub; nhóm trên server mất khi ngắt kết nối). `stop()` xóa danh sách để tài khoản sau không join đơn của tài khoản trước. `NoRealtimeSource` cài rỗng |
| `lib/app/app.dart` | Thêm `RepositoryProvider<RealtimeSource>` bọc `RealtimeBridge` để màn hình lấy được nguồn |
| `customer_models.dart` | `OrderTracking.withDriver(điểm, giờ)`: đổi vị trí và giờ tài xế, `driverStale = false`, giữ điểm lấy/giao và ETA |
| `order_detail_cubit.dart` | Nhận `locations`, lọc theo `orderId`, bỏ payload sai dạng; chưa có tracking (chưa tải xong) thì bỏ qua |
| `order_detail_screen.dart` | Mở màn: `joinOrder`; đóng: `leaveOrder`. Giữ timer 60 giây gọi `loadTracking` làm mới ETA (mỗi lần tốn 1 lượt MapVina, giới hạn 10 lần/phút) |

Test (`test/customer/order_location_realtime_test.dart`): `LocationUpdated` đúng đơn → vị trí, giờ đổi, hết mất tín hiệu, ETA giữ nguyên; khác đơn hoặc sai dạng → bỏ qua; chưa có tracking → bỏ qua; `OrderRooms` gọi lại `JoinOrder` cho đơn còn nhớ sau khi kết nối lại, không gọi cho đơn đã `leave`.

Chưa test được: dây nối `onreconnected` của SignalR thật (cần hub chạy). Logic nhớ và gọi lại nằm trong `OrderRooms` nên đã test riêng.

## Xong mỗi task

`flutter analyze`, `flutter test`. Thử tay trên Pixel_9_Pro với backend local. Commit `[TICKET-ID] feat|fix|test: ...` trên `feature/hieu`.

## Kết quả (2026-11-18)

Commit trên `feature/hieu`: `5bb5897` (MB-6), `657607e` + `24fa7a8` (MB-1..5). WIP spike: `93799af`. Mã `[MB-…]` trong tiêu đề commit là mã task của plan này, không phải ticket thật: đổi lại khi biết `TICKET-ID`.

| Kiểm tra | Kết quả |
|---|---|
| `flutter analyze` | Không có lỗi |
| `flutter test` | 231 pass, 4 skip (nền trước khi sửa: 219 pass, 4 skip) |
| Test mới | 10 test nhóm `GPS tài xế` + 4 test MB-6; bỏ 2 test mô phỏng GPS cũ |
| Build APK, thử tay trên Pixel_9_Pro với backend local (MB-2, MB-3, MB-5, MB-6) | **Chưa làm**: máy không có AVD/thiết bị Android, license SDK chưa chấp nhận, chưa chạy backend local |
| `Position.isMocked` của máy ảo | **Chưa kiểm tra** |

Việc người làm cần làm tiếp:
1. Trên Pixel_9_Pro: kiểm tra `isMocked`, rồi chạy các bước thử tay ở MB-3 và MB-2 (đứng yên 6 phút vẫn gửi mỗi 5 giây, nút "Đã đến" không khóa).
2. Merge spike vào `develop` (hoặc đổi nhánh đích PR), merge BE-2 trước PR này.
3. Đổi tiền tố commit theo ticket thật.
