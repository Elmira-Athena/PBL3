-- =============================================
-- PHẦN 2: PRODUCT MASTER DATA (MERGE & ALL SERIAL)
-- =============================================

-- 1. Bảng Manufacturers (Hãng sản xuất)
CREATE TABLE [Manufacturers] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [LogoUrl] nvarchar(500) NULL,
    [Website] nvarchar(255) NULL,
    [SupportEmail] varchar(100) NULL,

    -- Audit
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] nvarchar(100) NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    [DeletedDate] datetime2 NULL,

    CONSTRAINT [PK_Manufacturers] PRIMARY KEY ([Id])
    );
GO

-- 2. Bảng Categories (Danh mục đa cấp)
CREATE TABLE [Categories] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Slug] varchar(150) NOT NULL,
    [ParentId] int NULL, -- Self-Reference (Đệ quy)
    [Level] int NOT NULL DEFAULT 0, -- 0: Root, 1: Sub
    [ImageUrl] nvarchar(500) NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [IsVisible] bit NOT NULL DEFAULT 1,

    -- Audit
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
GO

CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
GO

-- 3. Bảng Products (Sản phẩm chung / Dòng máy)
-- VD: Laptop ASUS ROG Strix G15 (Chưa phải là cấu hình cụ thể)
CREATE TABLE [Products] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(255) NOT NULL,
    [ShortDescription] nvarchar(500) NULL,
    [Description] nvarchar(max) NULL, -- HTML mô tả chi tiết

    [ManufacturerId] int NOT NULL,
    [CategoryId] int NOT NULL,

    [Status] int NOT NULL DEFAULT 1, -- 1: Kinh doanh, 0: Ngừng kinh doanh

-- Audit
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
GO

-- 4. Bảng ProductVariants (SKU - Đơn vị bán hàng thực tế)
-- VD: G15-513RC (Cấu hình RAM 16GB)
-- LƯU Ý: Đã xóa cột IsSerialManaged vì mặc định là TRUE.
CREATE TABLE [ProductVariants] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ProductId] int NOT NULL,

    [SKU] varchar(50) NOT NULL, -- Mã định danh duy nhất (Barcode)
    [VariantName] nvarchar(200) NOT NULL, -- Tên hiển thị đầy đủ (VD: Màu Đen - 16GB)
    [Slug] varchar(250) NOT NULL, -- URL riêng cho biến thể

    [Price] decimal(18,2) NOT NULL, -- Giá bán
    [OriginalPrice] decimal(18,2) NULL, -- Giá gốc (để gạch ngang)

-- CACHED STOCK: Cột này chỉ dùng để hiển thị nhanh. 
-- Dữ liệu gốc nằm ở bảng ProductSerials (Module 3).
    [StockQuantity] int NOT NULL DEFAULT 0,

    [WarrantyMonth] int NOT NULL DEFAULT 12, -- Số tháng bảo hành

    [Specifications] nvarchar(max) NULL, -- JSON cấu hình chi tiết

-- Audit
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
GO

-- 5. Bảng ProductAttributes (Thông số kỹ thuật dùng cho Lọc & Build PC)
-- VD: VariantId = 10, Name = "Socket", Value = "LGA1700"
CREATE TABLE [ProductAttributes] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [VariantId] int NOT NULL,

    [AttributeName] varchar(50) NOT NULL, -- Key: Socket, RamType, TDP
    [AttributeValue] nvarchar(100) NOT NULL, -- Value: LGA1700, DDR4

    [IsFilterable] bit NOT NULL DEFAULT 1,

    CONSTRAINT [PK_ProductAttributes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductAttributes_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE CASCADE
    );
GO

CREATE INDEX [IX_ProductAttributes_Search] ON [ProductAttributes] ([AttributeName], [AttributeValue]);
GO

-- 6. Bảng ProductImages (Thư viện ảnh)
CREATE TABLE [ProductImages] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [VariantId] int NOT NULL,

    [ImageUrl] nvarchar(500) NOT NULL,
    [IsMain] bit NOT NULL DEFAULT 0,
    [SortOrder] int NOT NULL DEFAULT 0,

    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImages_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE CASCADE
    );
GO

-- =============================================
-- SEED DATA (Dữ liệu mẫu)
-- =============================================

-- Hãng
INSERT INTO [Manufacturers] ([Name], [LogoUrl]) VALUES 
(N'Intel', 'intel.png'), (N'AMD', 'amd.png'), (N'NVIDIA', 'nvidia.png'), (N'ASUS', 'asus.png');

-- Danh mục
INSERT INTO [Categories] ([Name], [Slug], [ParentId], [Level]) VALUES
    (N'Linh Kiện PC', 'linh-kien-pc', NULL, 0);

DECLARE @LinhKienId int = (SELECT Id FROM Categories WHERE Slug = 'linh-kien-pc');

INSERT INTO [Categories] ([Name], [Slug], [ParentId], [Level]) VALUES
    (N'CPU', 'cpu', @LinhKienId, 1),
    (N'VGA', 'vga', @LinhKienId, 1),
    (N'Mainboard', 'mainboard', @LinhKienId, 1);
GO