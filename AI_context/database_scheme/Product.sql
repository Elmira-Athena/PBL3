-- =============================================
-- PHẦN 2: PRODUCT MASTER DATA (FIXED & OPTIMIZED)
-- =============================================

-- 1. Bảng Manufacturers
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Manufacturers]') AND type in (N'U'))
BEGIN
CREATE TABLE [Manufacturers] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [LogoUrl] nvarchar(500) NULL,
    [Website] nvarchar(255) NULL,
    [SupportEmail] varchar(100) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    [DeletedDate] datetime2 NULL,
    CONSTRAINT [PK_Manufacturers] PRIMARY KEY ([Id])
);
END
GO

-- 2. Bảng Categories
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Categories]') AND type in (N'U'))
BEGIN
CREATE TABLE [Categories] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Slug] varchar(150) NOT NULL,
    [ParentId] int NULL,
    [Level] int NOT NULL DEFAULT 0,
    [ImageUrl] nvarchar(500) NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [IsVisible] bit NOT NULL DEFAULT 1,
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    [DeletedDate] datetime2 NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id]),
    CONSTRAINT [UQ_Categories_Slug] UNIQUE ([Slug])
);
CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
END
GO

-- 3. Bảng Products (Đã thêm cột ManufacturerId & CategoryId)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Products]') AND type in (N'U'))
BEGIN
CREATE TABLE [Products] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(255) NOT NULL,
    [ShortDescription] nvarchar(500) NULL,
    [Description] nvarchar(max) NULL,
    [ManufacturerId] int NOT NULL, -- Đã thêm
    [CategoryId] int NOT NULL,     -- Đã thêm
    [Status] int NOT NULL DEFAULT 1,
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    [DeletedDate] datetime2 NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Products_Manufacturers] FOREIGN KEY ([ManufacturerId]) REFERENCES [Manufacturers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Products_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
);
END
GO

-- 4. Bảng ProductVariants
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductVariants]') AND type in (N'U'))
BEGIN
CREATE TABLE [ProductVariants] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ProductId] int NOT NULL,
    [SKU] varchar(50) NOT NULL,
    [VariantName] nvarchar(200) NOT NULL,
    [Slug] varchar(250) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [OriginalPrice] decimal(18,2) NULL,
    [StockQuantity] int NOT NULL DEFAULT 0,
    [WarrantyMonth] int NOT NULL DEFAULT 12,
    [Specifications] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    [DeletedDate] datetime2 NULL,
    CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductVariants_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_ProductVariants_SKU] UNIQUE ([SKU])
);
END
GO

-- 5. Bảng ProductAttributes
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductAttributes]') AND type in (N'U'))
BEGIN
CREATE TABLE [ProductAttributes] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [VariantId] int NOT NULL,
    [AttributeName] varchar(50) NOT NULL,
    [AttributeValue] nvarchar(100) NOT NULL,
    [IsFilterable] bit NOT NULL DEFAULT 1,
    CONSTRAINT [PK_ProductAttributes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductAttributes_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_ProductAttributes_Search] ON [ProductAttributes] ([AttributeName], [AttributeValue]);
END
GO

-- 6. Bảng ProductImages
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductImages]') AND type in (N'U'))
BEGIN
CREATE TABLE [ProductImages] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [VariantId] int NOT NULL,
    [ImageUrl] nvarchar(500) NOT NULL,
    [IsMain] bit NOT NULL DEFAULT 0,
    [SortOrder] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImages_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE CASCADE
);
END
GO

-- SEED DATA (Sửa lại logic biến)
-- Chạy riêng block này
INSERT INTO [Manufacturers] ([Name], [LogoUrl]) VALUES 
(N'Intel', 'intel.png'), (N'AMD', 'amd.png'), (N'NVIDIA', 'nvidia.png'), (N'ASUS', 'asus.png');

INSERT INTO [Categories] ([Name], [Slug], [ParentId], [Level]) VALUES
(N'Linh Kiện PC', 'linh-kien-pc', NULL, 0);

-- Lấy ID cha vừa tạo để insert con (Dùng subquery trực tiếp thay vì biến để tránh lỗi GO)
INSERT INTO [Categories] ([Name], [Slug], [ParentId], [Level]) VALUES
(N'CPU', 'cpu', (SELECT Id FROM Categories WHERE Slug = 'linh-kien-pc'), 1),
(N'VGA', 'vga', (SELECT Id FROM Categories WHERE Slug = 'linh-kien-pc'), 1),
(N'Mainboard', 'mainboard', (SELECT Id FROM Categories WHERE Slug = 'linh-kien-pc'), 1);
GO