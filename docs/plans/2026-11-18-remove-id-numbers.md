# Plan: bỏ nhập số CCCD và số GPLX khi tài xế nộp hồ sơ

Ngày: 2026-11-18 · Trạng thái: **đã chốt, sẵn sàng làm** · Kiểm tra trên backend `feature/hieu` `23aa8f4`, mobile `feat/tri-customer`, web `feature/hieu`.

## 0. Quyết định (nhóm đã chốt 2026-11-18)

| # | Quyết định |
|---|---|
| R1 | Tài xế nộp hồ sơ chỉ gồm **7 ảnh** (GPLX trước/sau, đăng ký xe, đăng kiểm, ảnh xe, CCCD trước/sau) + loại xe, biển số, tải trọng. **Bỏ số CCCD và số GPLX.** Admin đọc số trên ảnh |
| R2 | Giữ biển số và tải trọng: ghép đơn dùng tải trọng (`VEHICLE_TOO_SMALL`), biển số vẫn chống trùng |
| R3 | **Xóa cột** `citizen_id_number`, `driver_license_number` (và index unique) ở `driver_profiles`, `driver_applications` bằng migration mới. Số đã lưu (hiện chỉ là dữ liệu demo) mất luôn |
| R4 | Hiếu làm cả backend (`DriverOnboarding`, `Drivers` của Hoàng), app tài xế và web; báo nhóm, ghi ngoại lệ vào TEAM-PLAN |

**Thứ tự:** app và web trước (thôi gửi, thôi hiển thị), backend sau. Backend cũ vẫn nhận request không có 2 trường này (đã là tùy chọn từ D2 2026-11-17), còn backend mới bỏ qua 2 trường nếu app cũ còn gửi (ASP.NET bỏ qua trường lạ). Migration xóa cột là bước không hoàn tác được nên để cuối.

## 1. Backend (ModMove-backend, nhánh `feature/hieu`)

### BE-ID-1. Tài liệu (PR riêng, làm đầu tiên)

| File | Sửa |
|---|---|
| `docs/DECISIONS.md` D2 | "Hồ sơ tài xế gửi loại xe, biển số, tải trọng **và đúng 7 ảnh** …; **không nhập số CCCD/GPLX**, Admin đọc trên ảnh (2026-11-18)" |
| `docs/TEAM-PLAN.md` mục 2 | Ngoại lệ: Hiếu sửa phần số CCCD/GPLX trong `Features/DriverOnboarding`, `Features/Drivers`, app tài xế, web (nhóm đồng ý 2026-11-18) |
| `docs/api/README.md` | Dòng `POST api/driver/onboarding`, `POST .../resubmit`, `GET .../status`, `GET api/driver/profile`, `GET api/admin/driver-applications/{id}`: bỏ `citizenIdNumber`, `driverLicenseNumber`, `personalInfo` |

### BE-ID-2. Domain và dữ liệu (file dùng chung)

| File | Sửa |
|---|---|
| `Domain/Entities/DriverProfile.cs` | Bỏ `CitizenIdNumber`, `DriverLicenseNumber`. `Create(long userId, string licenseFrontUrl, string licenseBackUrl)`, `UpdateCredentials(string licenseFrontUrl, string licenseBackUrl)` (có thể đổi tên `UpdateLicensePhotos`) |
| `Domain/Entities/DriverApplication.cs` | Bỏ 2 thuộc tính và 2 tham số của `Create` |
| `Domain/Constants/DriverApplicationItemKeys.cs` | Bỏ `PersonalInfo` (không còn gì để Admin từ chối) |
| `Infrastructure/Persistence/Configurations/DriverProfileConfiguration.cs` | Bỏ 2 `Property(...)` và 2 `HasIndex(...).IsUnique()` |
| `Infrastructure/Persistence/Configurations/DriverApplicationConfiguration.cs` | Bỏ 2 `Property(...)` |
| Migration mới `DropDriverIdNumbers` (dùng skill `add-migration`, `git pull develop` ngay trước) | `DropIndex` 2 index unique, `DropColumn` 4 cột. Đọc lại file sinh ra: chỉ có các lệnh này, `Down` tạo lại cột nullable |
| `Infrastructure/Persistence/Seed/DemoDataSeeder.cs`, `DemoOperationsSeeder.cs` | Bỏ số CCCD/GPLX khi gọi `Create` |

### BE-ID-3. Use case

| File | Sửa |
|---|---|
| `DriverOnboarding/Commands/SubmitDriverApplication/*` | Command bỏ `CitizenIdNumber`, `DriverLicenseNumber`; validator bỏ 2 rule; handler gọi `Create`/`UpdateCredentials` mới |
| `DriverOnboarding/Commands/ResubmitDriverApplication/*` | Bỏ `ResubmitPersonalInfo`, tham số `PersonalInfo`, nhánh `canUpdatePersonal`, rule trong validator và điều kiện "có gì để nộp lại" |
| `DriverOnboarding/DriverApplicationSubmissionHelper.cs` | `DuplicateSql` bỏ 2 `SELECT` CCCD/GPLX, chỉ còn biển số. `NormalizeCredentials` → `NormalizePlate(string)`. `EnsureNoDuplicatesAsync` bỏ 2 tham số |
| `DriverOnboarding/RejectedApplicationItemDto.cs` | Bỏ nhánh trả `PersonalInfo` (dòng ~109) |
| `DriverOnboarding/Commands/RejectDriverApplication/*` | Key `PersonalInfo` không còn hợp lệ (đã tự bỏ khi xóa khỏi `DriverApplicationItemKeys`) |
| `DriverOnboarding/Queries/GetApplicationDetails/*`, `GetMyApplicationStatus/*` | Bỏ 2 cột trong SQL, 2 trường trong Row và Response |
| `Drivers/Queries/GetDriverProfile/*` | Như trên |

### BE-ID-4. Test

Viết hoặc sửa test trước, thấy fail rồi mới code:
- `SubmitDriverApplicationValidatorTests`, `DriverOnboardingSubmissionTests`: nộp hồ sơ không có số CCCD/GPLX → thành công; body cũ còn `citizenIdNumber` vẫn thành công (bị bỏ qua); trùng biển số vẫn bị chặn.
- `DriverApplicationApprovalTests`: từ chối với key `PersonalInfo` → 400 `VALIDATION`; chi tiết hồ sơ không còn 2 trường.
- Khoảng 40 chỗ gọi `DriverProfile.Create(id, "001", "B2-001", front, back)` / `DriverApplication.Create(...)` trong `tests/` (AcceptOrderTests, DispatcherSetup, DriverTripsTests, UserAndCouponTests…): bỏ 2 đối số, sửa máy móc, không đổi điều test kiểm tra.

Xong: `dotnet format`, `dotnet build` (0 warning), `dotnet test` (cả ArchitectureTests), chạy app trên DB local để áp migration, gọi `POST api/driver/onboarding` và `GET api/admin/driver-applications/{id}` bằng tài khoản demo, agent `reviewer`.

## 2. App mobile (ModMove-mobile, nhánh `feature/hieu` cắt từ `develop`)

Repo đang ở `feat/tri-customer` (không có file chưa commit). Không làm trên nhánh đó.

| File | Sửa |
|---|---|
| `lib/features/onboarding/presentation/screens/verification_screen.dart` | Bỏ 2 ô `citizenId` ("Số CCCD") và `license` ("Số GPLX") ở bước 1 (dòng ~315–350) và validator đi kèm. Giữ ô ảnh CCCD trước/sau |
| `lib/features/onboarding/domain/usecases/validate_driver_application.dart` | Bỏ kiểm tra CCCD 9–12 số và GPLX 8–20 ký tự (dòng 30–33) |
| `lib/features/driver_trip/data/driver_api.dart` | `submitApplication`: bỏ `required String citizenIdNumber, driverLicenseNumber` và 2 khóa trong body |
| `lib/features/onboarding/data/repositories/backend_driver_repository.dart` | Bỏ `'citizenId'`, `'license'` trong `fields` (dòng 27–28, 131–132, 145–146) và 2 đối số khi gọi API (dòng 198–199) |
| `lib/features/onboarding/presentation/screens/driver_profile_screen.dart` | Bỏ 2 dòng "Số CCCD", "Số GPLX" (dòng ~173–178) |
| `lib/features/onboarding/data/repositories/demo_driver_repository.dart`, `data/services/mock_driver_api.dart` | Bỏ 2 trường mẫu và nhánh lỗi giả `'999999999999'` (mock_driver_api ~275) |
| `lib/features/onboarding/domain/rejection_mapper.dart` | Sửa chú thích dòng 10 (không còn "số CCCD") |
| Test: `driver_backend_test`, `driver_mock_api_test`, `driver_mock_flow_test`, `rejection_mapper_test`, `verification_screen_test` | Bỏ phần nhập số; thêm: nộp hồ sơ chỉ ảnh + xe → body không có `citizenIdNumber`, `driverLicenseNumber` |

Xong: `flutter analyze`, `flutter test`; thử tay trên Pixel_9_Pro: đăng ký tài xế mới, nộp 7 ảnh, màn hồ sơ không còn ô số.

## 3. Web (ModMove-web, nhánh `feature/hieu`)

| File | Sửa |
|---|---|
| `src/pages/driver/DriverProfilePage.tsx` | Bỏ state `citizenId`, `license` (dòng 73–74), kiểm tra (90–91), 2 trường trong body (100–101), ô "Số CCCD"/"Số GPLX" (~179) |
| `src/pages/admin/DriverApprovalPage.tsx` | Bỏ 2 dòng `KV` "Số CCCD", "Số GPLX" (117–118); `CHECK_ITEMS` bỏ 'Số CCCD', 'Số giấy phép lái xe' (dòng 18) |
| `src/api/types.ts` | Bỏ `citizenIdNumber`, `driverLicenseNumber` ở 3 interface (dòng ~464, ~471, ~896); sửa chú thích "Đúng 5 phần tử" thành 7 ảnh |

Xong: `npm run build` (có `tsc`, bắt chỗ còn dùng trường đã bỏ), `npm test`; thử tay: tài xế nộp hồ sơ trên web, Admin mở chi tiết và duyệt.

## 4. Kiểm tra cuối (sau khi cả 3 phần merge)

- Tài xế mới trên app: đăng ký, nộp 7 ảnh, Admin duyệt trên web, tài xế bật Online nhận đơn được.
- Hồ sơ bị từ chối: Admin chọn lý do (không còn mục số CCCD/GPLX), tài xế nộp lại.
- Database local: `driver_profiles`, `driver_applications` không còn 2 cột.
