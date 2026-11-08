# Plan: chuyển `IMapService` từ Google Maps sang MapVina

Trạng thái: **đã duyệt và làm xong (2026-11-08)**: Q1 `driving`, Q2 để PR sau, Q3 xóa Google adapter. Nhánh: `feature/hieu`. Commit: `[FE-xx] feat: ...`.

## 1. Mục tiêu

`IMapService` (geocode địa chỉ và tìm đường) gọi MapVina thay cho Google Maps. Chữ ký port giữ nguyên, nên handler, fake và test của handler không phải sửa.

## 2. Kết quả test API thật (2026-11-08, key cá nhân)

| Gọi | Kết quả |
|---|---|
| Search v2 `/api/v2/place/textsearch/json`, "1 Lê Lợi, Q1", "123 Xuân Thủy, Cầu Giấy" | `OK`, đúng số nhà, `types=street_address`, 0,3–0,9 giây |
| Search v2 "Số 8 Tôn Thất Thuyết" | Ra "10-8 Tôn Thất Thuyết", gần đúng |
| Search v2 "asdfgh" | `ZERO_RESULTS` |
| Search v2 "zzqqxx không tồn tại 99999" | **`OK` cùng một điểm lạ ở Bình Phước** |
| Directions v2 `/route/v2/directions/json`, Q1 → Tân Sơn Nhất | `driving` 7.141 m / 1.019 s; `truck` 7.141 m / 1.171 s |
| Directions v2 Hà Nội → TP HCM, `truck` | 1.621 km / 23,8 giờ |
| Directions v2 tới một điểm ngoài biển | **`OK`**, route bám vào đường gần nhất, không báo lỗi |
| Key sai | HTTP 401 |

Response Search v2 và Directions v2 cùng dạng Google: `status`, `results[].geometry.location.{lat,lng}`, `routes[].legs[].distance.value` (mét) và `duration.value` (giây). Key truyền qua `?key=`.

Hạn mức gói Free: 200 lượt geocode/ngày và 200 lượt tìm đường/ngày. Một lần báo giá tốn 2 lượt geocode và 1 lượt tìm đường, nên dùng được khoảng 100 lần báo giá mỗi ngày. Mỗi lần mở màn hình tracking tốn thêm 1 lượt tìm đường.

## 3. Các bước (test trước, code sau)

### Bước 1: adapter MapVina (phần của Hiếu)

Tạo `src/ModMove.Infrastructure/ExternalServices/MapVina/`:
- `MapVinaOptions.cs`: `ApiKey`, đọc từ section `MapVina`.
- `MapVinaMapService.cs : IMapService`
  - `GeocodeAsync`: gọi `GET https://maps.mapvina.com/api/v2/place/textsearch/json?language=vi&query=...&key=...`.
    - `ZERO_RESULTS` hoặc `results` rỗng → `DomainException("Address not found.", "ADDRESS_NOT_FOUND")`.
    - `status` khác `OK` → `InvalidOperationException` (500). Message không chứa URL, vì URL có key.
    - Trả về `results[0].geometry.location`.
  - `GetRouteAsync`: gọi `GET https://maps.mapvina.com/route/v2/directions/json?origin=lat,lng&destination=lat,lng&mode=driving&key=...`. Tọa độ format bằng `InvariantCulture`.
    - `routes` rỗng hoặc `status` là `ZERO_RESULTS`/`NOT_FOUND` → `DomainException("No road route between the two points.", "ROUTE_NOT_FOUND")`.
    - Còn lại cộng `legs[].distance.value` và `legs[].duration.value`, rồi làm tròn km và phút như `GoogleMapService` hiện tại.

Test `tests/ModMove.IntegrationTests/MapVinaMapServiceTests.cs` dùng JSON mẫu, không gọi mạng. Viết test trước và xác nhận test fail:
1. Geocode đọc tọa độ của kết quả đầu tiên.
2. `ZERO_RESULTS` → `ADDRESS_NOT_FOUND`.
3. Status lạ (`REQUEST_DENIED`) → `InvalidOperationException`, message không chứa key.
4. Route đọc mét và giây, đổi ra km (2 chữ số) và phút (làm tròn lên).
5. Cùng một điểm (`distance.value = 0`) → 0 km, 0 phút.
6. `routes` rỗng → `ROUTE_NOT_FOUND`.
7. Request có `language=vi`, `mode=driving`, tọa độ dùng dấu chấm thập phân.
8. HTTP 401 → lỗi 500, không lộ key.

### Bước 2: đăng ký DI (file chung, chỉ sửa khối map)

`src/ModMove.Infrastructure/DependencyInjection.cs`, khối `// Google Maps`:
- `MapVina:ApiKey` có giá trị → `AddHttpClient<IMapService, MapVinaMapService>().RemoveAllLoggers()`. Phải giữ `RemoveAllLoggers` vì key nằm trong URL.
- Không có key → `NotConfiguredMapService` như hiện tại. Sửa message thành "set MapVina:ApiKey".

Sửa `tests/ModMove.IntegrationTests/InfrastructureWiringTests.cs`: test `Maps_key_never_reaches_the_logs` đổi sang `MapVina:ApiKey`, stub trả JSON dạng Search v2.

### Bước 3: dọn Google

- Xóa `ExternalServices/GoogleMaps/GoogleMapService.cs`, `GoogleMapsOptions.cs` và `tests/.../GoogleMapServiceTests.cs`. Code vẫn còn trong lịch sử git.
- Chuyển `NotConfiguredMapService.cs` sang thư mục `MapVina/`.
- `appsettings.Development.example.json`: đổi section `GoogleMaps` thành `"MapVina": { "ApiKey": "" }`.

### Bước 4: tài liệu (cùng PR)

- `docs/api/README.md`: sửa chữ "Google Maps" trong mô tả tracking (dòng 60) và mã `ADDRESS_NOT_FOUND` (dòng 162).
- `docs/ARCHITECTURE.md` dòng 54: thư mục `ExternalServices/` có `MapVina` thay `GoogleMaps`.
- `docs/TEAM-PLAN.md` dòng 49 và 119; `CLAUDE.md` dòng 37; `.claude/rules/ownership.md` dòng 8: đổi tên dịch vụ.
- XML comment ở `Ports.cs:63`, `OrderPricing.cs:19`, `GetOrderTrackingQuery.cs:21`, `GetOrderTrackingQueryHandler.cs:21`: đổi "Google Maps" thành "MapVina" (chỉ đổi comment).
- `docs/DECISIONS.md`: trên `origin/develop` (đã fetch lúc 2026-11-08), dòng 4.4 và C3/3.4 **vẫn ghi Google Maps**. Nếu bạn sửa ở nhánh khác thì cần merge trước. Nếu chưa sửa thì làm luôn trong PR này.

### Bước 5: kiểm tra

`dotnet build`, `dotnet test` (gồm ArchitectureTests) và `dotnet format --verify-no-changes`. Chạy app gọi thử báo giá với key thật, sau đó chạy agent `reviewer`.

## 4. Ảnh hưởng đến phần của thành viên khác

Không file nào của Hoàng hay Huy gọi `IMapService`. Họ chỉ **đọc dữ liệu** mà geocode và route tạo ra, lưu trong `ShippingOrder` (tọa độ điểm lấy/giao, `DistanceKm`). Vì vậy không phải sửa code của họ, nhưng dữ liệu thay đổi:

| Người | Chỗ bị ảnh hưởng | Ảnh hưởng |
|---|---|---|
| Hoàng | `Matching/GetNearbyOrders`, `Matching/AcceptOrder` | Lọc đơn quanh tài xế theo tọa độ điểm lấy. Nếu geocode trả điểm lạ (rủi ro R1) thì đơn hiện sai chỗ hoặc không hiện. |
| Hoàng | `Pricing` / `FareCalculator` (B1), giá chốt 4.4 | Cước tính theo `DistanceKm` của MapVina. Số km có thể lệch vài phần trăm so với Google, nên giá cũng lệch theo. Nếu chọn `mode=truck` (câu hỏi Q1) thì km có thể dài hơn. |
| Hoàng | `Reports/ExportRevenueReport` | Đọc `DistanceKm` để báo cáo. Đơn cũ (Google) và đơn mới (MapVina) không cùng nguồn. |
| Huy | `Incidents/GetReplacementDrivers`, `ReassignIncidentDriver`, `Monitoring/GetDispatcherOrderById` | Đọc tọa độ đơn để tìm tài xế thay và hiển thị cho Dispatcher. Ảnh hưởng giống Matching. |
| Cả nhóm | `appsettings.Development.json` trên máy mỗi người | Ai không có `MapVina:ApiKey` thì báo giá và tạo đơn lỗi 500 "not configured" (app vẫn chạy). Hiếu gửi key qua kênh riêng, nhắc mọi người thêm vào. Lưu ý quota 200 lượt/ngày bị chia chung. |
| Cả nhóm (file chung) | `Infrastructure/DependencyInjection.cs`, comment trong `Application/Common/Interfaces/Ports.cs` | Theo `ownership.md` cần **PR riêng và báo nhóm**. Chỉ sửa khối map, không đụng khối khác. |
| Frontend | Không đổi API | Request và response của báo giá, tạo đơn, tracking giữ nguyên. |

`Fakes.cs`, entity, enum và migration đều **không đổi**.

## 5. Rủi ro

- **R1. Địa chỉ rác vẫn có tọa độ.** MapVina trả `OK` cho chuỗi không tồn tại, nên `ADDRESS_NOT_FOUND` chỉ xảy ra với chuỗi quá vô nghĩa. Backend không lọc chắc chắn được, vì `types` của kết quả rác trông như POI thật. Cách giảm: xem câu hỏi Q2.
- **R2. Không có đường bộ vẫn trả `OK`.** Route bám vào đường gần nhất, nên `ROUTE_NOT_FOUND` gần như không xảy ra. Với chuyển nhà trong đất liền thì chấp nhận được.
- **R3. Quota 200 lượt/ngày.** Chạy test tải hoặc demo đông người có thể hết quota. Khi hết quota, MapVina trả lỗi và báo giá trả 500. Cần nâng gói hoặc tránh chạy test tải bằng key thật.

## 6. Câu hỏi cần bạn chốt trước khi code

- **Q1.** `mode=driving` (giống Google hiện tại) hay `mode=truck` (đúng với xe chuyển nhà, nhưng km và giá có thể đổi)? Đề xuất: `driving` trong PR này. `truck` là đổi luật giá, cần nhóm chốt và ghi vào DECISIONS.
- **Q2.** Có thêm `pickup` / `dropoff` (địa chỉ chuẩn hóa và tọa độ) vào response báo giá để app hiện cho khách xác nhận không? Cách này xử lý R1 nhưng đổi API, cần báo frontend. Đề xuất: PR sau.
- **Q3.** Xóa hẳn Google adapter (bước 3) hay giữ làm dự phòng khi không có key MapVina? Đề xuất: xóa, vì DECISIONS đã đổi và code còn trong lịch sử git.
