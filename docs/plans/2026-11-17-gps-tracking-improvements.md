# Plan: cải thiện GPS tài xế và theo dõi đơn

Ngày: 2026-11-17, chốt 2026-11-18 · Trạng thái: **đã chốt hết, sẵn sàng làm** · Kiểm tra trên `feature/hieu` `f8682f8`, mobile và web bản đang có trên máy.

Plan chi tiết theo repo:
- Backend: [2026-11-18-gps-backend.md](2026-11-18-gps-backend.md) (BE-1…BE-4)
- App mobile: [2026-11-18-gps-mobile.md](2026-11-18-gps-mobile.md) (MB-0…MB-6)
- Web: [2026-11-18-gps-web.md](2026-11-18-gps-web.md) (WB-1…WB-3)

Thứ tự giữa các repo: BE-1 → (BE-2, BE-3, BE-4) song song với (WB-1…3, MB-0…3, MB-6) → MB-4, MB-5 chỉ sau khi BE-2 đã merge, vì app gửi `recordedAt` cho backend cũ sẽ làm điểm cũ ghi đè vị trí mới.

## 1. Hiện trạng

Đã chạy thử trên backend local: tài xế gửi vị trí 204, chưa đăng nhập 401, tài khoản khách 403, vĩ độ 100 bị 400; dispatcher và khách (đơn #12) đọc lại đúng tọa độ vừa gửi.

| Chỗ | Có gì | Thiếu gì |
|---|---|---|
| App tài xế (`api_driver_trip_repository.dart`) | Mỗi 5 giây `getCurrentPosition` (độ chính xác cao) rồi `POST api/driver/location`; `DRIVER_FAKE_LAT/LNG` và menu "Mô phỏng" (`TripSimulation`: gần điểm lấy, đi tiếp, mất GPS…) | B12 ghi tài xế gửi qua hub `SendLocation`, app lại dùng REST (cùng handler); dừng gửi khi app ra nền; không gửi `accuracy`/`isMocked`; mất mạng là mất điểm; gửi GPS lỗi thì bỏ cả vòng đồng bộ (`_syncOnce`) |
| GB-52 trên app | Nút "Đã đến"/"Hoàn tất giao" cần `GpsFix.freshAt` (GPS trong 30 giây) và cách điểm ≤ 500 m | — |
| Backend `UpdateDriverLocation` (Tracking) | Id từ token, kiểm tra Active + Approved mỗi lần; ghi đè `driver_profiles.last_*`; thêm `driver_location_logs` khi có đơn; đẩy SignalR `LocationUpdated` | Không lọc điểm sai/giả; dùng giờ server, không nhận giờ của máy |
| `driver_location_logs` | decimal(9,6); index `(order_id, recorded_at)`, `(recorded_at)`; `GpsLogCleanupJob` xóa điểm > 30 ngày, mỗi 24 giờ | Ghi mỗi 5 giây; job xóa bằng một lệnh duy nhất |
| Ai đọc `driver_location_logs` | Chỉ `GetOrderTracking` (`VehicleSql`): vị trí xe cũ khi đổi tài xế giữa chuyến (7.2) | — |
| App khách (`order_detail_screen.dart:100`) | Gọi `GET customer/Orders/{id}/tracking` mỗi **60 giây** | Không `JoinOrder`, không nghe `LocationUpdated` |
| Web (`realtime.service.ts`) | `JoinOrder`, nghe `OrderStatusChanged` | Không nghe `LocationUpdated` (cả màn khách lẫn bản đồ Dispatcher) |
| Chú thích `DriverLocationLog` | "GPS point streamed by a driver while Online" | Code chỉ lưu khi có đơn (B12) |

## 2. Việc sẽ làm

Người làm: **Hiếu toàn bộ** (Q8), kể cả phần app tài xế (vai trò của Hoàng) và bản đồ Dispatcher (của Huy); xem 3.2 M3.

| Mã | Việc | Repo, chỗ sửa | Theo quyết định |
|---|---|---|---|
| A1 | Khách và Dispatcher nhận vị trí realtime: `JoinOrder` + nghe `LocationUpdated`, giữ gọi lại 60 giây làm dự phòng | mobile (màn đơn của khách), web (màn khách, bản đồ Dispatcher) | B12 (đã có) |
| A2 | Gửi GPS lỗi không chặn tải chuyến/đơn/ca trong cùng vòng | mobile `api_driver_trip_repository.dart` | — |
| A3 | `getPositionStream` lấy vị trí **theo thời gian (5 giây), không dùng `distanceFilter`**, thay cho `getCurrentPosition` từng lần | mobile `driver_location_source.dart` | GB-52, 6.4 |
| A4 | Sửa chú thích `DriverLocationLog`; sửa B12: app tài xế gửi qua REST, hub `SendLocation` vẫn giữ | backend `Domain/Entities` + `docs/DECISIONS.md` (PR riêng, báo nhóm) | Q7 |
| B1 | Foreground service + thông báo "ModMove đang chia sẻ vị trí", **chỉ khi Online**; Offline thì dừng hẳn. Chỉ build APK | mobile (geolocator `foregroundNotificationConfig`, quyền `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`) | Q1, Q10 |
| B2 | Lọc điểm: app gửi thêm `accuracy`, `isMocked` (tùy chọn để app cũ vẫn chạy). Backend: `accuracy` > 100 m thì bỏ (không lưu, không đẩy), vẫn 204; vị trí giả lập theo M1 | mobile + backend `Features/Tracking/Commands/UpdateDriverLocation` | Q2, Q3 |
| B3 | Gửi bù khi mất mạng: app giữ điểm kèm giờ máy, có mạng gửi một lần. Backend nhận `recordedAt`: không quá 30 giây ở tương lai, không cũ hơn 10 phút; điểm cũ hơn `last_location_at` thì không ghi đè `last_*` | mobile + backend Tracking + `DriverProfile.UpdateLocation` (Domain, PR riêng) | Q4 |
| B4 | Bỏ giả lập vị trí theo M1 | mobile | Q3 |
| C1 | Lưu lịch sử **60 giây một lần** mỗi đơn (bỏ điểm nếu điểm log cuối của đơn chưa đủ 60 giây); `last_*` và SignalR vẫn cập nhật mỗi lần gửi | backend Tracking | Q5 |
| C2 | `GpsLogCleanupJob` xóa theo đợt ~10.000 dòng | backend Tracking + `Infrastructure/BackgroundJobs` | — |

**Bỏ:** C3 (mốc GPS riêng cho ghép đơn), vì 6.4 giữ 5 phút (Q6). Redis, chia partition bảng log: chưa cần.

**Không cần migration:** B2 bỏ điểm xấu chứ không lưu `accuracy`; C1 đọc điểm log cuối qua index `(order_id, recorded_at)` có sẵn.

**Hai chỗ đã sửa so với bản trước:**
- A3 trước ghi `distanceFilter` 10–20 m. Làm vậy thì xe đứng yên (chờ ở điểm lấy hàng, kẹt xe) sẽ không có điểm mới: sau 30 giây nút GB-52 bị khóa, sau 5 phút tài xế bị coi là mất kết nối (6.4). Phải lấy theo thời gian.
- C1 60 giây làm `VehicleSql` (vị trí xe khi đổi tài xế, 7.2) có thể cũ tới 60 giây. Chấp nhận được vì xe gặp sự cố thường đứng yên.

## 3. Quyết định

### 3.1 Đã chốt (2026-11-18)

| # | Quyết định | Ghi vào |
|---|---|---|
| Q1 | Chạy nền bằng foreground service, chỉ khi Online | DECISIONS mới |
| Q2 | Bỏ điểm sai số trên 100 m, vẫn trả 204 | DECISIONS mới |
| Q3 | Dùng GPS thật, không giả lập nữa (chi tiết ở M1) | DECISIONS mới |
| Q4 | `recordedAt` không ở tương lai, không cũ hơn 10 phút; không ghi đè vị trí mới hơn | DECISIONS mới |
| Q5 | Lưu lịch sử GPS 60 giây một lần | Sửa B12 |
| Q6 | 6.4 giữ 5 phút, không có mốc riêng cho ghép đơn | Không đổi |
| Q7 | App tài xế giữ REST, sửa B12 cho đúng | Sửa B12 |
| Q8 | Hiếu làm toàn bộ code GPS và bản đồ | TEAM-PLAN mục 2 |
| Q9 | Làm cả 3 đợt | — |
| Q10 | Chỉ APK cho demo; lên Play thì khai báo foreground service loại `location` và quay video giải thích | DECISIONS mới |

### 3.2 Đã chốt theo đề xuất (nhóm đồng ý 2026-11-18)

M1, M2, M3 theo cột "Đề xuất". **M4 sửa lại:** dùng nhánh `feature/hieu` ở cả mobile và web (một người một nhánh, như backend), không tạo `feature/hieu-gps`.

| # | Câu hỏi | Đề xuất | Chặn việc |
|---|---|---|---|
| M1 | Q3 "GPS thật, không giả lập" gồm những gì? (a) Bỏ `DRIVER_FAKE_LAT/LNG`; (b) bỏ menu "Mô phỏng" của tài xế; (c) bỏ `USE_MOCK_DRIVER`; (d) backend từ chối `isMocked` | **(a) + (b) + (d) ở mọi môi trường: 422 `MOCK_LOCATION`, app báo tắt app giả lập. Giữ (c)** vì không gọi backend. Test trên máy ảo bằng Extended Controls → Location (cần thử trước là máy ảo không bị đánh dấu `isMocked`) | B2, B4 |
| M2 | Có chặn điểm nhảy xa vô lý (tốc độ ngầm > 150 km/h) không? Q2 chỉ nói sai số | **Không**, lọc sai số 100 m là đủ cho demo | B2 |
| M3 | Hoàng và Huy đồng ý để Hiếu sửa app tài xế và bản đồ Dispatcher chưa? `ownership.md` ghi phần của họ "không sửa (hỏi trước)"; Q2–Q5 là luật mới, `CLAUDE.md` yêu cầu nhóm chốt | **Báo nhóm một tin, có đồng ý thì sửa TEAM-PLAN mục 2 và thêm luật vào DECISIONS** trong PR A4 | Tất cả |
| M4 | Làm trên nhánh nào ở mobile và web? Mobile đang ở `spike/mapvina-gl` với 24 file chưa commit, web ở `feat/web-status-toast` | **Tạo `feature/hieu-gps` ở mỗi repo từ `develop`**, không đụng các file đang dở | A1, A2, A3, B1–B4 |

## 4. Thứ tự làm và kiểm tra

Bản dưới là tóm tắt ban đầu; khi khác với 3 plan theo repo thì làm theo 3 plan đó.

**Đợt 1: A4, A1, A2, A3, C2** (cần M3, M4)
- A4: PR riêng vào `develop`: sửa chú thích, B12, thêm luật Q1–Q5, Q10 vào DECISIONS, sửa TEAM-PLAN.
- A1: theo dõi đơn đang chạy trên app khách và web, tài xế chạy route trên máy ảo (Extended Controls → Location → Routes); vị trí khách và Dispatcher thấy đổi trong ≤ 10 giây. Ngắt SignalR thì vẫn cập nhật mỗi 60 giây.
- A2: test repository mobile: `sendLocation` ném lỗi → vòng đó vẫn tải chuyến và danh sách đơn.
- A3: xe đứng yên 6 phút vẫn gửi điểm mỗi 5 giây, nút GB-52 không bị khóa, tài xế không mất kết nối.
- C2: test handler xóa đủ số dòng khi nhiều hơn một đợt.

**Đợt 2: B1, B2, B3, B4** (cần M1, M2)
- Backend: viết test trước theo `CLAUDE.md`: `accuracy` > 100 m → 204, không ghi log, không đẩy SignalR, `last_*` giữ nguyên; `isMocked` → 422 `MOCK_LOCATION` (thêm vào bảng mã lỗi `docs/api/README.md`); `recordedAt` > 30 giây tương lai hoặc cũ hơn 10 phút → 400; điểm cũ hơn `last_location_at` không ghi đè `last_*`; request không có trường mới vẫn chạy như cũ.
- B1: bật Online, về màn hình chính 6 phút: dispatcher vẫn thấy tài xế. Offline: thông báo tắt, không gửi điểm.
- B4: build không còn `DRIVER_FAKE_*` và menu "Mô phỏng"; README mobile ghi cách test bằng Extended Controls.
- Cập nhật `docs/api` cho `POST api/driver/location` và `SendLocation`.

**Đợt 3: C1** (không cần chốt thêm)
- Test: 2 điểm cách 30 giây cùng đơn → 1 dòng log; cách 60 giây → 2 dòng; `last_*` cập nhật cả 2 lần.

Xong mỗi đợt: `dotnet build`, `dotnet test` (kể cả ArchitectureTests), `dotnet format --verify-no-changes`, `flutter analyze` + `flutter test`, `npm test` ở web, chạy agent `reviewer`.
