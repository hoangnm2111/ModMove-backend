# Plan: cache kết quả MapVina (geocode và tìm đường)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Trạng thái: **đã duyệt và làm xong (2026-11-09)**. Nhánh: `feature/hieu`. Commit: `feat: ... (3.4)`.

**Goal:** Gọi MapVina ít hơn: cùng một địa chỉ hoặc cùng một cặp điểm thì dùng lại kết quả trong RAM, không tốn thêm lượt của gói Free.

**Architecture:** Thêm decorator `CachedMapService : IMapService` ở Infrastructure, bọc ngoài `MapVinaMapService`, lưu kết quả bằng `IMemoryCache`. Port `IMapService`, handler, `Fakes.cs` và Application không đổi. Chỉ bọc adapter thật, không bọc `NotConfiguredMapService`.

**Tech Stack:** .NET 8, `Microsoft.Extensions.Caching.Memory` 8.0.1 (đã có trong `ModMove.Infrastructure.csproj`), xUnit.

**Spec:** DECISIONS 3.4 (MapVina gói Free: 200 lượt geocode và 200 lượt tìm đường mỗi ngày, dùng chung key) và 4.4 (giá chốt theo km MapVina lúc đặt đơn, so với `expectedTotalFee`).

## 1. Vì sao cần

- Mỗi lần báo giá hoặc tạo đơn tốn 2 geocode và 1 route (`OrderPricing.cs:44-46`). Khách thường báo giá rồi đặt đơn với cùng hai địa chỉ, nên hiện tốn 6 lượt; có cache còn 3.
- Báo giá và tạo đơn ra cùng số km, nên `expectedTotalFee` khớp, ít dính `FARE_CHANGED` (4.4).
- Khi dev và test thủ công, nhóm lặp lại vài địa chỉ quen, gần như mọi lần đều trúng cache.

## 2. Quyết định đã chốt trong plan

| Mục | Giá trị | Lý do |
|---|---|---|
| Khoá geocode | `mapvina:geo:` + địa chỉ đã chuẩn hoá | |
| Chuẩn hoá địa chỉ | `Trim`, gộp mọi khoảng trắng liên tiếp thành 1 dấu cách, `ToLowerInvariant()`. **Giữ dấu tiếng Việt** | "Lê Lợi" và "Le Loi" có thể là hai chỗ khác nhau |
| TTL geocode | 7 ngày, tuyệt đối (`AbsoluteExpirationRelativeToNow`) | Địa chỉ gần như không đổi toạ độ |
| Khoá route | `mapvina:route:{lat1},{lng1}:{lat2},{lng2}`, mỗi số làm tròn 5 chữ số (`MidpointRounding.AwayFromZero`), format `InvariantCulture` | 5 chữ số khoảng 1,1 m; tránh dấu phẩy thập phân theo máy |
| TTL route | 1 ngày | Directions v2 `mode=driving` không tính kẹt xe, kết quả ổn định |
| Lỗi | Không cache bất kỳ exception nào (kể cả `ADDRESS_NOT_FOUND`, `ROUTE_NOT_FOUND`, huỷ request) | Key hỏng hay MapVina sập không được bị "nhớ" |
| Giới hạn bộ nhớ | Không đặt `SizeLimit` | Mỗi lần miss là một lượt MapVina, nên số mục mỗi ngày ≤ quota (~400 mục, vài chục KB) |
| Hai request cùng miss một lúc | Chấp nhận gọi MapVina 2 lần | Không cần khoá, tránh phức tạp |
| Lifetime DI | `Transient`, giống typed client hiện tại | Không giữ một `HttpClient` mãi trong singleton |

## Global Constraints

- Không thêm NuGet package. Dùng `Microsoft.Extensions.Caching.Memory` 8.0.1 đã có.
- Không sửa `Application/Common/Interfaces/Ports.cs`, `Fakes.cs`, handler hay `tests/ModMove.ArchitectureTests`.
- Giữ `.RemoveAllLoggers()` trên HTTP client MapVina: URL chứa key.
- Giữ tên HTTP client là `nameof(IMapService)`, vì test `Maps_key_never_reaches_the_logs` gắn stub theo tên này.
- `DependencyInjection.cs` là file chung: chỉ sửa khối `// MapVina`.
- Xong khi `dotnet build ModMove.slnx` không có warning mới, `dotnet test ModMove.slnx` pass, `dotnet format ModMove.slnx --verify-no-changes` sạch.

## Review Focus

1. Địa chỉ khác nhau chỉ ở dấu ("Lê Lợi" và "Le Loi") phải là hai mục cache khác nhau. Test ở Task 1.
2. Request bị huỷ giữa chừng không được để lại mục cache. Test ở Task 1.
3. MapVina lỗi (key hết hạn, hết quota) rồi hồi phục: lần gọi sau phải gọi MapVina thật, không trả lỗi cũ. Test ở Task 1.
4. Cache nằm trong một process: restart app là mất, nhiều instance thì mỗi instance một cache. Chấp nhận được vì DECISIONS C7 chỉ chạy local. Không test.
5. Tracking ETA cũng đi qua cache. Tài xế đứng yên thì nhận lại ETA cũ, vẫn đúng vì cùng điểm thì cùng route. Không test riêng.

---

### Task 1: decorator `CachedMapService`

**Files:**
- Create: `src/ModMove.Infrastructure/ExternalServices/MapVina/CachedMapService.cs`
- Test: `tests/ModMove.IntegrationTests/CachedMapServiceTests.cs`

**Interfaces:**
- Consumes: `IMapService`, `GeoPoint(decimal Latitude, decimal Longitude)`, `RouteInfo(decimal DistanceKm, int DurationMinutes)` từ `Ports.cs`; `IMemoryCache`.
- Produces: `public sealed class CachedMapService(IMapService inner, IMemoryCache cache) : IMapService`, với hằng `public static readonly TimeSpan GeocodeTtl = TimeSpan.FromDays(7)` và `RouteTtl = TimeSpan.FromDays(1)`.

Test dùng `new MemoryCache(new MemoryCacheOptions { Clock = clock })` với một `ISystemClock` (`Microsoft.Extensions.Internal`) giả để tua thời gian, và một `IMapService` đếm số lần gọi viết `private sealed` ngay trong file test (không đụng `Fakes.cs`). Fake này trả điểm khác nhau theo địa chỉ, và có cờ để ném exception theo lượt.

- [ ] **Step 1: Viết test fail**

| Test | Khẳng định |
|---|---|
| `Same_address_is_geocoded_once` | Gọi `"1 Lê Lợi, Q1"` hai lần → inner được gọi 1 lần, hai kết quả bằng nhau |
| `Address_differing_only_in_case_and_spaces_hits_the_cache` | `"  1 Lê Lợi,   Q1 "` rồi `"1 lê lợi, q1"` → inner 1 lần |
| `Address_without_diacritics_is_a_different_entry` | `"1 Lê Lợi, Q1"` rồi `"1 Le Loi, Q1"` → inner 2 lần |
| `Failed_geocode_is_not_cached` | Lần 1 inner ném `DomainException` code `ADDRESS_NOT_FOUND` → exception lan ra; lần 2 inner trả điểm → inner 2 lần, trả điểm |
| `Map_outage_is_not_cached` | Lần 1 inner ném `InvalidOperationException`; lần 2 thành công → inner 2 lần |
| `Cancelled_geocode_is_not_cached` | Lần 1 inner ném `OperationCanceledException`; lần 2 → inner được gọi lại |
| `Geocode_expires_after_seven_days` | Gọi, tua `GeocodeTtl + 1s`, gọi lại → inner 2 lần; tua `GeocodeTtl - 1s` → inner 1 lần |
| `Same_route_is_fetched_once` | Cùng `from`/`to` hai lần → inner route 1 lần |
| `Route_points_equal_after_rounding_hit_the_cache` | `(10.776889m, 106.700981m)` và `(10.7768891m, 106.7009809m)` cùng đích → inner 1 lần |
| `Different_route_is_fetched_again` | Đổi đích → inner 2 lần |
| `Route_expires_after_one_day` | Tua `RouteTtl + 1s` → inner 2 lần |

- [ ] **Step 2: Chạy, xác nhận fail vì chưa có `CachedMapService`**

Run: `dotnet test ModMove.slnx --filter "FullyQualifiedName~CachedMapServiceTests"`
Expected: build lỗi `CachedMapService` không tồn tại.

- [ ] **Step 3: Viết `CachedMapService`**

- `GeocodeAsync(string address, CancellationToken ct)`: `TryGetValue` theo khoá; miss thì `await inner.GeocodeAsync(address, ct)` (truyền địa chỉ gốc, không truyền bản chuẩn hoá), rồi `cache.Set(key, point, GeocodeTtl)`. Exception tự lan ra nên không vào cache.
- `GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken ct)`: như trên với khoá route và `RouteTtl`.
- Chuẩn hoá địa chỉ: `string.Join(' ', address.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant()`.
- XML summary: nêu DECISIONS 3.4 (quota) và 4.4 (báo giá và tạo đơn cùng km).

- [ ] **Step 4: Chạy lại, xác nhận pass**

Run: `dotnet test ModMove.slnx --filter "FullyQualifiedName~CachedMapServiceTests"`
Expected: 11 test pass. Nếu `ISystemClock` gây warning obsolete khi build thì đổi test TTL sang so `AbsoluteExpirationRelativeToNow` của mục, không tắt warning.

- [ ] **Step 5: Commit**

```bash
git add src/ModMove.Infrastructure/ExternalServices/MapVina/CachedMapService.cs tests/ModMove.IntegrationTests/CachedMapServiceTests.cs
git commit -m "[FE-xx] feat: add CachedMapService for MapVina results"
```

### Task 2: đăng ký DI và cập nhật tài liệu

**Files:**
- Modify: `src/ModMove.Infrastructure/DependencyInjection.cs:97-108` (khối `// MapVina`)
- Test: `tests/ModMove.IntegrationTests/InfrastructureWiringTests.cs`
- Modify: `docs/plans/2026-11-09-mapvina-cache.md` (trạng thái), `docs/DECISIONS.md` dòng `C3, 3.4` (thêm "kết quả được cache trong RAM: geocode 7 ngày, route 1 ngày")

**Interfaces:**
- Consumes: `CachedMapService(IMapService inner, IMemoryCache cache)` từ Task 1; `MapVinaMapService(HttpClient http, MapVinaOptions options)`.
- Produces: `IMapService` trong DI là `CachedMapService` bọc `MapVinaMapService` khi có `MapVina:ApiKey`; vẫn là `NotConfiguredMapService` khi không có.

- [ ] **Step 1: Viết test fail** trong `InfrastructureWiringTests`

`Maps_results_are_cached_across_requests`: dựng DI như `Maps_key_never_reaches_the_logs` (có `MapVina:ApiKey`, stub cho client `nameof(IMapService)` trả JSON Search v2 hợp lệ và đếm số request). Lấy `IMapService` trong **hai scope khác nhau**, mỗi scope geocode `"Hà Nội"` một lần → stub nhận đúng 1 request; `Assert.IsType<CachedMapService>(...)`.

- [ ] **Step 2: Chạy, xác nhận fail**

Run: `dotnet test ModMove.slnx --filter "FullyQualifiedName~InfrastructureWiringTests"`
Expected: test mới FAIL (stub nhận 2 request, kiểu là `MapVinaMapService`).

- [ ] **Step 3: Sửa khối MapVina trong `DependencyInjection.cs`**

Khi có key:
- `services.AddMemoryCache();`
- `services.AddHttpClient(nameof(IMapService)).RemoveAllLoggers();`
- `services.AddTransient<IMapService>(sp => new CachedMapService(new MapVinaMapService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IMapService)), options), sp.GetRequiredService<IMemoryCache>()));` với `options` là `MapVinaOptions` đã tạo.
- Sửa comment của khối: thêm một câu về cache.

Khi không có key: giữ nguyên `NotConfiguredMapService`.

- [ ] **Step 4: Chạy toàn bộ**

Run: `dotnet build ModMove.slnx`, `dotnet test ModMove.slnx`, `dotnet format ModMove.slnx --verify-no-changes`
Expected: không warning mới; mọi test pass, gồm `Maps_key_never_reaches_the_logs`, `App_starts_without_maps_or_cloudinary_keys` và `ModMove.ArchitectureTests`.

- [ ] **Step 5: Cập nhật tài liệu** (DECISIONS dòng 3.4, trạng thái plan này thành "đã làm xong").

- [ ] **Step 6: Commit**

```bash
git add src/ModMove.Infrastructure/DependencyInjection.cs tests/ModMove.IntegrationTests/InfrastructureWiringTests.cs docs/DECISIONS.md docs/plans/2026-11-09-mapvina-cache.md
git commit -m "[FE-xx] feat: cache MapVina geocode and route results"
```

`DECISIONS.md` là file chung: báo nhóm khi mở PR.
