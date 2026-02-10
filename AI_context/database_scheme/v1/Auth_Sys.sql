-- 1. Tạo Database (Nếu chưa có)
-- CREATE DATABASE [TechGearDB];
-- GO
-- USE [TechGearDB];
-- GO

-- =============================================
-- PHẦN 1: IDENTITY TABLES (Customized)
-- =============================================

-- 1. Bảng Users (Dùng GUID làm PK để bảo mật)
CREATE TABLE [AppUsers] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),

    -- Các cột chuẩn của Identity
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL DEFAULT 0,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL DEFAULT 0,
    [TwoFactorEnabled] bit NOT NULL DEFAULT 0,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL DEFAULT 0,
    [AccessFailedCount] int NOT NULL DEFAULT 0,

    -- Các cột Custom (Nghiệp vụ)
    [FullName] nvarchar(100) NOT NULL,
    [DateOfBirth] datetime2 NULL,
    [Gender] int NOT NULL DEFAULT 0, -- 0: Male, 1: Female, 2: Other
    [AvatarUrl] nvarchar(500) NULL,
    [IsActive] bit NOT NULL DEFAULT 1, -- 1: Active, 0: Banned
    [Type] int NOT NULL DEFAULT 2,     -- 0: Admin, 1: Employee, 2: Customer

-- Audit & Soft Delete
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    [DeletedDate] datetime2 NULL,

    CONSTRAINT [PK_AppUsers] PRIMARY KEY ([Id])
    );
GO

-- Index cho User để tìm kiếm nhanh
CREATE INDEX [IX_AppUsers_NormalizedUserName] ON [AppUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
CREATE INDEX [IX_AppUsers_NormalizedEmail] ON [AppUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;
GO

-- 2. Bảng Roles (Quyền hạn)
CREATE TABLE [AppRoles] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,

    -- Custom Columns
    [Description] nvarchar(250) NULL,
    [RoleCode] varchar(50) NOT NULL, -- VD: ADMIN, STAFF

    CONSTRAINT [PK_AppRoles] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_AppRoles_RoleCode] UNIQUE ([RoleCode]) -- Mã quyền không được trùng
    );
GO

CREATE INDEX [IX_AppRoles_NormalizedName] ON [AppRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

-- 3. Bảng UserRoles (Liên kết N-N User và Role)
CREATE TABLE [AppUserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,

     CONSTRAINT [PK_AppUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AppUserRoles_AppUsers] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AppUserRoles_AppRoles] FOREIGN KEY ([RoleId]) REFERENCES [AppRoles] ([Id]) ON DELETE CASCADE
    );
GO

-- 4. Các bảng phụ của Identity (Claims, Logins, Tokens - Cần thiết để Identity chạy ổn định)
CREATE TABLE [AppUserClaims] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AppUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AppUserClaims_AppUsers] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE
    );

CREATE TABLE [AppRoleClaims] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AppRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AppRoleClaims_AppRoles] FOREIGN KEY ([RoleId]) REFERENCES [AppRoles] ([Id]) ON DELETE CASCADE
    );

CREATE TABLE [AppUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AppUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AppUserLogins_AppUsers] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE
    );

CREATE TABLE [AppUserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AppUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AppUserTokens_AppUsers] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE
    );
GO

-- =============================================
-- PHẦN 2: EXTENSION TABLES (Nghiệp vụ)
-- =============================================

-- 5. Bảng UserAddresses (Sổ địa chỉ)
CREATE TABLE [UserAddresses] (
    [Id] int IDENTITY(1,1) NOT NULL, -- Dùng INT cho dữ liệu nghiệp vụ
    [UserId] uniqueidentifier NOT NULL,

    [ReceiverName] nvarchar(100) NOT NULL,
    [PhoneNumber] varchar(15) NOT NULL,
    [AddressLine] nvarchar(255) NOT NULL,
    [Ward] nvarchar(100) NOT NULL,     -- Phường/Xã
    [District] nvarchar(100) NOT NULL, -- Quận/Huyện
    [City] nvarchar(100) NOT NULL,     -- Tỉnh/TP

    [IsDefault] bit NOT NULL DEFAULT 0,

    -- Audit Columns
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    [DeletedDate] datetime2 NULL,

    CONSTRAINT [PK_UserAddresses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserAddresses_AppUsers] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE
    );
GO

-- 6. Bảng RefreshTokens (Bảo mật JWT)
CREATE TABLE [RefreshTokens] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,

    [Token] nvarchar(max) NOT NULL,
    [JwtId] nvarchar(100) NOT NULL, -- JTI id của Access Token
    [IsUsed] bit NOT NULL DEFAULT 0,
    [IsRevoked] bit NOT NULL DEFAULT 0,
    [ExpiryDate] datetime2 NOT NULL,

    -- Audit Columns
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    [DeletedDate] datetime2 NULL,

    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_AppUsers] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE
    );
GO

-- =============================================
-- PHẦN 3: SEED DATA (Dữ liệu mẫu)
-- =============================================

-- Insert Roles (Admin, Employee, Customer)
INSERT INTO [AppRoles] ([Id], [Name], [NormalizedName], [RoleCode], [Description])
VALUES 
(NEWID(), 'Admin', 'ADMIN', 'ADMIN', N'Quản trị viên hệ thống'),
(NEWID(), 'Employee', 'EMPLOYEE', 'STAFF', N'Nhân viên bán hàng/Kho'),
(NEWID(), 'Customer', 'CUSTOMER', 'CUST', N'Khách hàng mua sắm');
GO

-- Insert 1 Super Admin (Mật khẩu mẫu: Admin@123 - Hash bên dưới chỉ là ví dụ tượng trưng)
-- Lưu ý: Khi chạy code thật, Identity sẽ tự hash lại password này. Đây chỉ để demo cấu trúc.
DECLARE @AdminId uniqueidentifier = NEWID();
DECLARE @RoleId uniqueidentifier = (SELECT TOP 1 Id FROM AppRoles WHERE Name = 'Admin');

INSERT INTO [AppUsers]
([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [FullName], [Type], [IsActive])
VALUES
    (@AdminId, 'admin', 'ADMIN', 'admin@techgear.com', 'ADMIN@TECHGEAR.COM', 1,
    'AQAAAAIAAYagAAAAELnP...', -- Hash của Admin@123 (Ví dụ)
    NEWID(), N'Super Admin', 0, 1);

-- Gán quyền Admin cho User vừa tạo
INSERT INTO [AppUserRoles] ([UserId], [RoleId]) VALUES (@AdminId, @RoleId);
GO