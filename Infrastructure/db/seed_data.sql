-- =============================================
-- SQL SEED DATA FOR HUSHSTORE SYSTEM (VERIFIED HASH)
-- =============================================

USE [HushStoreDb];
GO

-- 1. SEED ROLES
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '29837492-3847-4837-2938-472938472938';
DECLARE @EmployeeRoleId UNIQUEIDENTIFIER = '39485729-3847-4837-2938-472938472939';
DECLARE @CustomerRoleId UNIQUEIDENTIFIER = '49586730-3847-4837-2938-472938472940';
DECLARE @TechnicianRoleId UNIQUEIDENTIFIER = '59687841-3847-4837-2938-472938472941';

IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleCode = 'ADMIN')
    INSERT INTO AppRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, RoleCode)
    VALUES (@AdminRoleId, 'Admin', 'ADMIN', NEWID(), 'Administrator with full access', 'ADMIN');
SET @AdminRoleId = (SELECT TOP 1 Id FROM AppRoles WHERE RoleCode = 'ADMIN');

IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleCode = 'EMPLOYEE')
    INSERT INTO AppRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, RoleCode)
    VALUES (@EmployeeRoleId, 'Employee', 'EMPLOYEE', NEWID(), 'Staff with POS and Warehouse access', 'EMPLOYEE');
SET @EmployeeRoleId = (SELECT TOP 1 Id FROM AppRoles WHERE RoleCode = 'EMPLOYEE');

IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleCode = 'CUSTOMER')
    INSERT INTO AppRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, RoleCode)
    VALUES (@CustomerRoleId, 'Customer', 'CUSTOMER', NEWID(), 'Regular customer', 'CUSTOMER');
SET @CustomerRoleId = (SELECT TOP 1 Id FROM AppRoles WHERE RoleCode = 'CUSTOMER');

-- Technician (KTV): role nay TRUOC DAY chi duoc tao boi khoi seed trong Program.cs.
-- Khoi do khong co try/catch va chay o top-level statement, nen hai ECS task
-- cold-start cung luc (tuc dung luc deploy) se co mot task vi pham unique index
-- RoleNameIndex + IX_AppRoles_RoleCode => exception chua bat => process exit != 0
-- => TASK CHET LUC BOOT. Ngoai ra no lam startup phu thuoc vao viec DB dang song.
--
-- THU TU BAT BUOC: dong nay phai chay TRUOC khi xoa khoi seed trong Program.cs.
-- Dao thu tu thi EmployeeService.AddToRoleAsync(user, "Technician") vo hieu IM LANG.
--
-- NormalizedName phai ghi tay 'TECHNICIAN': RoleManager.CreateAsync tu sinh no qua
-- UpperInvariantLookupNormalizer, INSERT tay thi khong. Thieu no thi
-- FindByNameAsync/RoleExistsAsync KHONG BAO GIO tim thay role nay.
IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleCode = 'KTV')
    INSERT INTO AppRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, RoleCode)
    VALUES (@TechnicianRoleId, 'Technician', 'TECHNICIAN', NEWID(), 'Ky thuat vien sua chua', 'KTV');
SET @TechnicianRoleId = (SELECT TOP 1 Id FROM AppRoles WHERE RoleCode = 'KTV');

-- 2. SEED ADMIN ACCOUNT (Password: Admin@123)
-- Verified Hash generated via Microsoft.AspNetCore.Identity.PasswordHasher
DECLARE @AdminUserId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @PasswordHash NVARCHAR(MAX) = 'AQAAAAIAAYagAAAAEAQ7lGtyVXM7GIF0g6h3X8PIZITSayN4jcoVXWU0uGhMQEFHaSIteP4GKjx9lNwLhQ==';

IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Id = @AdminUserId)
BEGIN
    INSERT INTO AppUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, IsActive, Type, CreatedDate, IsDeleted)
    VALUES 
    (@AdminUserId, 'admin', 'ADMIN', 'admin@hushstore.com', 'ADMIN@HUSHSTORE.COM', 1, @PasswordHash, NEWID(), NEWID(), '0123456789', 1, 0, 1, 0, 1, 0, GETUTCDATE(), 0);

    INSERT INTO UserProfiles (UserId, FullName, Gender, DateOfBirth, Address, City)
    VALUES (@AdminUserId, 'HushStore Administrator', 0, '1990-01-01', '123 Nguyen Van Linh', 'Da Nang');

    INSERT INTO AppUserRoles (UserId, RoleId) VALUES (@AdminUserId, @AdminRoleId);
END
ELSE
BEGIN
    UPDATE AppUsers SET PasswordHash = @PasswordHash WHERE Id = @AdminUserId;
END



-- Chot kiem: phai co du 4 role. Neu thieu, seeder phai that bai ON AO
-- chu khong duoc exit 0 roi de he thong chay voi role bi thieu.
DECLARE @RoleCount INT = (SELECT COUNT(*) FROM AppRoles WHERE RoleCode IN ('ADMIN','EMPLOYEE','CUSTOMER','KTV'));
IF @RoleCount <> 4
BEGIN
    DECLARE @msg NVARCHAR(200) = N'SEED THAT BAI: mong doi 4 role, tim thay ' + CAST(@RoleCount AS NVARCHAR(10));
    THROW 50001, @msg, 1;
END

PRINT 'Seed data with verified hash completed successfully. AppRoles = 4.';
GO
