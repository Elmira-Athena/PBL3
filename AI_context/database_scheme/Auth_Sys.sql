-- =============================================
-- MODULE 1: AUTHENTICATION & SYSTEM (FINAL MERGE)
-- =============================================

-- 1. Bảng Users (Tất cả trong một: Đăng nhập + Hồ sơ + Địa chỉ)
CREATE TABLE [AppUsers] (
    -- A. ĐỊNH DANH (IDENTITY CORE)
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,

    -- Các cột Identity bắt buộc (để Default để tương thích Framework)
    [EmailConfirmed] bit NOT NULL DEFAULT 0,
    [PhoneNumberConfirmed] bit NOT NULL DEFAULT 0,
    [TwoFactorEnabled] bit NOT NULL DEFAULT 0,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL DEFAULT 0,
    [AccessFailedCount] int NOT NULL DEFAULT 0,

    -- B. THÔNG TIN CÁ NHÂN (PROFILE MERGED)
    [FullName] nvarchar(100) NOT NULL,
    [Gender] int NOT NULL DEFAULT 0,   -- 0: Male, 1: Female, 2: Other
    [DateOfBirth] datetime2 NULL,
    [AvatarUrl] nvarchar(500) NULL,

    -- C. ĐỊA CHỈ MẶC ĐỊNH (DEFAULT ADDRESS MERGED)
    [Address] nvarchar(255) NULL, -- Số nhà, tên đường, phường/xã...
    [City] nvarchar(100) NULL,    -- Tỉnh/Thành phố

-- D. QUẢN TRỊ & TRẠNG THÁI
    [IsActive] bit NOT NULL DEFAULT 1, -- 1: Hoạt động, 0: Bị khóa
    [Type] int NOT NULL DEFAULT 2,     -- 0: Admin, 1: Employee, 2: Customer

-- E. AUDIT (LỊCH SỬ)
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [IsDeleted] bit NOT NULL DEFAULT 0,

    CONSTRAINT [PK_AppUsers] PRIMARY KEY ([Id])
    );
GO

-- Index tìm kiếm nhanh
CREATE INDEX [IX_AppUsers_NormalizedUserName] ON [AppUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
CREATE INDEX [IX_AppUsers_NormalizedEmail] ON [AppUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;
GO

-- 2. Bảng Roles (Vai trò cố định)
CREATE TABLE [AppRoles] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),

    [Name] nvarchar(256) NULL, -- Tên hiển thị (Admin)
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,

    -- Custom
    [Description] nvarchar(250) NULL,
    [RoleCode] varchar(50) NOT NULL, -- Mã dùng trong code (ADMIN)

    CONSTRAINT [PK_AppRoles] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_AppRoles_RoleCode] UNIQUE ([RoleCode])
    );
GO

-- 3. Bảng AppUserRoles (Liên kết User - Role)
CREATE TABLE [AppUserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,

     CONSTRAINT [PK_AppUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AppUserRoles_AppUsers] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AppUserRoles_AppRoles] FOREIGN KEY ([RoleId]) REFERENCES [AppRoles] ([Id]) ON DELETE CASCADE
    );
GO

-- 4. Bảng RefreshTokens (Quản lý phiên đăng nhập App/Web)
CREATE TABLE [RefreshTokens] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,

    [Token] nvarchar(max) NOT NULL,
    [JwtId] nvarchar(100) NOT NULL,
    [IsUsed] bit NOT NULL DEFAULT 0,
    [IsRevoked] bit NOT NULL DEFAULT 0, -- Dùng để đăng xuất từ xa
    [ExpiryDate] datetime2 NOT NULL,

    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [IsDeleted] bit NOT NULL DEFAULT 0,

    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_AppUsers] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE
    );
GO

-- =============================================
-- SEED DATA (DỮ LIỆU MẪU)
-- =============================================

-- 1. Tạo 3 Role cứng
INSERT INTO [AppRoles] ([Id], [Name], [NormalizedName], [RoleCode], [Description]) VALUES 
(NEWID(), 'Admin', 'ADMIN', 'ADMIN', N'Quản trị viên hệ thống'),
(NEWID(), 'Employee', 'EMPLOYEE', 'STAFF', N'Nhân viên bán hàng/Kho'),
(NEWID(), 'Customer', 'CUSTOMER', 'CUST', N'Khách hàng mua sắm');

-- 2. Tạo 1 Super Admin mặc định
DECLARE @AdminId uniqueidentifier = NEWID();
DECLARE @AdminRoleId uniqueidentifier = (SELECT TOP 1 Id FROM AppRoles WHERE RoleCode = 'ADMIN');

INSERT INTO [AppUsers]
([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [PasswordHash], [SecurityStamp], [FullName], [Type], [IsActive])
VALUES
    (@AdminId, 'admin', 'ADMIN', 'admin@techgear.com', 'ADMIN@TECHGEAR.COM',
    'AQAAAAIAAYagAAAAELnP...', -- Mật khẩu tượng trưng, Identity sẽ hash lại khi chạy code thật
    NEWID(), N'Super Admin', 0, 1);

-- 3. Gán quyền Admin
INSERT INTO [AppUserRoles] ([UserId], [RoleId]) VALUES (@AdminId, @AdminRoleId);
GO