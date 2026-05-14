-- =============================================
-- SQL SEED DATA FOR HUSHSTORE SYSTEM (VERIFIED HASH)
-- =============================================

USE [HushStoreDb];
GO

-- 1. SEED ROLES
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '29837492-3847-4837-2938-472938472938';
DECLARE @EmployeeRoleId UNIQUEIDENTIFIER = '39485729-3847-4837-2938-472938472939';
DECLARE @CustomerRoleId UNIQUEIDENTIFIER = '49586730-3847-4837-2938-472938472940';

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



PRINT 'Seed data with verified hash completed successfully.';
GO
