-- =============================================
-- SQL SEED DATA FOR HUSHSTORE SYSTEM (VERIFIED HASH)
-- =============================================

USE [HushStoreDb];
GO

-- 1. SEED ROLES
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '29837492-3847-4837-2938-472938472938';
DECLARE @EmployeeRoleId UNIQUEIDENTIFIER = '39485729-3847-4837-2938-472938472939';
DECLARE @CustomerRoleId UNIQUEIDENTIFIER = '49586730-3847-4837-2938-472938472940';

IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE Id = @AdminRoleId)
BEGIN
    INSERT INTO AppRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, RoleCode)
    VALUES 
    (@AdminRoleId, 'Admin', 'ADMIN', NEWID(), 'Administrator with full access', 'ADMIN'),
    (@EmployeeRoleId, 'Employee', 'EMPLOYEE', NEWID(), 'Staff with POS and Warehouse access', 'EMPLOYEE'),
    (@CustomerRoleId, 'Customer', 'CUSTOMER', NEWID(), 'Regular customer', 'CUSTOMER');
END

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

-- 3. SEED TEST CUSTOMER
DECLARE @CustomerUserId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Id = @CustomerUserId)
BEGIN
    INSERT INTO AppUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, IsActive, Type, CreatedDate, IsDeleted)
    VALUES 
    (@CustomerUserId, 'customer1', 'CUSTOMER1', 'customer1@gmail.com', 'CUSTOMER1@GMAIL.COM', 1, NEWID(), NEWID(), '0987654321', 1, 0, 1, 0, 1, 2, GETUTCDATE(), 0);

    INSERT INTO UserProfiles (UserId, FullName, Gender, DateOfBirth, Address, City)
    VALUES (@CustomerUserId, 'Nguyễn Văn A', 0, '1995-05-10', '456 Le Duan', 'Da Nang');

    INSERT INTO AppUserRoles (UserId, RoleId) VALUES (@CustomerUserId, @CustomerRoleId);
END

-- 4. SEED PRODUCT DATA
DECLARE @ManId INT, @CatId INT, @ProdId INT, @VarId INT;
IF NOT EXISTS (SELECT 1 FROM Categories WHERE Name = N'Linh kiện máy tính')
BEGIN
    INSERT INTO Categories (Name, Slug, Level, CreatedDate, IsDeleted, SortOrder, IsVisible)
    VALUES (N'Linh kiện máy tính', 'linh-kien-may-tinh', 0, GETUTCDATE(), 0, 1, 1);
END
SET @CatId = (SELECT TOP 1 Id FROM Categories WHERE Name = N'Linh kiện máy tính');

IF NOT EXISTS (SELECT 1 FROM Manufacturers WHERE Name = 'ASUS')
BEGIN
    INSERT INTO Manufacturers (Name, LogoUrl, Website, CreatedDate, IsDeleted)
    VALUES ('ASUS', 'https://via.placeholder.com/100', 'https://www.asus.com', GETUTCDATE(), 0);
END
SET @ManId = (SELECT TOP 1 Id FROM Manufacturers WHERE Name = 'ASUS');

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = N'Laptop ASUS ExpertBook')
BEGIN
    INSERT INTO Products (Name, ShortDescription, Description, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted)
    VALUES (N'Laptop ASUS ExpertBook', N'Dòng laptop doanh nhân siêu bền nhẹ', N'Chi tiết sản phẩm Laptop ASUS ExpertBook B9...', @ManId, @CatId, 1, GETUTCDATE(), 0);
END
SET @ProdId = (SELECT TOP 1 Id FROM Products WHERE Name = N'Laptop ASUS ExpertBook');

IF NOT EXISTS (SELECT 1 FROM ProductVariants WHERE SKU = 'ASUS-EB-B9-V1')
BEGIN
    INSERT INTO ProductVariants (ProductId, SKU, VariantName, Slug, Price, OriginalPrice, WarrantyMonth, StockQuantity, CreatedDate, IsDeleted, Specifications)
    VALUES (@ProdId, 'ASUS-EB-B9-V1', N'ExpertBook B9 - i7 / 16GB / 512GB', 'expertbook-b9-i7-16-512', 36000000, 38000000, 24, 10, GETUTCDATE(), 0, '{"CPU": "Core i7", "RAM": "16GB", "SSD": "512GB"}');
END
SET @VarId = (SELECT TOP 1 Id FROM ProductVariants WHERE SKU = 'ASUS-EB-B9-V1');

-- 5. SEED INVENTORY
DECLARE @SuppId INT, @RecId INT;
IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Name = 'ASUS Vietnam')
BEGIN
    INSERT INTO Suppliers (Name, ContactPerson, PhoneNumber, Email, Address, CreatedDate, IsDeleted)
    VALUES ('ASUS Vietnam', 'Mr. Minh', '0281234567', 'info@asus.vn', 'Ho Chi Minh City', GETUTCDATE(), 0);
END
SET @SuppId = (SELECT TOP 1 Id FROM Suppliers WHERE Name = 'ASUS Vietnam');

IF NOT EXISTS (SELECT 1 FROM ImportReceipts WHERE ReceiptCode = 'IMP20260407')
BEGIN
    INSERT INTO ImportReceipts (ReceiptCode, SupplierId, EmployeeId, ImportDate, TotalAmount, IsDeleted)
    VALUES ('IMP20260407', @SuppId, @AdminUserId, GETUTCDATE(), 360000000, 0);
END
SET @RecId = (SELECT TOP 1 Id FROM ImportReceipts WHERE ReceiptCode = 'IMP20260407');

IF NOT EXISTS (SELECT 1 FROM ProductSerials WHERE SerialNumber = 'LT001')
BEGIN
    INSERT INTO ProductSerials (SerialNumber, VariantId, ImportReceiptId, Status, CreatedDate)
    VALUES 
    ('LT001', @VarId, @RecId, 0, GETUTCDATE()),
    ('LT002', @VarId, @RecId, 0, GETUTCDATE()),
    ('LT003', @VarId, @RecId, 0, GETUTCDATE());
END

-- 6. SEED VOUCHER
IF NOT EXISTS (SELECT 1 FROM Vouchers WHERE Code = 'HUSH10')
BEGIN
    INSERT INTO Vouchers (Code, Name, DiscountType, DiscountValue, MinOrderValue, MaxDiscountAmount, StartDate, EndDate, Quantity, UsedCount, IsActive)
    VALUES ('HUSH10', N'Giảm giá khai trương', 1, 10, 1000000, 500000, '2026-01-01', '2026-12-31', 100, 0, 1);
END

PRINT 'Seed data with verified hash completed successfully.';
GO
