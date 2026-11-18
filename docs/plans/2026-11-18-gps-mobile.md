# Plan GPS: app mobile (ModMove-mobile)

Ngày: 2026-11-18 · Plan tổng: [2026-11-17-gps-tracking-improvements.md](2026-11-17-gps-tracking-improvements.md) · Nhánh: `feature/hieu` (cắt từ `develop`) → PR vào `develop` · Người làm: Hiếu (nhóm đã đồng ý).

## MB-0. Chuẩn bị nhánh (làm trước mọi task)

Repo đang ở `spike/mapvina-gl` với 24 file chưa commit (bản đồ MapVina GL). Trong đó có 2 file plan này cũng sửa: `lib/core/config/app_config.dart` và `lib/features/driver_trip/data/driver_api.dart`.

1. Commit phần dở lên `spike/mapvina-gl`. Không xóa, không stash bỏ.
2. Nếu spike sắp xong: merge spike vào `develop` trước, rồi mới cắt `feature/hieu`. Nếu chưa: cắt `feature/hieu` từ `develop`, chấp nhận gộp 2 file trên khi spike merge sau.
3. `flutter pub get`, `flutter test` xanh trên nhánh mới trước khi sửa.

Chạy lệnh flutter bằng `D:\flutter_windows_3.44.1-stable\flutter\bin\flutter.bat` (chưa có trong PATH).

## MB-1. Gửi GPS lỗi không làm hỏng vòng đồng bộ (A2)

| File | Thay đổi |
|---|---|
| `lib/features/driver_trip/data/repositories/api_driver_trip_repository.dart` `_syncOnce` | Bọc `_pushLocation()` riêng: lỗi thì ghi nhận (đẩy điểm vào hàng đợi ở MB-4) và đi tiếp tải chuyến, đơn, ca |

Test (`test/driver_trip_repository_test.dart`): `sendLocation` ném lỗi → vòng đó vẫn gọi tải chuyến và danh sách đơn; `ACCOUNT_INACTIVE` vẫn tắt Online như cũ.

## MB-2. Luồng vị trí theo thời gian (A3)

| File | Thay đổi |
|---|---|
| `lib/features/driver_trip/data/driver_location_source.dart` | Thêm `Stream<Position> watch()` dùng `Geolocator.getPositionStream(locationSettings: AndroidSettings(accuracy: high, intervalDuration: 5 giây, distanceFilter: 0))`. **Không dùng `distanceFilter` > 0**: xe đứng yên phải vẫn có điểm mới, nếu không nút GB-52 khóa sau 30 giây và tài xế mất kết nối sau 5 phút (6.4). `current()` giữ lại cho lúc bật Online |
| `api_driver_trip_repository.dart` | Khi Online: nghe `watch()`, lưu điểm mới nhất vào `_fix`; `_pushLocation` gửi điểm mới nhất thay vì gọi `getCurrentPosition` mỗi lần. Offline hoặc đăng xuất: hủy stream |

Test: nguồn vị trí giả phát 3 điểm → `_fix` là điểm cuối; Offline → hủy nghe. Thử tay trên máy ảo: đứng yên 6 phút vẫn gửi mỗi 5 giây, nút "Đã đến" không khóa.

## MB-3. Foreground service khi Online (B1, B18)

| File | Thay đổi |
|---|---|
| `android/app/src/main/AndroidManifest.xml` | `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, `POST_NOTIFICATIONS` (Android 13+). Không thêm `ACCESS_BACKGROUND_LOCATION` |
| `driver_location_source.dart` `watch()` | `AndroidSettings(foregroundNotificationConfig: ForegroundNotificationConfig(notificationTitle: 'ModMove', notificationText: 'ModMove đang chia sẻ vị trí khi bạn Online', enableWakeLock: true))` |
| `api_driver_trip_repository.dart` `setForeground` | Bỏ đoạn hủy timer khi ra nền lúc đang Online; vẫn dừng hẳn khi Offline |
| `README.md` | Ghi B18: chỉ APK; lên Play phải khai báo loại `location` và video |

Test tay trên Pixel_9_Pro:
- Online, về màn hình chính 6 phút: dispatcher vẫn thấy tài xế, có thông báo thường trực.
- Offline: thông báo tắt, không gửi điểm.
- Từ chối quyền thông báo: vẫn gửi được GPS (thông báo chỉ là bắt buộc của Android).

## MB-4. Gửi bù khi mất mạng (B3, B21)

**Cần backend BE-2 lên trước.**

| File | Thay đổi |
|---|---|
| `driver_api.dart` `sendLocation` | Thêm `accuracy`, `isMocked`, `recordedAt` (ISO 8601 UTC) vào body |
| `api_driver_trip_repository.dart` | Hàng đợi FIFO trong bộ nhớ: mọi điểm (cả điểm mới) đi qua hàng đợi, gửi theo thứ tự cũ trước. Gửi lỗi mạng thì giữ lại; bỏ điểm cũ hơn 10 phút; tối đa 120 điểm. 400/422 thì bỏ điểm đó, không thử lại |

Test: mạng lỗi 3 vòng rồi có lại → gửi 4 điểm đúng thứ tự, có `recordedAt`; điểm cũ hơn 10 phút bị bỏ; 400 không thử lại.

Hàng đợi chỉ nằm trong bộ nhớ: tắt app là mất. Đủ cho demo.

## MB-5. Lọc điểm và bỏ giả lập (B2, B4, B19, B20)

| File | Thay đổi |
|---|---|
| `driver_location_source.dart` | Trả cả `accuracy` và `isMocked` của `Position`. Bỏ nhánh `AppConfig.driverFakeLocation` |
| `lib/core/config/app_config.dart` | Bỏ `DRIVER_FAKE_LAT/LNG` và `driverFakeLocation`. Giữ `USE_MOCK_DRIVER` (mock không gọi backend) |
| `api_driver_trip_repository.dart` | `supported` trả tập rỗng; bỏ các nhánh `TripSimulation.nearPickup/advanceRoute/nearDropoff/gpsLost/gpsRestored` và `_override`, `_gpsOff` |
| Các màn có `showTripSimulation` (dashboard, job details, pickup navigation, cargo inspection, pickup approval, delivery) | Ẩn nút khi `supported` rỗng; mock repository vẫn thấy menu |
| Khi backend trả 422 `MOCK_LOCATION` | Tắt Online, hiện "Tắt ứng dụng giả lập vị trí để nhận đơn" |
| `README.md`, `docs/driver-pickup-mock.md` | Bỏ hướng dẫn `DRIVER_FAKE_*`; ghi cách test: Extended Controls → Location (đặt điểm hoặc Routes → Play route) |

**Kiểm tra trước khi làm:** trên Pixel_9_Pro đặt vị trí bằng Extended Controls, in `Position.isMocked`. Nếu là `true` thì dừng lại và báo nhóm, vì B20 sẽ chặn luôn máy ảo.

Test: `isMocked` được gửi đúng; 422 `MOCK_LOCATION` tắt Online và hiện thông báo; repository thật không còn mô phỏng GPS; `test/driver_arrival_radius_test.dart` vẫn pass (nếu test dựa vào `_override` thì đổi sang nguồn vị trí giả trong test, không đổi điều nó kiểm tra).

## MB-6. Khách nhận vị trí tài xế realtime (A1)

| File | Thay đổi |
|---|---|
| `lib/core/notifications/realtime_source.dart` | Thêm `Stream<Map<String, Object?>> locations` (sự kiện `LocationUpdated`), `joinOrder(int)`, `leaveOrder(int)`; nhớ các đơn đã join và gọi lại `JoinOrder` trong `onreconnected` (B12). `NoRealtimeSource` cài rỗng |
| `lib/features/customer/presentation/order_detail/order_detail_cubit.dart` | Nghe `locations` lọc theo `orderId`: cập nhật vị trí và giờ của tài xế trong `OrderTracking`, `driverStale = false`; giữ ETA cũ |
| `order_detail_screen.dart` | Mở màn: `joinOrder`; đóng: `leaveOrder`. Giữ timer 60 giây gọi `loadTracking` để làm mới ETA (mỗi lần tốn 1 lượt MapVina, giới hạn 10 lần/phút) |

Test (`test/customer/`): nguồn realtime giả phát `LocationUpdated` đúng đơn → vị trí đổi, ETA giữ nguyên; khác đơn → bỏ qua; kết nối lại → gọi lại `JoinOrder`.

## Xong mỗi task

`flutter analyze`, `flutter test`. Thử tay trên Pixel_9_Pro với backend local. Commit `[TICKET-ID] feat|fix|test: ...` trên `feature/hieu`.
