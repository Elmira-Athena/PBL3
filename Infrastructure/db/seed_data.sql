-- =============================================
-- SEED DATA CHO HUSHSTORE (PostgreSQL 17) — VERIFIED HASH
-- =============================================
-- Đợt 7: chuyển từ T-SQL sang PL/pgSQL. Bốn nhóm khác biệt, ghi ra để lần sau
-- không ai "sửa lại cho giống bản cũ":
--   1. Không có `USE [db]` và không có `GO` — psql không có batch separator; database
--      đã được chọn bằng tham số `-d` của psql.
--   2. Định danh dùng "nháy kép", không phải [ngoặc vuông]. Bỏ nháy là PostgreSQL hạ
--      chữ thường, và bảng do EF tạo giữ nguyên hoa/thường ⇒ không tìm thấy.
--   3. `DECLARE @var` → khối `DO $$ DECLARE … BEGIN … END $$;`. Biến không có tiền tố @.
--   4. Cột `bit` của SQL Server là `boolean` ở đây: phải là `true`/`false`, KHÔNG phải
--      `1`/`0`. PostgreSQL không cast ngầm int→bool trong INSERT.
--
-- Không cần `setval` trong file này: mọi khoá chính ở đây là `uuid` do file tự cấp,
-- không phải cột identity. (seed_product_data.sql thì CÓ cần — xem cuối file đó.)

DO $$
DECLARE
    admin_role_id      uuid := '29837492-3847-4837-2938-472938472938';
    employee_role_id   uuid := '39485729-3847-4837-2938-472938472939';
    customer_role_id   uuid := '49586730-3847-4837-2938-472938472940';
    technician_role_id uuid := '59687841-3847-4837-2938-472938472941';
    admin_user_id      uuid := '11111111-1111-1111-1111-111111111111';
    -- Hash sinh bằng Microsoft.AspNetCore.Identity.PasswordHasher (mật khẩu: Admin@123)
    password_hash      text := 'AQAAAAIAAYagAAAAEAQ7lGtyVXM7GIF0g6h3X8PIZITSayN4jcoVXWU0uGhMQEFHaSIteP4GKjx9lNwLhQ==';
    role_count         int;
BEGIN
    -- ── 1. ROLE ──────────────────────────────────────────────────────────────
    INSERT INTO "AppRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "RoleCode")
    VALUES (admin_role_id, 'Admin', 'ADMIN', gen_random_uuid()::text, 'Administrator with full access', 'ADMIN')
    ON CONFLICT DO NOTHING;

    INSERT INTO "AppRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "RoleCode")
    VALUES (employee_role_id, 'Employee', 'EMPLOYEE', gen_random_uuid()::text, 'Staff with POS and Warehouse access', 'EMPLOYEE')
    ON CONFLICT DO NOTHING;

    INSERT INTO "AppRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "RoleCode")
    VALUES (customer_role_id, 'Customer', 'CUSTOMER', gen_random_uuid()::text, 'Regular customer', 'CUSTOMER')
    ON CONFLICT DO NOTHING;

    -- Technician (KTV): role này TRƯỚC ĐÂY chỉ được tạo bởi khối seed trong Program.cs.
    -- Khối đó không có try/catch và chạy ở top-level statement, nên hai ECS task
    -- cold-start cùng lúc (tức đúng lúc deploy) sẽ có một task vi phạm unique index
    -- => exception chưa bắt => process exit != 0 => TASK CHẾT LÚC BOOT. Ngoài ra nó làm
    -- startup phụ thuộc vào việc DB đang sống.
    --
    -- THỨ TỰ BẮT BUỘC: dòng này phải chạy TRƯỚC khi xoá khối seed trong Program.cs.
    -- Đảo thứ tự thì EmployeeService.AddToRoleAsync(user, "Technician") vô hiệu IM LẶNG.
    --
    -- NormalizedName phải ghi tay 'TECHNICIAN': RoleManager.CreateAsync tự sinh nó qua
    -- UpperInvariantLookupNormalizer, INSERT tay thì không. Thiếu nó thì
    -- FindByNameAsync/RoleExistsAsync KHÔNG BAO GIỜ tìm thấy role này.
    INSERT INTO "AppRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "RoleCode")
    VALUES (technician_role_id, 'Technician', 'TECHNICIAN', gen_random_uuid()::text, 'Ky thuat vien sua chua', 'KTV')
    ON CONFLICT DO NOTHING;

    -- Đọc lại Id thật: nếu role đã tồn tại từ lần seed trước thì Id trong DB mới là Id
    -- đúng, không phải hằng ở trên. Bản T-SQL cũ làm việc này bằng `SET @x = (SELECT TOP 1 …)`.
    SELECT "Id" INTO admin_role_id FROM "AppRoles" WHERE "RoleCode" = 'ADMIN' LIMIT 1;

    -- ── 2. TÀI KHOẢN ADMIN ───────────────────────────────────────────────────
    IF NOT EXISTS (SELECT 1 FROM "AppUsers" WHERE "Id" = admin_user_id) THEN
        INSERT INTO "AppUsers" (
            "Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
            "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed",
            "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "IsActive", "Type",
            "CreatedDate", "IsDeleted")
        VALUES (
            admin_user_id, 'admin', 'ADMIN', 'admin@hushstore.com', 'ADMIN@HUSHSTORE.COM', true,
            password_hash, gen_random_uuid()::text, gen_random_uuid()::text, '0123456789', true,
            false, true, 0, true, 0,
            now() AT TIME ZONE 'utc', false);

        INSERT INTO "UserProfiles" ("UserId", "FullName", "Gender", "DateOfBirth", "Address", "City")
        VALUES (admin_user_id, 'HushStore Administrator', 0, DATE '1990-01-01', '123 Nguyen Van Linh', 'Da Nang');

        INSERT INTO "AppUserRoles" ("UserId", "RoleId") VALUES (admin_user_id, admin_role_id);
    ELSE
        UPDATE "AppUsers" SET "PasswordHash" = password_hash WHERE "Id" = admin_user_id;
    END IF;

    -- ── 3. CHỐT KIỂM ─────────────────────────────────────────────────────────
    -- Phải có đủ 4 role. Nếu thiếu, seeder phải thất bại ỒN ÀO chứ không được exit 0
    -- rồi để hệ thống chạy với role bị thiếu.
    SELECT COUNT(*) INTO role_count
    FROM "AppRoles" WHERE "RoleCode" IN ('ADMIN', 'EMPLOYEE', 'CUSTOMER', 'KTV');

    IF role_count <> 4 THEN
        RAISE EXCEPTION 'SEED THAT BAI: mong doi 4 role, tim thay %', role_count;
    END IF;

    RAISE NOTICE 'Seed data with verified hash completed successfully. AppRoles = %.', role_count;
END $$;
