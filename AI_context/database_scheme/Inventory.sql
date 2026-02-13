-- =============================================
-- PHẦN 3: INVENTORY MANAGEMENT (FIXED & OPTIMIZED)
-- =============================================

-- 1. Bảng Suppliers
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Suppliers]') AND type in (N'U'))
BEGIN
CREATE TABLE [Suppliers] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [ContactPerson] nvarchar(100) NULL,
    [PhoneNumber] varchar(20) NOT NULL,
    [Email] varchar(100) NULL,
    [Address] nvarchar(255) NULL,
    [TaxCode] varchar(20) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [IsDeleted] bit NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
);
END
GO

-- 2. Bảng ImportReceipts
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ImportReceipts]') AND type in (N'U'))
BEGIN
CREATE TABLE [ImportReceipts] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ReceiptCode] varchar(20) NOT NULL,
    [SupplierId] int NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL, -- Sẽ link với bảng User sau
    [ImportDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [TotalAmount] decimal(18,2) NOT NULL DEFAULT 0,
    [Note] nvarchar(500) NULL,
    [IsDeleted] bit NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ImportReceipts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ImportReceipts_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]),
    CONSTRAINT [UQ_ImportReceipts_Code] UNIQUE ([ReceiptCode])
);
END
GO

-- 3. Bảng ImportReceiptDetails
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ImportReceiptDetails]') AND type in (N'U'))
BEGIN
CREATE TABLE [ImportReceiptDetails] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ReceiptId] int NOT NULL,
    [VariantId] int NOT NULL, -- Link sang Module Product
    [Quantity] int NOT NULL,
    [ImportPrice] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_ImportReceiptDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ImportReceiptDetails_Receipts] FOREIGN KEY ([ReceiptId]) REFERENCES [ImportReceipts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ImportReceiptDetails_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id])
);
END
GO

-- 4. Bảng ProductSerials (Đã sửa logic Unique)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductSerials]') AND type in (N'U'))
BEGIN
CREATE TABLE [ProductSerials] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [SerialNumber] varchar(100) NOT NULL,
    [VariantId] int NOT NULL,
    [ImportReceiptId] int NOT NULL,
    -- 0: Available, 1: Reserved, 2: Sold, 3: Defective, 4: Returned
    [Status] int NOT NULL DEFAULT 0,
    [OrderId] int NULL, -- Sẽ link với Order sau
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [SoldDate] datetime2 NULL,
    CONSTRAINT [PK_ProductSerials] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductSerials_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]),
    CONSTRAINT [FK_ProductSerials_ImportReceipts] FOREIGN KEY ([ImportReceiptId]) REFERENCES [ImportReceipts] ([Id]),
    
    -- SỬA LẠI: Unique theo cặp (Variant + Serial) thay vì chỉ Serial
    CONSTRAINT [UQ_ProductSerials_Variant_SN] UNIQUE ([VariantId], [SerialNumber])
);
CREATE INDEX [IX_ProductSerials_Search] ON [ProductSerials] ([SerialNumber]);
CREATE INDEX [IX_ProductSerials_Inventory] ON [ProductSerials] ([VariantId], [Status]);
END
GO

-- 5. Bảng InventoryChecks
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryChecks]') AND type in (N'U'))
BEGIN
CREATE TABLE [InventoryChecks] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [CheckCode] varchar(20) NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [CheckDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [Note] nvarchar(500) NULL,
    [Status] int NOT NULL DEFAULT 0, -- 0: Pending, 1: Completed
    CONSTRAINT [PK_InventoryChecks] PRIMARY KEY ([Id])
);
END
GO

-- 6. Bảng InventoryCheckDetails
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryCheckDetails]') AND type in (N'U'))
BEGIN
CREATE TABLE [InventoryCheckDetails] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [CheckId] int NOT NULL,
    [VariantId] int NOT NULL,
    [SystemQuantity] int NOT NULL,
    [ActualQuantity] int NOT NULL,
    [Reason] nvarchar(255) NULL,
    -- Cột Computed Difference
    [Difference] AS ([ActualQuantity] - [SystemQuantity]),
    CONSTRAINT [PK_InventoryCheckDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryCheckDetails_Checks] FOREIGN KEY ([CheckId]) REFERENCES [InventoryChecks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryCheckDetails_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id])
);
END
GO

-- SEED DATA
INSERT INTO [Suppliers] ([Name], [PhoneNumber], [Email], [Address]) VALUES 
(N'Công Ty Viễn Sơn (Phân phối GIGABYTE/ASUS)', '02833334444', 'sales@vienson.com', N'Q1, TP.HCM'),
(N'FPT Synnex (Phân phối Intel/Kingston)', '02477778888', 'contact@synnexfpt.com', N'Cầu Giấy, Hà Nội'),
(N'Vĩnh Xuân (SPC)', '02433335555', 'sales@spc.com.vn', N'Hai Bà Trưng, Hà Nội');
GO