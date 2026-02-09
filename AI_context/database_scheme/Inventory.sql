-- =============================================
-- PHẦN 3: INVENTORY MANAGEMENT (KHO & SERIAL)
-- =============================================

-- 1. Bảng Suppliers (Nhà cung cấp)
CREATE TABLE [Suppliers] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [ContactPerson] nvarchar(100) NULL,
    [PhoneNumber] varchar(20) NOT NULL,
    [Email] varchar(100) NULL,
    [Address] nvarchar(255) NULL,
    [TaxCode] varchar(20) NULL, -- Mã số thuế

-- Audit
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [IsDeleted] bit NOT NULL DEFAULT 0,

    CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
GO

-- 2. Bảng ImportReceipts (Phiếu nhập kho - Header)
CREATE TABLE [ImportReceipts] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ReceiptCode] varchar(20) NOT NULL, -- VD: PN-240210-001

    [SupplierId] int NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL, -- Người nhập (Link sang Module Auth)

    [ImportDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [TotalAmount] decimal(18,2) NOT NULL DEFAULT 0, -- Tổng tiền trả NCC
    [Note] nvarchar(500) NULL,

    [IsDeleted] bit NOT NULL DEFAULT 0, -- Cho phép hủy phiếu nhập (nếu nhập sai)

    CONSTRAINT [PK_ImportReceipts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ImportReceipts_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]),
    CONSTRAINT [UQ_ImportReceipts_Code] UNIQUE ([ReceiptCode])
    );
GO

-- 3. Bảng ImportReceiptDetails (Chi tiết nhập - Dòng hàng)
-- Lưu ý: Bảng này chỉ lưu số lượng tổng & giá vốn. Serial cụ thể lưu bảng dưới.
CREATE TABLE [ImportReceiptDetails] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ReceiptId] int NOT NULL,
    [VariantId] int NOT NULL, -- Nhập sản phẩm nào (Link sang Module Product)

    [Quantity] int NOT NULL, -- Số lượng nhập
    [ImportPrice] decimal(18,2) NOT NULL, -- Giá vốn (Cost) tại thời điểm nhập

    CONSTRAINT [PK_ImportReceiptDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ImportReceiptDetails_Receipts] FOREIGN KEY ([ReceiptId]) REFERENCES [ImportReceipts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ImportReceiptDetails_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id])
    );
GO

-- 4. Bảng ProductSerials (Định danh vật lý) -> TRÁI TIM CỦA MODULE KHO
-- Vì "mặc định có Serial hết", nên mọi sản phẩm nhập vào đều sinh 1 dòng ở đây.
CREATE TABLE [ProductSerials] (
    [Id] int IDENTITY(1,1) NOT NULL, -- ID nội bộ
    [SerialNumber] varchar(100) NOT NULL, -- Mã vạch/SN trên vỏ hộp (Quét súng Barcode)

    [VariantId] int NOT NULL, -- Là con của dòng sản phẩm nào
    [ImportReceiptId] int NOT NULL, -- Truy vết nguồn gốc (Nhập từ lô nào, ngày nào, NCC nào)

-- Trạng thái vòng đời sản phẩm
-- 0: Available (Trong kho, bán được)
-- 1: Reserved (Khách đã đặt Online, đang chờ ship, không bán cho người khác)
-- 2: Sold (Đã bán thành công)
-- 3: Defective (Hàng lỗi, chờ trả NCC)
-- 4: Returned (Khách trả hàng, đang kiểm tra)
    [Status] int NOT NULL DEFAULT 0,

    [OrderId] int NULL, -- Nếu đã bán/đặt, thì thuộc đơn hàng nào (Link sang Module Sale)

-- Audit
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [SoldDate] datetime2 NULL, -- Ngày bán thực tế (để tính bảo hành)

    CONSTRAINT [PK_ProductSerials] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductSerials_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]),
    CONSTRAINT [FK_ProductSerials_ImportReceipts] FOREIGN KEY ([ImportReceiptId]) REFERENCES [ImportReceipts] ([Id]),
    -- Index Unique: 1 Mã Serial chỉ được tồn tại duy nhất trong hệ thống (tránh nhập trùng)
    CONSTRAINT [UQ_ProductSerials_SN] UNIQUE ([SerialNumber])
    );
GO

-- Index tìm kiếm Serial cực nhanh (khi quét mã bán hàng/bảo hành)
CREATE INDEX [IX_ProductSerials_Search] ON [ProductSerials] ([SerialNumber]);
CREATE INDEX [IX_ProductSerials_Inventory] ON [ProductSerials] ([VariantId], [Status]); -- Index để đếm tồn kho
GO


-- =============================================
-- PHẦN 4: INVENTORY CHECK (KIỂM KÊ KHO - Theo SRS UC013)
-- =============================================

-- 5. Bảng InventoryChecks (Phiếu kiểm kê)
CREATE TABLE [InventoryChecks] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [CheckCode] varchar(20) NOT NULL, -- KK-240210
    [EmployeeId] uniqueidentifier NOT NULL,
    [CheckDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),

    [Note] nvarchar(500) NULL,
    [Status] int NOT NULL DEFAULT 0, -- 0: Pending (Đang đếm), 1: Completed (Đã cân bằng)

    CONSTRAINT [PK_InventoryChecks] PRIMARY KEY ([Id])
    );
GO

-- 6. Bảng InventoryCheckDetails (Chi tiết lệch)
-- Lưu kết quả đối soát: Máy tính báo 10, đếm được 8 -> Lệch -2
CREATE TABLE [InventoryCheckDetails] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [CheckId] int NOT NULL,
    [VariantId] int NOT NULL,

    [SystemQuantity] int NOT NULL, -- Tồn kho lý thuyết (Lúc tạo phiếu)
    [ActualQuantity] int NOT NULL, -- Tồn kho thực tế (Đếm được)
    [Difference] AS ([ActualQuantity] - [SystemQuantity]), -- Cột tính toán tự động

    [Reason] nvarchar(255) NULL, -- Lý do: Mất cắp, Hư hỏng, Tìm thấy...

    CONSTRAINT [PK_InventoryCheckDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryCheckDetails_Checks] FOREIGN KEY ([CheckId]) REFERENCES [InventoryChecks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryCheckDetails_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id])
    );
GO

-- =============================================
-- SEED DATA (Dữ liệu mẫu NCC)
-- =============================================
INSERT INTO [Suppliers] ([Name], [PhoneNumber], [Email], [Address]) VALUES 
(N'Công Ty Viễn Sơn (Phân phối GIGABYTE/ASUS)', '02833334444', 'sales@vienson.com', N'Q1, TP.HCM'),
(N'FPT Synnex (Phân phối Intel/Kingston)', '02477778888', 'contact@synnexfpt.com', N'Cầu Giấy, Hà Nội'),
(N'Vĩnh Xuân (SPC)', '02433335555', 'sales@spc.com.vn', N'Hai Bà Trưng, Hà Nội');
GO