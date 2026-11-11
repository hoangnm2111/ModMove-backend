-- =====================================================================
-- ModMove - file cơ sở dữ liệu đầy đủ (MySQL 8, utf8mb4_unicode_ci)
--
-- Gồm: tạo database `modmove`, 22 bảng nghiệp vụ + `__EFMigrationsHistory`,
-- và dữ liệu thiết lập/mẫu cho mọi vai trò (Admin, Dispatcher, Customer, Driver).
--
-- Nguồn: schema sinh từ toàn bộ migration EF Core tới
-- 20261105153536_UserAndOtpConcurrencyTokens; dữ liệu do chính seeder của app tạo
-- (ReferenceDataSeeder, DemoDataSeeder, DemoOperationsSeeder), cộng thêm:
--   - 5 tài khoản ở các trạng thái khác (xem database/modmove-seed.sql)
--   - payment_transactions cho các đơn PayOS đã thanh toán và lần nạp cọc
--   - 2 support_requests, 5 điểm GPS của đơn MMDEMO001
-- otp_verifications và refresh_tokens để trống: dữ liệu phiên, sinh khi chạy.
--
-- Cách chạy (trên MySQL chưa có database `modmove`):
--   mysql --default-character-set=utf8mb4 -u <user> -p < database/modmove-full.sql
-- File KHÔNG xóa bảng cũ: database `modmove` đã có bảng thì script dừng với lỗi
-- "Table already exists" thay vì ghi đè.
--
-- Vì bảng __EFMigrationsHistory đã đủ, app nối vào DB này sẽ không chạy lại migration.
-- Mọi tài khoản dùng mật khẩu demo của nhóm (docs/TEAM-PLAN.md), lưu dạng BCrypt cost 12.
-- ĐỔI MẬT KHẨU trước khi dùng trên môi trường thật. Thời gian lưu UTC.
-- =====================================================================


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

CREATE DATABASE /*!32312 IF NOT EXISTS*/ `modmove` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;

USE `modmove`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `__EFMigrationsHistory` (
  `MigrationId` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ProductVersion` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261020063927_InitialCreate','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261020175620_UserLoginConcurrencyToken','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261021150143_AlignSchemaWithSds','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261021165053_OtpFailedAttempts','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261022180906_TeamDecisions','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261102071610_CustomerReasonCodesAndGpsRetention','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261102093127_RefundRowVersionAndDropAvatar','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261102150604_RenameOrderStatusPendingToPlaceOrder','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261102172137_RenameOrderStatusPlaceOrderBackToPending','8.0.10');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20261105153536_UserAndOtpConcurrencyTokens','8.0.10');
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cargo_details` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `order_id` bigint NOT NULL,
  `cargo_type` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `quantity` int NOT NULL,
  `weight_kg` decimal(8,2) NOT NULL,
  `length_cm` int NOT NULL,
  `width_cm` int NOT NULL,
  `height_cm` int NOT NULL,
  `photo_urls` json NOT NULL,
  `special_notes` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_cargo_details_order_id` (`order_id`),
  CONSTRAINT `FK_cargo_details_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (1,1,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:29.666475',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (2,2,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:29.781070',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (3,3,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:29.881853',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (4,4,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:29.960855',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (5,5,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.020935',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (6,6,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.068486',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (7,7,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.140775',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (8,8,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.309016',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (9,9,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.436735',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (10,10,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.569146',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (11,11,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.771999',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (12,12,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.840290',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (13,13,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:30.895047',NULL);
INSERT INTO `cargo_details` (`id`, `order_id`, `cargo_type`, `quantity`, `weight_kg`, `length_cm`, `width_cm`, `height_cm`, `photo_urls`, `special_notes`, `created_at`, `updated_at`) VALUES (14,14,'APPLIANCE',3,120.00,100,60,50,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,'2026-11-10 14:58:31.046387',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `coupons` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `discount_code` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `discount_value` decimal(15,2) NOT NULL,
  `minimum_order_value` decimal(15,2) NOT NULL DEFAULT '0.00',
  `valid_from` datetime(6) NOT NULL,
  `expires_at` datetime(6) NOT NULL,
  `usage_limit` int DEFAULT NULL,
  `used_count` int NOT NULL DEFAULT '0',
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_coupons_discount_code` (`discount_code`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `coupons` (`id`, `discount_code`, `discount_value`, `minimum_order_value`, `valid_from`, `expires_at`, `usage_limit`, `used_count`, `is_active`, `created_at`, `updated_at`) VALUES (1,'WELCOME20K',20000.00,100000.00,'2026-11-10 14:58:03.335215','2027-11-10 14:58:03.335215',1000,0,1,'2026-11-10 14:58:28.117135',NULL);
INSERT INTO `coupons` (`id`, `discount_code`, `discount_value`, `minimum_order_value`, `valid_from`, `expires_at`, `usage_limit`, `used_count`, `is_active`, `created_at`, `updated_at`) VALUES (2,'FREESHIP50K',50000.00,300000.00,'2026-11-10 14:58:03.335215','2027-11-10 14:58:03.335215',500,0,1,'2026-11-10 14:58:28.117135',NULL);
INSERT INTO `coupons` (`id`, `discount_code`, `discount_value`, `minimum_order_value`, `valid_from`, `expires_at`, `usage_limit`, `used_count`, `is_active`, `created_at`, `updated_at`) VALUES (3,'MODMOVE100K',100000.00,1000000.00,'2026-11-10 14:58:03.335215','2027-11-10 14:58:03.335215',100,0,1,'2026-11-10 14:58:28.117135',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `driver_applications` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `driver_profile_id` bigint NOT NULL,
  `version_number` int NOT NULL,
  `vehicle_type_id` bigint NOT NULL,
  `citizen_id_number` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `driver_license_number` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `license_plate` varchar(15) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `max_load_weight_kg` decimal(8,2) NOT NULL,
  `document_urls` json NOT NULL,
  `application_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Pending',
  `rejection_reasons` json DEFAULT NULL,
  `reviewed_by` bigint DEFAULT NULL,
  `reviewed_at` datetime(6) DEFAULT NULL,
  `submitted_at` datetime(6) NOT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_driver_applications_driver_profile_id_version_number` (`driver_profile_id`,`version_number`),
  KEY `IX_driver_applications_reviewed_by` (`reviewed_by`),
  KEY `IX_driver_applications_vehicle_type_id` (`vehicle_type_id`),
  CONSTRAINT `FK_driver_applications_driver_profiles_driver_profile_id` FOREIGN KEY (`driver_profile_id`) REFERENCES `driver_profiles` (`id`) ON DELETE CASCADE,
  CONSTRAINT `FK_driver_applications_users_reviewed_by` FOREIGN KEY (`reviewed_by`) REFERENCES `users` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_driver_applications_vehicle_types_vehicle_type_id` FOREIGN KEY (`vehicle_type_id`) REFERENCES `vehicle_types` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `driver_applications` (`id`, `driver_profile_id`, `version_number`, `vehicle_type_id`, `citizen_id_number`, `driver_license_number`, `license_plate`, `max_load_weight_kg`, `document_urls`, `application_status`, `rejection_reasons`, `reviewed_by`, `reviewed_at`, `submitted_at`, `created_at`, `updated_at`) VALUES (1,1,1,1,'079090000001','790000000001','51C-123.45',500.00,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Approved',NULL,1,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215','2026-11-10 14:58:29.028257',NULL);
INSERT INTO `driver_applications` (`id`, `driver_profile_id`, `version_number`, `vehicle_type_id`, `citizen_id_number`, `driver_license_number`, `license_plate`, `max_load_weight_kg`, `document_urls`, `application_status`, `rejection_reasons`, `reviewed_by`, `reviewed_at`, `submitted_at`, `created_at`, `updated_at`) VALUES (2,2,1,1,'079090000002','790000000002','51C-222.22',500.00,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Approved',NULL,1,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215','2026-11-10 14:58:29.308037',NULL);
INSERT INTO `driver_applications` (`id`, `driver_profile_id`, `version_number`, `vehicle_type_id`, `citizen_id_number`, `driver_license_number`, `license_plate`, `max_load_weight_kg`, `document_urls`, `application_status`, `rejection_reasons`, `reviewed_by`, `reviewed_at`, `submitted_at`, `created_at`, `updated_at`) VALUES (3,3,1,1,'079090000003','790000000003','51C-333.33',500.00,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Approved',NULL,1,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215','2026-11-10 14:58:29.463650',NULL);
INSERT INTO `driver_applications` (`id`, `driver_profile_id`, `version_number`, `vehicle_type_id`, `citizen_id_number`, `driver_license_number`, `license_plate`, `max_load_weight_kg`, `document_urls`, `application_status`, `rejection_reasons`, `reviewed_by`, `reviewed_at`, `submitted_at`, `created_at`, `updated_at`) VALUES (4,4,1,2,'079090000014','790000000014','51C-444.44',1000.00,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Pending',NULL,NULL,NULL,'2026-11-10 14:58:43.026466','2026-11-10 14:58:43.026466',NULL);
INSERT INTO `driver_applications` (`id`, `driver_profile_id`, `version_number`, `vehicle_type_id`, `citizen_id_number`, `driver_license_number`, `license_plate`, `max_load_weight_kg`, `document_urls`, `application_status`, `rejection_reasons`, `reviewed_by`, `reviewed_at`, `submitted_at`, `created_at`, `updated_at`) VALUES (5,5,1,1,'079090000015','790000000015','51C-555.55',500.00,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Rejected','[\"Ảnh giấy phép lái xe bị mờ\", \"Giấy đăng kiểm đã hết hạn\"]',1,'2026-11-10 14:58:43.026466','2026-11-10 14:58:43.026466','2026-11-10 14:58:43.026466',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `driver_location_logs` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `driver_profile_id` bigint NOT NULL,
  `order_id` bigint DEFAULT NULL,
  `latitude` decimal(9,6) NOT NULL,
  `longitude` decimal(9,6) NOT NULL,
  `recorded_at` datetime(6) NOT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  KEY `IX_driver_location_logs_driver_profile_id` (`driver_profile_id`),
  KEY `IX_driver_location_logs_order_id_recorded_at` (`order_id`,`recorded_at`),
  KEY `IX_driver_location_logs_recorded_at` (`recorded_at`),
  CONSTRAINT `FK_driver_location_logs_driver_profiles_driver_profile_id` FOREIGN KEY (`driver_profile_id`) REFERENCES `driver_profiles` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_driver_location_logs_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `driver_location_logs` (`id`, `driver_profile_id`, `order_id`, `latitude`, `longitude`, `recorded_at`, `created_at`) VALUES (1,1,1,10.783940,106.709000,'2026-11-10 14:55:16.599856','2026-11-10 14:55:16.599856');
INSERT INTO `driver_location_logs` (`id`, `driver_profile_id`, `order_id`, `latitude`, `longitude`, `recorded_at`, `created_at`) VALUES (2,1,1,10.790980,106.717100,'2026-11-10 14:56:16.599856','2026-11-10 14:56:16.599856');
INSERT INTO `driver_location_logs` (`id`, `driver_profile_id`, `order_id`, `latitude`, `longitude`, `recorded_at`, `created_at`) VALUES (3,1,1,10.798020,106.725200,'2026-11-10 14:57:16.599856','2026-11-10 14:57:16.599856');
INSERT INTO `driver_location_logs` (`id`, `driver_profile_id`, `order_id`, `latitude`, `longitude`, `recorded_at`, `created_at`) VALUES (4,1,1,10.805060,106.733300,'2026-11-10 14:58:16.599856','2026-11-10 14:58:16.599856');
INSERT INTO `driver_location_logs` (`id`, `driver_profile_id`, `order_id`, `latitude`, `longitude`, `recorded_at`, `created_at`) VALUES (5,1,1,10.812100,106.741400,'2026-11-10 14:59:16.599856','2026-11-10 14:59:16.599856');
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `driver_profiles` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `user_id` bigint NOT NULL,
  `citizen_id_number` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `driver_license_number` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `license_front_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `license_back_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `approval_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Pending',
  `online_status` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Offline',
  `last_latitude` decimal(9,6) DEFAULT NULL,
  `last_longitude` decimal(9,6) DEFAULT NULL,
  `last_location_at` datetime(6) DEFAULT NULL,
  `bank_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `bank_account_number` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `bank_account_holder` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_driver_profiles_citizen_id_number` (`citizen_id_number`),
  UNIQUE KEY `IX_driver_profiles_driver_license_number` (`driver_license_number`),
  UNIQUE KEY `IX_driver_profiles_user_id` (`user_id`),
  CONSTRAINT `FK_driver_profiles_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `driver_profiles` (`id`, `user_id`, `citizen_id_number`, `driver_license_number`, `license_front_url`, `license_back_url`, `approval_status`, `online_status`, `last_latitude`, `last_longitude`, `last_location_at`, `bank_name`, `bank_account_number`, `bank_account_holder`, `created_at`, `updated_at`) VALUES (1,4,'079090000001','790000000001','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','Approved','Online',10.801200,106.713000,'2026-11-10 14:57:03.335215','Vietcombank','0123456789','TAI XE DEMO','2026-11-10 14:58:28.979291','2026-11-10 14:58:29.229163');
INSERT INTO `driver_profiles` (`id`, `user_id`, `citizen_id_number`, `driver_license_number`, `license_front_url`, `license_back_url`, `approval_status`, `online_status`, `last_latitude`, `last_longitude`, `last_location_at`, `bank_name`, `bank_account_number`, `bank_account_holder`, `created_at`, `updated_at`) VALUES (2,5,'079090000002','790000000002','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','Approved','Online',10.805000,106.715000,'2026-11-10 14:57:03.335215','Vietcombank','0900000005','TÀI XẾ THAY THẾ','2026-11-10 14:58:29.276984','2026-11-10 14:58:29.414749');
INSERT INTO `driver_profiles` (`id`, `user_id`, `citizen_id_number`, `driver_license_number`, `license_front_url`, `license_back_url`, `approval_status`, `online_status`, `last_latitude`, `last_longitude`, `last_location_at`, `bank_name`, `bank_account_number`, `bank_account_holder`, `created_at`, `updated_at`) VALUES (3,6,'079090000003','790000000003','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','Approved','Online',10.803000,106.714000,'2026-11-10 14:46:03.335215','Vietcombank','0900000006','TÀI XẾ MẤT GPS','2026-11-10 14:58:29.443933','2026-11-10 14:58:29.544109');
INSERT INTO `driver_profiles` (`id`, `user_id`, `citizen_id_number`, `driver_license_number`, `license_front_url`, `license_back_url`, `approval_status`, `online_status`, `last_latitude`, `last_longitude`, `last_location_at`, `bank_name`, `bank_account_number`, `bank_account_holder`, `created_at`, `updated_at`) VALUES (4,10,'079090000014','790000000014','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','Pending','Offline',NULL,NULL,NULL,NULL,NULL,NULL,'2026-11-10 14:58:43.026466',NULL);
INSERT INTO `driver_profiles` (`id`, `user_id`, `citizen_id_number`, `driver_license_number`, `license_front_url`, `license_back_url`, `approval_status`, `online_status`, `last_latitude`, `last_longitude`, `last_location_at`, `bank_name`, `bank_account_number`, `bank_account_holder`, `created_at`, `updated_at`) VALUES (5,11,'079090000015','790000000015','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','Rejected','Offline',NULL,NULL,NULL,NULL,NULL,NULL,'2026-11-10 14:58:43.026466',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `driver_wallets` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `driver_profile_id` bigint NOT NULL,
  `deposit_balance` decimal(15,2) NOT NULL DEFAULT '0.00',
  `earnings_balance` decimal(15,2) NOT NULL DEFAULT '0.00',
  `wallet_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Active',
  `row_version` int NOT NULL DEFAULT '0',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_driver_wallets_driver_profile_id` (`driver_profile_id`),
  CONSTRAINT `FK_driver_wallets_driver_profiles_driver_profile_id` FOREIGN KEY (`driver_profile_id`) REFERENCES `driver_profiles` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `driver_wallets` (`id`, `driver_profile_id`, `deposit_balance`, `earnings_balance`, `wallet_status`, `row_version`, `created_at`, `updated_at`) VALUES (1,1,500000.00,89000.00,'Active',3,'2026-11-10 14:58:29.182129','2026-11-10 14:58:31.152448');
INSERT INTO `driver_wallets` (`id`, `driver_profile_id`, `deposit_balance`, `earnings_balance`, `wallet_status`, `row_version`, `created_at`, `updated_at`) VALUES (2,2,500000.00,0.00,'Active',1,'2026-11-10 14:58:29.381267',NULL);
INSERT INTO `driver_wallets` (`id`, `driver_profile_id`, `deposit_balance`, `earnings_balance`, `wallet_status`, `row_version`, `created_at`, `updated_at`) VALUES (3,3,500000.00,0.00,'Active',1,'2026-11-10 14:58:29.521315',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `in_transit_incidents` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `order_id` bigint NOT NULL,
  `reported_by` bigint NOT NULL,
  `incident_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `photo_urls` json DEFAULT NULL,
  `latitude` decimal(9,6) DEFAULT NULL,
  `longitude` decimal(9,6) DEFAULT NULL,
  `resolution_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Open',
  `resolution_decision` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `resolution_note` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `handled_by` bigint DEFAULT NULL,
  `resolved_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `IX_in_transit_incidents_handled_by` (`handled_by`),
  KEY `IX_in_transit_incidents_order_id` (`order_id`),
  KEY `IX_in_transit_incidents_reported_by` (`reported_by`),
  CONSTRAINT `FK_in_transit_incidents_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_in_transit_incidents_users_handled_by` FOREIGN KEY (`handled_by`) REFERENCES `users` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_in_transit_incidents_users_reported_by` FOREIGN KEY (`reported_by`) REFERENCES `users` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `in_transit_incidents` (`id`, `order_id`, `reported_by`, `incident_type`, `description`, `photo_urls`, `latitude`, `longitude`, `resolution_status`, `resolution_decision`, `resolution_note`, `handled_by`, `resolved_at`, `created_at`, `updated_at`) VALUES (1,1,4,'VehicleBreakdown','Xe bị nổ lốp trên đường, cần hỗ trợ đổi xe.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',10.801200,106.713000,'Open',NULL,NULL,NULL,NULL,'2026-11-10 14:58:29.753118',NULL);
INSERT INTO `in_transit_incidents` (`id`, `order_id`, `reported_by`, `incident_type`, `description`, `photo_urls`, `latitude`, `longitude`, `resolution_status`, `resolution_decision`, `resolution_note`, `handled_by`, `resolved_at`, `created_at`, `updated_at`) VALUES (2,2,3,'DamagedCargo','Khách thấy hàng bị móp khi tài xế bốc lên xe.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,NULL,'InProgress',NULL,NULL,2,NULL,'2026-11-10 14:58:29.807072',NULL);
INSERT INTO `in_transit_incidents` (`id`, `order_id`, `reported_by`, `incident_type`, `description`, `photo_urls`, `latitude`, `longitude`, `resolution_status`, `resolution_decision`, `resolution_note`, `handled_by`, `resolved_at`, `created_at`, `updated_at`) VALUES (3,3,4,'Accident','Va chạm nhẹ, xe vẫn chạy được, hàng không hư hại.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',10.790500,106.680200,'Resolved','ContinueTrip','Đã gọi tài xế, hàng nguyên vẹn, tiếp tục chuyến.',2,'2026-11-10 14:58:03.335215','2026-11-10 14:58:29.910528',NULL);
INSERT INTO `in_transit_incidents` (`id`, `order_id`, `reported_by`, `incident_type`, `description`, `photo_urls`, `latitude`, `longitude`, `resolution_status`, `resolution_decision`, `resolution_note`, `handled_by`, `resolved_at`, `created_at`, `updated_at`) VALUES (4,4,3,'Other','Tài xế đến trễ, tôi muốn báo sự cố.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',NULL,NULL,'Cancelled',NULL,'Không có lý do hợp lệ, chuyến vẫn tiếp tục.',2,'2026-11-10 14:58:03.335215','2026-11-10 14:58:29.986359',NULL);
INSERT INTO `in_transit_incidents` (`id`, `order_id`, `reported_by`, `incident_type`, `description`, `photo_urls`, `latitude`, `longitude`, `resolution_status`, `resolution_decision`, `resolution_note`, `handled_by`, `resolved_at`, `created_at`, `updated_at`) VALUES (5,5,4,'Other','Khách không nghe máy, đã chờ quá 15 phút tại điểm lấy.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',10.776900,106.700900,'Open',NULL,NULL,NULL,NULL,'2026-11-10 14:58:30.047811',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `master_data_items` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `category` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  `description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_master_data_items_category_code` (`category`,`code`)
) ENGINE=InnoDB AUTO_INCREMENT=57 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (1,'CARGO_TYPE','FURNITURE','Đồ nội thất',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (2,'CARGO_TYPE','APPLIANCE','Đồ điện gia dụng',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (3,'CARGO_TYPE','ELECTRONICS','Thiết bị điện tử',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (4,'CARGO_TYPE','GOODS','Hàng hóa kinh doanh',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (5,'CARGO_TYPE','DOCUMENT','Tài liệu, giấy tờ',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (6,'CARGO_TYPE','OTHER','Khác',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (7,'INCIDENT_CATEGORY','VEHICLE_BREAKDOWN','Xe hỏng',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (8,'INCIDENT_CATEGORY','DAMAGED_CARGO','Hàng hóa hư hỏng',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (9,'INCIDENT_CATEGORY','ACCIDENT','Tai nạn',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (10,'INCIDENT_CATEGORY','OTHER','Khác',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (11,'COMPLAINT_REASON','DAMAGED_CARGO','Hàng bị hư hỏng',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (12,'COMPLAINT_REASON','LATE_DELIVERY','Giao hàng trễ',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (13,'COMPLAINT_REASON','WRONG_ROUTE','Đi sai lộ trình',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (14,'COMPLAINT_REASON','SERVICE_ATTITUDE','Thái độ phục vụ',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (15,'COMPLAINT_REASON','OTHER','Khác',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (16,'CANCELLATION_REASON','CHANGED_PLAN','Thay đổi kế hoạch',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (17,'CANCELLATION_REASON','WRONG_INFO','Nhập sai thông tin đơn',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (18,'CANCELLATION_REASON','FOUND_OTHER_SERVICE','Đã tìm được dịch vụ khác',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (19,'CANCELLATION_REASON','WAITED_TOO_LONG','Chờ tài xế quá lâu',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (20,'CANCELLATION_REASON','PRICE_TOO_HIGH','Giá cao hơn dự kiến',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (21,'CANCELLATION_REASON','OTHER','Khác',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (22,'POLICY','WARRANTY','Chính sách bảo hành',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (23,'POLICY','REFUND','Chính sách hoàn tiền',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (24,'POLICY','TERMS_OF_USE','Điều khoản sử dụng',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (25,'POLICY','PRIVACY','Chính sách bảo mật',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (26,'SERVICE_INFO','ABOUT','Giới thiệu ModMove',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (27,'SERVICE_INFO','COVERAGE_AREA','Khu vực phục vụ',1,'2026-11-10 14:58:28.117135',NULL,NULL);
INSERT INTO `master_data_items` (`id`, `category`, `code`, `name`, `is_active`, `created_at`, `updated_at`, `description`) VALUES (28,'SERVICE_INFO','BENEFITS','Lợi ích khi sử dụng',1,'2026-11-10 14:58:28.117135',NULL,NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `order_status_histories` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `order_id` bigint NOT NULL,
  `from_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `to_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `changed_by_role` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `changed_by_user_id` bigint DEFAULT NULL,
  `note` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  KEY `IX_order_status_histories_changed_by_user_id` (`changed_by_user_id`),
  KEY `IX_order_status_histories_order_id` (`order_id`),
  CONSTRAINT `FK_order_status_histories_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE CASCADE,
  CONSTRAINT `FK_order_status_histories_users_changed_by_user_id` FOREIGN KEY (`changed_by_user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=58 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (1,1,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:29.666475');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (2,1,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:29.666475');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (3,1,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:29.666475');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (4,1,'Arrived','PickedUp','Driver',4,NULL,'2026-11-10 14:58:29.666475');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (5,1,'PickedUp','InTransit','Customer',3,NULL,'2026-11-10 14:58:29.666475');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (6,2,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:29.781070');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (7,2,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:29.781070');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (8,2,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:29.781070');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (9,2,'Arrived','PickedUp','Driver',4,NULL,'2026-11-10 14:58:29.781070');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (10,3,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:29.881853');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (11,3,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:29.881853');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (12,3,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:29.881853');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (13,3,'Arrived','PickedUp','Driver',4,NULL,'2026-11-10 14:58:29.881853');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (14,3,'PickedUp','InTransit','Customer',3,NULL,'2026-11-10 14:58:29.881853');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (15,4,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:29.960855');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (16,4,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:29.960855');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (17,4,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:29.960855');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (18,5,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.020935');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (19,5,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:30.020935');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (20,5,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:30.020935');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (21,6,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.068486');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (22,6,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:30.068486');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (23,6,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:30.068486');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (24,6,'Arrived','PickedUp','Driver',4,NULL,'2026-11-10 14:58:30.068486');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (25,6,'PickedUp','InTransit','Customer',3,NULL,'2026-11-10 14:58:30.068486');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (26,6,'InTransit','Delivered','Driver',4,NULL,'2026-11-10 14:58:30.068486');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (27,7,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.140775');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (28,7,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:30.140775');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (29,7,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:30.140775');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (30,7,'Arrived','PickedUp','Driver',4,NULL,'2026-11-10 14:58:30.140775');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (31,7,'PickedUp','InTransit','Customer',3,NULL,'2026-11-10 14:58:30.140775');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (32,7,'InTransit','Delivered','Driver',4,NULL,'2026-11-10 14:58:30.140775');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (33,8,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.309016');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (34,8,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:30.309016');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (35,8,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:30.309016');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (36,8,'Arrived','PickedUp','Driver',4,NULL,'2026-11-10 14:58:30.309016');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (37,8,'PickedUp','InTransit','Customer',3,NULL,'2026-11-10 14:58:30.309016');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (38,8,'InTransit','Delivered','Driver',4,NULL,'2026-11-10 14:58:30.309016');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (39,9,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.436735');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (40,9,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:30.436735');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (41,9,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:30.436735');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (42,9,'Arrived','PickedUp','Driver',4,NULL,'2026-11-10 14:58:30.436735');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (43,9,'PickedUp','InTransit','Customer',3,NULL,'2026-11-10 14:58:30.436735');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (44,9,'InTransit','Delivered','Driver',4,NULL,'2026-11-10 14:58:30.436735');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (45,10,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.569146');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (46,10,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:30.569146');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (47,10,'Accepted','Arrived','Driver',4,NULL,'2026-11-10 14:58:30.569146');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (48,10,'Arrived','PickedUp','Driver',4,NULL,'2026-11-10 14:58:30.569146');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (49,10,'PickedUp','InTransit','Customer',3,NULL,'2026-11-10 14:58:30.569146');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (50,10,'InTransit','Delivered','Driver',4,NULL,'2026-11-10 14:58:30.569146');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (51,10,'Delivered','Completed','System',NULL,NULL,'2026-11-10 14:58:30.569146');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (52,11,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.771999');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (53,12,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.840290');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (54,12,'Pending','Accepted','Driver',4,NULL,'2026-11-10 14:58:30.840290');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (55,13,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:30.895047');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (56,13,'Pending','Cancelled','Customer',3,'Khách hủy đơn trước khi ghép tài xế.','2026-11-10 14:58:30.895047');
INSERT INTO `order_status_histories` (`id`, `order_id`, `from_status`, `to_status`, `changed_by_role`, `changed_by_user_id`, `note`, `created_at`) VALUES (57,14,NULL,'Pending','Customer',3,NULL,'2026-11-10 14:58:31.046387');
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `otp_verifications` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `purpose` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `code_hash` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `expires_at` datetime(6) NOT NULL,
  `send_count` int NOT NULL DEFAULT '1',
  `window_started_at` datetime(6) NOT NULL,
  `verified_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `failed_attempts` int NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `IX_otp_verifications_email` (`email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `payment_transactions` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `order_id` bigint DEFAULT NULL,
  `wallet_id` bigint DEFAULT NULL,
  `payment_method` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `payer_role` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `amount` decimal(15,2) NOT NULL,
  `payos_order_code` bigint DEFAULT NULL,
  `checkout_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `transaction_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Pending',
  `expires_at` datetime(6) DEFAULT NULL,
  `paid_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_payment_transactions_payos_order_code` (`payos_order_code`),
  KEY `IX_payment_transactions_order_id` (`order_id`),
  KEY `IX_payment_transactions_wallet_id` (`wallet_id`),
  CONSTRAINT `FK_payment_transactions_driver_wallets_wallet_id` FOREIGN KEY (`wallet_id`) REFERENCES `driver_wallets` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_payment_transactions_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_payment_transactions_one_target` CHECK (((`order_id` is null) <> (`wallet_id` is null)))
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `payment_transactions` (`id`, `order_id`, `wallet_id`, `payment_method`, `payer_role`, `amount`, `payos_order_code`, `checkout_url`, `transaction_status`, `expires_at`, `paid_at`, `created_at`, `updated_at`) VALUES (1,2,NULL,'PayOS',NULL,220000.00,2611100002,NULL,'Paid',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL);
INSERT INTO `payment_transactions` (`id`, `order_id`, `wallet_id`, `payment_method`, `payer_role`, `amount`, `payos_order_code`, `checkout_url`, `transaction_status`, `expires_at`, `paid_at`, `created_at`, `updated_at`) VALUES (2,5,NULL,'PayOS',NULL,260000.00,2611100005,NULL,'Paid',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL);
INSERT INTO `payment_transactions` (`id`, `order_id`, `wallet_id`, `payment_method`, `payer_role`, `amount`, `payos_order_code`, `checkout_url`, `transaction_status`, `expires_at`, `paid_at`, `created_at`, `updated_at`) VALUES (3,6,NULL,'PayOS',NULL,240000.00,2611100006,NULL,'Paid',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL);
INSERT INTO `payment_transactions` (`id`, `order_id`, `wallet_id`, `payment_method`, `payer_role`, `amount`, `payos_order_code`, `checkout_url`, `transaction_status`, `expires_at`, `paid_at`, `created_at`, `updated_at`) VALUES (4,8,NULL,'PayOS',NULL,280000.00,2611100008,NULL,'Paid',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL);
INSERT INTO `payment_transactions` (`id`, `order_id`, `wallet_id`, `payment_method`, `payer_role`, `amount`, `payos_order_code`, `checkout_url`, `transaction_status`, `expires_at`, `paid_at`, `created_at`, `updated_at`) VALUES (5,9,NULL,'PayOS',NULL,320000.00,2611100009,NULL,'Paid',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL);
INSERT INTO `payment_transactions` (`id`, `order_id`, `wallet_id`, `payment_method`, `payer_role`, `amount`, `payos_order_code`, `checkout_url`, `transaction_status`, `expires_at`, `paid_at`, `created_at`, `updated_at`) VALUES (6,10,NULL,'PayOS',NULL,210000.00,2611100010,NULL,'Paid',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL);
INSERT INTO `payment_transactions` (`id`, `order_id`, `wallet_id`, `payment_method`, `payer_role`, `amount`, `payos_order_code`, `checkout_url`, `transaction_status`, `expires_at`, `paid_at`, `created_at`, `updated_at`) VALUES (7,12,NULL,'PayOS',NULL,195000.00,2611100012,NULL,'Paid',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL);
INSERT INTO `payment_transactions` (`id`, `order_id`, `wallet_id`, `payment_method`, `payer_role`, `amount`, `payos_order_code`, `checkout_url`, `transaction_status`, `expires_at`, `paid_at`, `created_at`, `updated_at`) VALUES (8,NULL,1,'PayOS',NULL,500000.00,2611109001,NULL,'Paid',NULL,'2026-11-10 14:58:29.182129','2026-11-10 14:58:29.182129',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `platform_settings` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `setting_key` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `setting_value` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `effective_from` datetime(6) NOT NULL,
  `updated_by` bigint DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_platform_settings_setting_key` (`setting_key`),
  KEY `IX_platform_settings_updated_by` (`updated_by`),
  CONSTRAINT `FK_platform_settings_users_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `platform_settings` (`id`, `setting_key`, `setting_value`, `effective_from`, `updated_by`, `created_at`, `updated_at`) VALUES (1,'COMMISSION_RATE','0.10','2026-11-10 14:58:03.335215',NULL,'2026-11-10 14:58:28.117135',NULL);
INSERT INTO `platform_settings` (`id`, `setting_key`, `setting_value`, `effective_from`, `updated_by`, `created_at`, `updated_at`) VALUES (2,'LOADING_FEE','100000','2026-11-10 14:58:03.335215',NULL,'2026-11-10 14:58:28.117135',NULL);
INSERT INTO `platform_settings` (`id`, `setting_key`, `setting_value`, `effective_from`, `updated_by`, `created_at`, `updated_at`) VALUES (3,'MIN_COD_DEPOSIT','200000','2026-11-10 14:58:03.335215',NULL,'2026-11-10 14:58:28.117135',NULL);
INSERT INTO `platform_settings` (`id`, `setting_key`, `setting_value`, `effective_from`, `updated_by`, `created_at`, `updated_at`) VALUES (4,'MATCHING_RADIUS_KM','5','2026-11-10 14:58:03.335215',NULL,'2026-11-10 14:58:28.117135',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `proof_of_deliveries` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `order_id` bigint NOT NULL,
  `pickup_photo_urls` json DEFAULT NULL,
  `proof_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Pending',
  `rejection_reason` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delivery_photo_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `delivery_completion_time` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  `pickup_submission_count` int NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_proof_of_deliveries_order_id` (`order_id`),
  CONSTRAINT `FK_proof_of_deliveries_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `proof_of_deliveries` (`id`, `order_id`, `pickup_photo_urls`, `proof_status`, `rejection_reason`, `delivery_photo_url`, `delivery_completion_time`, `created_at`, `updated_at`, `pickup_submission_count`) VALUES (1,1,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Verified',NULL,NULL,NULL,'2026-11-10 14:58:29.666475',NULL,1);
INSERT INTO `proof_of_deliveries` (`id`, `order_id`, `pickup_photo_urls`, `proof_status`, `rejection_reason`, `delivery_photo_url`, `delivery_completion_time`, `created_at`, `updated_at`, `pickup_submission_count`) VALUES (2,2,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Pending',NULL,NULL,NULL,'2026-11-10 14:58:29.781070',NULL,1);
INSERT INTO `proof_of_deliveries` (`id`, `order_id`, `pickup_photo_urls`, `proof_status`, `rejection_reason`, `delivery_photo_url`, `delivery_completion_time`, `created_at`, `updated_at`, `pickup_submission_count`) VALUES (3,3,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Verified',NULL,NULL,NULL,'2026-11-10 14:58:29.881853',NULL,1);
INSERT INTO `proof_of_deliveries` (`id`, `order_id`, `pickup_photo_urls`, `proof_status`, `rejection_reason`, `delivery_photo_url`, `delivery_completion_time`, `created_at`, `updated_at`, `pickup_submission_count`) VALUES (4,6,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Verified',NULL,'https://res.cloudinary.com/demo/image/upload/sample.jpg','2026-11-10 13:58:03.335215','2026-11-10 14:58:30.068486',NULL,1);
INSERT INTO `proof_of_deliveries` (`id`, `order_id`, `pickup_photo_urls`, `proof_status`, `rejection_reason`, `delivery_photo_url`, `delivery_completion_time`, `created_at`, `updated_at`, `pickup_submission_count`) VALUES (5,7,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Verified',NULL,'https://res.cloudinary.com/demo/image/upload/sample.jpg','2026-11-10 13:58:03.335215','2026-11-10 14:58:30.140775',NULL,1);
INSERT INTO `proof_of_deliveries` (`id`, `order_id`, `pickup_photo_urls`, `proof_status`, `rejection_reason`, `delivery_photo_url`, `delivery_completion_time`, `created_at`, `updated_at`, `pickup_submission_count`) VALUES (6,8,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Verified',NULL,'https://res.cloudinary.com/demo/image/upload/sample.jpg','2026-11-10 13:58:03.335215','2026-11-10 14:58:30.309016',NULL,1);
INSERT INTO `proof_of_deliveries` (`id`, `order_id`, `pickup_photo_urls`, `proof_status`, `rejection_reason`, `delivery_photo_url`, `delivery_completion_time`, `created_at`, `updated_at`, `pickup_submission_count`) VALUES (7,9,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Verified',NULL,'https://res.cloudinary.com/demo/image/upload/sample.jpg','2026-11-10 13:58:03.335215','2026-11-10 14:58:30.436735',NULL,1);
INSERT INTO `proof_of_deliveries` (`id`, `order_id`, `pickup_photo_urls`, `proof_status`, `rejection_reason`, `delivery_photo_url`, `delivery_completion_time`, `created_at`, `updated_at`, `pickup_submission_count`) VALUES (8,10,'[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]','Verified',NULL,'https://res.cloudinary.com/demo/image/upload/sample.jpg','2026-11-08 14:58:03.335215','2026-11-10 14:58:30.569146',NULL,1);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ratings` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `order_id` bigint NOT NULL,
  `customer_id` bigint NOT NULL,
  `driver_profile_id` bigint NOT NULL,
  `rating_stars` tinyint NOT NULL,
  `review_comment` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_ratings_order_id` (`order_id`),
  KEY `IX_ratings_customer_id` (`customer_id`),
  KEY `IX_ratings_driver_profile_id` (`driver_profile_id`),
  CONSTRAINT `FK_ratings_driver_profiles_driver_profile_id` FOREIGN KEY (`driver_profile_id`) REFERENCES `driver_profiles` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_ratings_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_ratings_users_customer_id` FOREIGN KEY (`customer_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_ratings_stars` CHECK ((`rating_stars` between 1 and 5))
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `ratings` (`id`, `order_id`, `customer_id`, `driver_profile_id`, `rating_stars`, `review_comment`, `created_at`) VALUES (1,10,3,1,2,'Tài xế nói chuyện cộc lốc.','2026-11-10 14:58:30.703643');
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `refresh_tokens` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `user_id` bigint NOT NULL,
  `token_hash` char(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `expires_at` datetime(6) NOT NULL,
  `revoked_at` datetime(6) DEFAULT NULL,
  `replaced_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_refresh_tokens_token_hash` (`token_hash`),
  KEY `IX_refresh_tokens_user_id` (`user_id`),
  CONSTRAINT `FK_refresh_tokens_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `refunds` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `order_id` bigint NOT NULL,
  `incident_id` bigint DEFAULT NULL,
  `source_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `reason` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `evidence_urls` json DEFAULT NULL,
  `requested_amount` decimal(15,2) NOT NULL,
  `proposed_amount` decimal(15,2) DEFAULT NULL,
  `proposal_reason` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `proposed_by` bigint DEFAULT NULL,
  `approved_by` bigint DEFAULT NULL,
  `info_request_note` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `refund_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Requested',
  `bank_reference_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `decided_at` datetime(6) DEFAULT NULL,
  `processed_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  `customer_reply` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `decision_note` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `reason_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `row_version` int NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `IX_refunds_approved_by` (`approved_by`),
  KEY `IX_refunds_incident_id` (`incident_id`),
  KEY `IX_refunds_order_id_source_type` (`order_id`,`source_type`),
  KEY `IX_refunds_proposed_by` (`proposed_by`),
  CONSTRAINT `FK_refunds_in_transit_incidents_incident_id` FOREIGN KEY (`incident_id`) REFERENCES `in_transit_incidents` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_refunds_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_refunds_users_approved_by` FOREIGN KEY (`approved_by`) REFERENCES `users` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_refunds_users_proposed_by` FOREIGN KEY (`proposed_by`) REFERENCES `users` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `refunds` (`id`, `order_id`, `incident_id`, `source_type`, `reason`, `evidence_urls`, `requested_amount`, `proposed_amount`, `proposal_reason`, `proposed_by`, `approved_by`, `info_request_note`, `refund_status`, `bank_reference_code`, `decided_at`, `processed_at`, `created_at`, `updated_at`, `customer_reply`, `decision_note`, `reason_code`, `row_version`) VALUES (1,2,2,'Incident','Yêu cầu bồi thường phần vỏ thùng bị móp khi bốc xếp.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',100000.00,NULL,NULL,NULL,NULL,NULL,'Requested',NULL,NULL,NULL,'2026-11-10 14:58:29.856002',NULL,NULL,NULL,NULL,0);
INSERT INTO `refunds` (`id`, `order_id`, `incident_id`, `source_type`, `reason`, `evidence_urls`, `requested_amount`, `proposed_amount`, `proposal_reason`, `proposed_by`, `approved_by`, `info_request_note`, `refund_status`, `bank_reference_code`, `decided_at`, `processed_at`, `created_at`, `updated_at`, `customer_reply`, `decision_note`, `reason_code`, `row_version`) VALUES (2,6,NULL,'Complaint','Hàng bị hỏng khi giao: tủ bị trầy xước nhiều chỗ.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',240000.00,NULL,NULL,NULL,NULL,NULL,'Requested',NULL,NULL,NULL,'2026-11-10 14:58:30.107797',NULL,NULL,NULL,'DAMAGED_CARGO',0);
INSERT INTO `refunds` (`id`, `order_id`, `incident_id`, `source_type`, `reason`, `evidence_urls`, `requested_amount`, `proposed_amount`, `proposal_reason`, `proposed_by`, `approved_by`, `info_request_note`, `refund_status`, `bank_reference_code`, `decided_at`, `processed_at`, `created_at`, `updated_at`, `customer_reply`, `decision_note`, `reason_code`, `row_version`) VALUES (3,7,NULL,'Complaint','Thiếu 2 thùng đồ so với lúc lấy hàng.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',200000.00,NULL,NULL,NULL,NULL,'Vui lòng gửi ảnh hàng khi nhận và danh sách đồ bị thiếu.','Requested',NULL,NULL,NULL,'2026-11-10 14:58:30.242741',NULL,NULL,NULL,'OTHER',1);
INSERT INTO `refunds` (`id`, `order_id`, `incident_id`, `source_type`, `reason`, `evidence_urls`, `requested_amount`, `proposed_amount`, `proposal_reason`, `proposed_by`, `approved_by`, `info_request_note`, `refund_status`, `bank_reference_code`, `decided_at`, `processed_at`, `created_at`, `updated_at`, `customer_reply`, `decision_note`, `reason_code`, `row_version`) VALUES (4,8,NULL,'Complaint','Tài xế giao muộn hơn 2 giờ so với dự kiến.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\", \"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',100000.00,NULL,NULL,NULL,NULL,'Bạn có tin nhắn hoặc ảnh chụp thời điểm nhận hàng không?','Requested',NULL,NULL,NULL,'2026-11-10 14:58:30.374847',NULL,'Tôi nhận hàng lúc 17:30 trong khi hẹn 15:00, có ảnh chụp màn hình.',NULL,'LATE_DELIVERY',2);
INSERT INTO `refunds` (`id`, `order_id`, `incident_id`, `source_type`, `reason`, `evidence_urls`, `requested_amount`, `proposed_amount`, `proposal_reason`, `proposed_by`, `approved_by`, `info_request_note`, `refund_status`, `bank_reference_code`, `decided_at`, `processed_at`, `created_at`, `updated_at`, `customer_reply`, `decision_note`, `reason_code`, `row_version`) VALUES (5,9,NULL,'Complaint','Hàng bị ướt do xe không che bạt.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',320000.00,160000.00,'Ảnh giao hàng cho thấy bạt che chưa kín, đề xuất hoàn 50%.',2,NULL,NULL,'Requested',NULL,NULL,NULL,'2026-11-10 14:58:30.490611',NULL,NULL,NULL,'DAMAGED_CARGO',1);
INSERT INTO `refunds` (`id`, `order_id`, `incident_id`, `source_type`, `reason`, `evidence_urls`, `requested_amount`, `proposed_amount`, `proposal_reason`, `proposed_by`, `approved_by`, `info_request_note`, `refund_status`, `bank_reference_code`, `decided_at`, `processed_at`, `created_at`, `updated_at`, `customer_reply`, `decision_note`, `reason_code`, `row_version`) VALUES (6,10,NULL,'Complaint','Tài xế thái độ không tốt, yêu cầu hoàn tiền.','[\"https://res.cloudinary.com/demo/image/upload/sample.jpg\"]',210000.00,0.00,'Không có bằng chứng về thái độ; ảnh giao hàng bình thường.',2,1,NULL,'Rejected',NULL,'2026-11-10 14:58:03.335215',NULL,'2026-11-10 14:58:30.644341',NULL,NULL,NULL,'SERVICE_ATTITUDE',2);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `shipping_orders` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `order_code` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `customer_id` bigint NOT NULL,
  `driver_profile_id` bigint DEFAULT NULL,
  `vehicle_type_id` bigint NOT NULL,
  `coupon_id` bigint DEFAULT NULL,
  `pickup_address` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `pickup_latitude` decimal(9,6) NOT NULL,
  `pickup_longitude` decimal(9,6) NOT NULL,
  `dropoff_address` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `dropoff_latitude` decimal(9,6) NOT NULL,
  `dropoff_longitude` decimal(9,6) NOT NULL,
  `receiver_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `receiver_phone` varchar(15) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `estimated_distance_km` decimal(8,2) NOT NULL,
  `estimated_duration_min` int NOT NULL,
  `has_loading_service` tinyint(1) NOT NULL DEFAULT '0',
  `distance_fee` decimal(15,2) NOT NULL,
  `service_fee` decimal(15,2) NOT NULL DEFAULT '0.00',
  `discount_amount` decimal(15,2) NOT NULL DEFAULT '0.00',
  `total_fee` decimal(15,2) NOT NULL,
  `payment_method` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `payer_role` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `payment_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Unpaid',
  `order_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `cancellation_reason` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `warranty_accepted_at` datetime(6) NOT NULL,
  `accepted_at` datetime(6) DEFAULT NULL,
  `delivered_at` datetime(6) DEFAULT NULL,
  `completed_at` datetime(6) DEFAULT NULL,
  `row_version` int NOT NULL DEFAULT '0',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  `commission_amount` decimal(15,2) DEFAULT NULL,
  `commission_rate` decimal(5,4) DEFAULT NULL,
  `paid_at` datetime(6) DEFAULT NULL,
  `cancellation_reason_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_shipping_orders_order_code` (`order_code`),
  KEY `IX_shipping_orders_coupon_id` (`coupon_id`),
  KEY `IX_shipping_orders_customer_id` (`customer_id`),
  KEY `IX_shipping_orders_driver_profile_id` (`driver_profile_id`),
  KEY `IX_shipping_orders_order_status` (`order_status`),
  KEY `IX_shipping_orders_vehicle_type_id` (`vehicle_type_id`),
  CONSTRAINT `FK_shipping_orders_coupons_coupon_id` FOREIGN KEY (`coupon_id`) REFERENCES `coupons` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_shipping_orders_driver_profiles_driver_profile_id` FOREIGN KEY (`driver_profile_id`) REFERENCES `driver_profiles` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_shipping_orders_users_customer_id` FOREIGN KEY (`customer_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_shipping_orders_vehicle_types_vehicle_type_id` FOREIGN KEY (`vehicle_type_id`) REFERENCES `vehicle_types` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (1,'MMDEMO001',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,180000.00,0.00,0.00,180000.00,'Cash','Sender','Unpaid','InTransit',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL,NULL,1,'2026-11-10 14:58:29.666475',NULL,NULL,NULL,NULL,NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (2,'MMDEMO002',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,220000.00,0.00,0.00,220000.00,'PayOS',NULL,'Paid','PickedUp',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL,NULL,2,'2026-11-10 14:58:29.781070',NULL,NULL,NULL,'2026-11-10 14:58:03.335215',NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (3,'MMDEMO003',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,150000.00,0.00,0.00,150000.00,'Cash','Sender','Unpaid','InTransit',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL,NULL,1,'2026-11-10 14:58:29.881853',NULL,NULL,NULL,NULL,NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (4,'MMDEMO004',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,130000.00,0.00,0.00,130000.00,'Cash','Sender','Unpaid','Arrived',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL,NULL,1,'2026-11-10 14:58:29.960855',NULL,NULL,NULL,NULL,NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (5,'MMDEMO005',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,260000.00,0.00,0.00,260000.00,'PayOS',NULL,'Paid','Arrived',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL,NULL,2,'2026-11-10 14:58:30.020935',NULL,NULL,NULL,'2026-11-10 14:58:03.335215',NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (6,'MMDEMO006',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,240000.00,0.00,0.00,240000.00,'PayOS',NULL,'Paid','Delivered',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215','2026-11-10 13:58:03.335215',NULL,2,'2026-11-10 14:58:30.068486',NULL,NULL,NULL,'2026-11-10 14:58:03.335215',NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (7,'MMDEMO007',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,200000.00,0.00,0.00,200000.00,'Cash','Sender','Unpaid','Delivered',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215','2026-11-10 13:58:03.335215',NULL,1,'2026-11-10 14:58:30.140775',NULL,NULL,NULL,NULL,NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (8,'MMDEMO008',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,280000.00,0.00,0.00,280000.00,'PayOS',NULL,'Paid','Delivered',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215','2026-11-10 13:58:03.335215',NULL,2,'2026-11-10 14:58:30.309016',NULL,NULL,NULL,'2026-11-10 14:58:03.335215',NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (9,'MMDEMO009',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,320000.00,0.00,0.00,320000.00,'PayOS',NULL,'Paid','Delivered',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215','2026-11-10 13:58:03.335215',NULL,2,'2026-11-10 14:58:30.436735',NULL,NULL,NULL,'2026-11-10 14:58:03.335215',NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (10,'MMDEMO010',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,210000.00,0.00,0.00,210000.00,'PayOS',NULL,'Paid','Completed',NULL,'2026-11-10 14:58:03.335215','2026-11-08 14:58:03.335215','2026-11-08 14:58:03.335215','2026-11-10 13:58:03.335215',3,'2026-11-10 14:58:30.569146',NULL,21000.00,0.1000,'2026-11-10 14:58:03.335215',NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (11,'MMDEMO011',3,NULL,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,170000.00,0.00,0.00,170000.00,'Cash','Sender','Unpaid','Pending',NULL,'2026-11-10 14:58:03.335215',NULL,NULL,NULL,0,'2026-11-10 14:58:30.771999',NULL,NULL,NULL,NULL,NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (12,'MMDEMO012',3,1,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,195000.00,0.00,0.00,195000.00,'PayOS',NULL,'Paid','Accepted',NULL,'2026-11-10 14:58:03.335215','2026-11-10 14:58:03.335215',NULL,NULL,2,'2026-11-10 14:58:30.840290',NULL,NULL,NULL,'2026-11-10 14:58:03.335215',NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (13,'MMDEMO013',3,NULL,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,160000.00,0.00,0.00,160000.00,'Cash','Sender','Unpaid','Cancelled','Khách hủy đơn trước khi ghép tài xế.','2026-11-10 14:58:03.335215',NULL,NULL,NULL,1,'2026-11-10 14:58:30.895047',NULL,NULL,NULL,NULL,NULL);
INSERT INTO `shipping_orders` (`id`, `order_code`, `customer_id`, `driver_profile_id`, `vehicle_type_id`, `coupon_id`, `pickup_address`, `pickup_latitude`, `pickup_longitude`, `dropoff_address`, `dropoff_latitude`, `dropoff_longitude`, `receiver_name`, `receiver_phone`, `estimated_distance_km`, `estimated_duration_min`, `has_loading_service`, `distance_fee`, `service_fee`, `discount_amount`, `total_fee`, `payment_method`, `payer_role`, `payment_status`, `order_status`, `cancellation_reason`, `warranty_accepted_at`, `accepted_at`, `delivered_at`, `completed_at`, `row_version`, `created_at`, `updated_at`, `commission_amount`, `commission_rate`, `paid_at`, `cancellation_reason_code`) VALUES (14,'MMDEMO014',3,NULL,1,NULL,'12 Nguyễn Huệ, Quận 1, TP.HCM',10.776900,106.700900,'45 Lê Văn Việt, TP. Thủ Đức, TP.HCM',10.847300,106.781900,'Nguyễn Văn Nhận','0911000001',12.40,35,0,200000.00,0.00,0.00,200000.00,'PayOS',NULL,'Unpaid','Pending',NULL,'2026-11-10 14:58:03.335215',NULL,NULL,NULL,0,'2026-11-10 14:58:31.046387',NULL,NULL,NULL,NULL,NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `support_requests` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `full_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `contact` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `content` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'New',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `IX_support_requests_status` (`status`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `support_requests` (`id`, `full_name`, `contact`, `content`, `status`, `created_at`, `updated_at`) VALUES (1,'Trần Thị Mai','0912000001','Tôi muốn hỏi giá chuyển nhà trọn gói từ Quận 7 sang Thủ Đức.','New','2026-11-10 14:59:16.599856',NULL);
INSERT INTO `support_requests` (`id`, `full_name`, `contact`, `content`, `status`, `created_at`, `updated_at`) VALUES (2,'Lê Văn Bình','binh.le@example.com','ModMove có nhận chuyển văn phòng vào cuối tuần không?','Handled','2026-11-09 14:59:16.599856',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `full_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `phone_number` varchar(15) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `role` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `account_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Active',
  `email_verified` tinyint(1) NOT NULL DEFAULT '0',
  `failed_login_count` int NOT NULL DEFAULT '0',
  `locked_until` datetime(6) DEFAULT NULL,
  `fcm_token` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_users_email` (`email`),
  UNIQUE KEY `IX_users_phone_number` (`phone_number`)
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (1,'Admin ModMove','0900000001','admin@modmove.local','$2a$12$Nhp/oIHkc2W2kaHlHtR2AOzgOQg/tAtjqylwZ8dMT2sJXhDxiioDi','Administrator','Active',1,0,NULL,NULL,'2026-11-10 14:58:28.834456',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (2,'Dispatcher ModMove','0900000002','dispatcher@modmove.local','$2a$12$Nhp/oIHkc2W2kaHlHtR2AOzgOQg/tAtjqylwZ8dMT2sJXhDxiioDi','Dispatcher','Active',1,0,NULL,NULL,'2026-11-10 14:58:28.834456',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (3,'Khách hàng Demo','0900000003','customer@modmove.local','$2a$12$Nhp/oIHkc2W2kaHlHtR2AOzgOQg/tAtjqylwZ8dMT2sJXhDxiioDi','Customer','Active',1,0,NULL,NULL,'2026-11-10 14:58:28.834456',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (4,'Tài xế Demo','0900000004','driver@modmove.local','$2a$12$Nhp/oIHkc2W2kaHlHtR2AOzgOQg/tAtjqylwZ8dMT2sJXhDxiioDi','Driver','Active',1,0,NULL,NULL,'2026-11-10 14:58:28.834456',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (5,'Tài xế Thay thế','0900000005','driver2@modmove.local','$2a$12$Nhp/oIHkc2W2kaHlHtR2AOzgOQg/tAtjqylwZ8dMT2sJXhDxiioDi','Driver','Active',1,0,NULL,NULL,'2026-11-10 14:58:29.229163',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (6,'Tài xế Mất GPS','0900000006','driver3@modmove.local','$2a$12$Nhp/oIHkc2W2kaHlHtR2AOzgOQg/tAtjqylwZ8dMT2sJXhDxiioDi','Driver','Active',1,0,NULL,NULL,'2026-11-10 14:58:29.414749',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (7,'Khách chưa xác minh','0900000011','customer.unverified@modmove.local','$2a$12$QWPv7RuE2Vvvq1Ivji3w7e2libH3DUYasIIU5I0mmvnZ1.Ymp6iIO','Customer','Active',0,0,NULL,NULL,'2026-11-10 14:58:43.026466',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (8,'Khách đã xóa','0900000012','customer.inactive@modmove.local','$2a$12$QWPv7RuE2Vvvq1Ivji3w7e2libH3DUYasIIU5I0mmvnZ1.Ymp6iIO','Customer','Inactive',1,0,NULL,NULL,'2026-11-10 14:58:43.026466',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (9,'Khách bị khóa','0900000013','customer.locked@modmove.local','$2a$12$QWPv7RuE2Vvvq1Ivji3w7e2libH3DUYasIIU5I0mmvnZ1.Ymp6iIO','Customer','Locked',1,0,NULL,NULL,'2026-11-10 14:58:43.026466',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (10,'Tài xế chờ duyệt','0900000014','driver.pending@modmove.local','$2a$12$QWPv7RuE2Vvvq1Ivji3w7e2libH3DUYasIIU5I0mmvnZ1.Ymp6iIO','Driver','Active',1,0,NULL,NULL,'2026-11-10 14:58:43.026466',NULL);
INSERT INTO `users` (`id`, `full_name`, `phone_number`, `email`, `password_hash`, `role`, `account_status`, `email_verified`, `failed_login_count`, `locked_until`, `fcm_token`, `created_at`, `updated_at`) VALUES (11,'Tài xế bị từ chối','0900000015','driver.rejected@modmove.local','$2a$12$QWPv7RuE2Vvvq1Ivji3w7e2libH3DUYasIIU5I0mmvnZ1.Ymp6iIO','Driver','Active',1,0,NULL,NULL,'2026-11-10 14:58:43.026466',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `vehicle_types` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `max_load_weight_kg` decimal(8,2) NOT NULL,
  `cargo_bed_length_cm` int NOT NULL,
  `cargo_bed_width_cm` int NOT NULL,
  `cargo_bed_height_cm` int NOT NULL,
  `base_fare` decimal(15,2) NOT NULL,
  `price_per_km` decimal(15,2) NOT NULL,
  `minimum_fare` decimal(15,2) NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_vehicle_types_name` (`name`)
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `vehicle_types` (`id`, `name`, `max_load_weight_kg`, `cargo_bed_length_cm`, `cargo_bed_width_cm`, `cargo_bed_height_cm`, `base_fare`, `price_per_km`, `minimum_fare`, `is_active`, `created_at`, `updated_at`) VALUES (1,'Xe van 500kg',500.00,170,120,120,50000.00,10000.00,80000.00,1,'2026-11-10 14:58:28.117135',NULL);
INSERT INTO `vehicle_types` (`id`, `name`, `max_load_weight_kg`, `cargo_bed_length_cm`, `cargo_bed_width_cm`, `cargo_bed_height_cm`, `base_fare`, `price_per_km`, `minimum_fare`, `is_active`, `created_at`, `updated_at`) VALUES (2,'Xe tải 1 tấn',1000.00,300,160,160,80000.00,14000.00,150000.00,1,'2026-11-10 14:58:28.117135',NULL);
INSERT INTO `vehicle_types` (`id`, `name`, `max_load_weight_kg`, `cargo_bed_length_cm`, `cargo_bed_width_cm`, `cargo_bed_height_cm`, `base_fare`, `price_per_km`, `minimum_fare`, `is_active`, `created_at`, `updated_at`) VALUES (3,'Xe tải 1.5 tấn',1500.00,320,170,170,100000.00,17000.00,200000.00,1,'2026-11-10 14:58:28.117135',NULL);
INSERT INTO `vehicle_types` (`id`, `name`, `max_load_weight_kg`, `cargo_bed_length_cm`, `cargo_bed_width_cm`, `cargo_bed_height_cm`, `base_fare`, `price_per_km`, `minimum_fare`, `is_active`, `created_at`, `updated_at`) VALUES (4,'Xe tải 2 tấn',2000.00,400,180,180,130000.00,20000.00,260000.00,1,'2026-11-10 14:58:28.117135',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `vehicles` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `driver_profile_id` bigint NOT NULL,
  `vehicle_type_id` bigint NOT NULL,
  `license_plate` varchar(15) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `max_load_weight_kg` decimal(8,2) NOT NULL,
  `cargo_bed_dimensions` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `registration_cert_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `inspection_cert_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `vehicle_photo_url` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `vehicle_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Active',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_vehicles_driver_profile_id` (`driver_profile_id`),
  UNIQUE KEY `IX_vehicles_license_plate` (`license_plate`),
  KEY `IX_vehicles_vehicle_type_id` (`vehicle_type_id`),
  CONSTRAINT `FK_vehicles_driver_profiles_driver_profile_id` FOREIGN KEY (`driver_profile_id`) REFERENCES `driver_profiles` (`id`) ON DELETE CASCADE,
  CONSTRAINT `FK_vehicles_vehicle_types_vehicle_type_id` FOREIGN KEY (`vehicle_type_id`) REFERENCES `vehicle_types` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `vehicles` (`id`, `driver_profile_id`, `vehicle_type_id`, `license_plate`, `max_load_weight_kg`, `cargo_bed_dimensions`, `registration_cert_url`, `inspection_cert_url`, `vehicle_photo_url`, `vehicle_status`, `created_at`, `updated_at`) VALUES (1,1,1,'51C-123.45',500.00,'170x120x120','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','Active','2026-11-10 14:58:29.028257',NULL);
INSERT INTO `vehicles` (`id`, `driver_profile_id`, `vehicle_type_id`, `license_plate`, `max_load_weight_kg`, `cargo_bed_dimensions`, `registration_cert_url`, `inspection_cert_url`, `vehicle_photo_url`, `vehicle_status`, `created_at`, `updated_at`) VALUES (2,2,1,'51C-222.22',500.00,'170x120x120','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','Active','2026-11-10 14:58:29.308037',NULL);
INSERT INTO `vehicles` (`id`, `driver_profile_id`, `vehicle_type_id`, `license_plate`, `max_load_weight_kg`, `cargo_bed_dimensions`, `registration_cert_url`, `inspection_cert_url`, `vehicle_photo_url`, `vehicle_status`, `created_at`, `updated_at`) VALUES (3,3,1,'51C-333.33',500.00,'170x120x120','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','https://res.cloudinary.com/demo/image/upload/sample.jpg','Active','2026-11-10 14:58:29.463650',NULL);
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wallet_transactions` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `wallet_id` bigint NOT NULL,
  `order_id` bigint DEFAULT NULL,
  `transaction_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `amount` decimal(15,2) NOT NULL,
  `balance_after` decimal(15,2) NOT NULL,
  `transaction_status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `reference_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `processed_by` bigint DEFAULT NULL,
  `processed_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_wallet_transactions_order_id_transaction_type` (`order_id`,`transaction_type`),
  KEY `IX_wallet_transactions_processed_by` (`processed_by`),
  KEY `IX_wallet_transactions_wallet_id` (`wallet_id`),
  CONSTRAINT `FK_wallet_transactions_driver_wallets_wallet_id` FOREIGN KEY (`wallet_id`) REFERENCES `driver_wallets` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_wallet_transactions_shipping_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `shipping_orders` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_wallet_transactions_users_processed_by` FOREIGN KEY (`processed_by`) REFERENCES `users` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

INSERT INTO `wallet_transactions` (`id`, `wallet_id`, `order_id`, `transaction_type`, `amount`, `balance_after`, `transaction_status`, `reference_code`, `processed_by`, `processed_at`, `created_at`, `updated_at`) VALUES (1,1,NULL,'TopUpDeposit',500000.00,500000.00,'Completed',NULL,NULL,NULL,'2026-11-10 14:58:29.182129',NULL);
INSERT INTO `wallet_transactions` (`id`, `wallet_id`, `order_id`, `transaction_type`, `amount`, `balance_after`, `transaction_status`, `reference_code`, `processed_by`, `processed_at`, `created_at`, `updated_at`) VALUES (2,2,NULL,'TopUpDeposit',500000.00,500000.00,'Completed',NULL,NULL,NULL,'2026-11-10 14:58:29.381267',NULL);
INSERT INTO `wallet_transactions` (`id`, `wallet_id`, `order_id`, `transaction_type`, `amount`, `balance_after`, `transaction_status`, `reference_code`, `processed_by`, `processed_at`, `created_at`, `updated_at`) VALUES (3,3,NULL,'TopUpDeposit',500000.00,500000.00,'Completed',NULL,NULL,NULL,'2026-11-10 14:58:29.521315',NULL);
INSERT INTO `wallet_transactions` (`id`, `wallet_id`, `order_id`, `transaction_type`, `amount`, `balance_after`, `transaction_status`, `reference_code`, `processed_by`, `processed_at`, `created_at`, `updated_at`) VALUES (4,1,10,'TripEarning',189000.00,189000.00,'Completed',NULL,NULL,NULL,'2026-11-10 14:58:31.152448',NULL);
INSERT INTO `wallet_transactions` (`id`, `wallet_id`, `order_id`, `transaction_type`, `amount`, `balance_after`, `transaction_status`, `reference_code`, `processed_by`, `processed_at`, `created_at`, `updated_at`) VALUES (5,1,NULL,'WithdrawEarnings',100000.00,89000.00,'Pending',NULL,NULL,NULL,'2026-11-10 14:58:31.152448',NULL);
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

