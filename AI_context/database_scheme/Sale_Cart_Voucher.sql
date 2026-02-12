-- =============================================
-- PHẦN 4: SALES & ORDER MANAGEMENT
-- =============================================

-- 1. Bảng Vouchers (Mã giảm giá/Khuyến mãi)
CREATE TABLE [Vouchers] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Code] varchar(50) NOT NULL, -- VD: TET2026, BLACKFRIDAY
    [Name] nvarchar(200) NOT NULL,

    [DiscountType] int NOT NULL DEFAULT 0, -- 0: Giảm theo Tiền (Amount), 1: Giảm theo % (Percentage)
    [DiscountValue] decimal(18,2) NOT NULL, -- VD: 50.000 hoặc 10 (%)

    [MinOrderValue] decimal(18,2) NOT NULL DEFAULT 0, -- Đơn tối thiểu để áp dụng
    [MaxDiscountAmount] decimal(18,2) NULL, -- Giảm tối đa (cho loại %)

    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,

    [Quantity] int NOT NULL DEFAULT 0, -- Tổng số lượng mã
    [UsedCount] int NOT NULL DEFAULT 0, -- Số lượng đã dùng

    [IsActive] bit NOT NULL DEFAULT 1,

    CONSTRAINT [PK_Vouchers] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Vouchers_Code] UNIQUE ([Code]),
    CONSTRAINT [CK_Vouchers_Date] CHECK ([EndDate] >= [StartDate])
    );
GO

-- 2. Bảng Orders (Đơn hàng - Header)
CREATE TABLE [Orders] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [OrderCode] varchar(20) NOT NULL, -- Human Readable: DH-260123-XXX

    [UserId] uniqueidentifier NULL, -- Khách hàng (Null nếu khách vãng lai mua tại quầy)
    [EmployeeId] uniqueidentifier NULL, -- Nhân viên tạo đơn (Null nếu khách tự đặt Online)

    [OrderDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),

    -- Trạng thái đơn hàng
    -- 0: Pending (Chờ xác nhận)
    -- 1: Confirmed (Đã duyệt, chờ đóng gói)
    -- 2: Shipping (Đang giao)
    -- 3: Success (Hoàn thành)
    -- 4: Cancelled (Đã hủy)
    -- 5: Returned (Trả hàng)
    [Status] int NOT NULL DEFAULT 0,

    -- Thông tin giao hàng (SNAPSHOT - Lưu chết tại thời điểm đặt)
    -- Lý do: Khách có thể đổi địa chỉ trong Profile sau này, nhưng đơn cũ phải giữ nguyên địa chỉ cũ.
    [ShipName] nvarchar(100) NOT NULL,
    [ShipPhone] varchar(20) NOT NULL,
    [ShipAddress] nvarchar(255) NOT NULL,
    [ShipCity] nvarchar(100) NOT NULL,

    -- Tài chính
    [SubTotal] decimal(18,2) NOT NULL DEFAULT 0, -- Tổng tiền hàng
    [ShippingFee] decimal(18,2) NOT NULL DEFAULT 0, -- Phí ship

    [VoucherId] int NULL,
    [DiscountAmount] decimal(18,2) NOT NULL DEFAULT 0, -- Tiền giảm giá

    [TotalAmount] decimal(18,2) NOT NULL DEFAULT 0, -- Tổng thanh toán cuối cùng (= Sub + Ship - Discount)

-- Thanh toán
    [PaymentMethod] int NOT NULL DEFAULT 0, -- 0: COD, 1: Banking, 2: VNPay
    [PaymentStatus] int NOT NULL DEFAULT 0, -- 0: Unpaid, 1: Paid, 2: Refunded

-- Audit
    [Note] nvarchar(500) NULL, -- Ghi chú của khách
    [CancelReason] nvarchar(255) NULL, -- Lý do hủy đơn

    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_Users] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]),
    CONSTRAINT [FK_Orders_Employees] FOREIGN KEY ([EmployeeId]) REFERENCES [AppUsers] ([Id]),
    CONSTRAINT [FK_Orders_Vouchers] FOREIGN KEY ([VoucherId]) REFERENCES [Vouchers] ([Id]),
    CONSTRAINT [UQ_Orders_Code] UNIQUE ([OrderCode])
    );
GO

-- Index để tìm kiếm đơn hàng & Lịch sử mua hàng nhanh
CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
CREATE INDEX [IX_Orders_OrderDate] ON [Orders] ([OrderDate]);
GO

-- 3. Bảng OrderDetails (Chi tiết đơn hàng - Dòng sản phẩm)
CREATE TABLE [OrderDetails] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [OrderId] int NOT NULL,
    [VariantId] int NOT NULL, -- SKU nào

    [Quantity] int NOT NULL DEFAULT 1,

    -- GIÁ SNAPSHOT (QUAN TRỌNG)
    -- Phải lưu giá tại thời điểm mua. Không được JOIN về bảng Product để lấy giá.
    -- Vì tháng sau giá Product tăng, xem lại đơn cũ giá phải giữ nguyên.
    [UnitPrice] decimal(18,2) NOT NULL,

    [TotalLine] AS ([Quantity] * [UnitPrice]), -- Computed Column

    CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderDetails_Orders] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderDetails_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id])
    );
GO

-- 4. Bảng OrderSerials (Liên kết Đơn hàng - Serial vật lý)
-- Đây là bảng GIẢI QUYẾT BÀI TOÁN "100% SẢN PHẨM CÓ SERIAL"
-- Khi xuất kho (Status = Shipping/Success), nhân viên phải quét Serial gán vào đơn.
CREATE TABLE [OrderSerials] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [OrderDetailId] int NOT NULL, -- Thuộc dòng nào trong đơn
    [SerialId] int NOT NULL, -- Serial nào trong kho (Module 3)

    CONSTRAINT [PK_OrderSerials] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderSerials_OrderDetails] FOREIGN KEY ([OrderDetailId]) REFERENCES [OrderDetails] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderSerials_Serials] FOREIGN KEY ([SerialId]) REFERENCES [ProductSerials] ([Id]),
    -- Ràng buộc: 1 Serial chỉ thuộc về 1 dòng đơn hàng tại 1 thời điểm (trong context bán)
    CONSTRAINT [UQ_OrderSerials_SerialId] UNIQUE ([SerialId])
    );
GO

-- 5. Bảng Carts (Giỏ hàng - Lưu tạm)
-- Dùng để lưu trạng thái giỏ hàng khi user chưa checkout.
CREATE TABLE [Carts] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [VariantId] int NOT NULL,
    [Quantity] int NOT NULL DEFAULT 1,
    [CreatedDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Carts_Users] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Carts_Variants] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id])
    );
GO

-- Index Unique: 1 User chỉ có 1 dòng cho 1 sản phẩm trong giỏ (Nếu thêm trùng thì update quantity)
CREATE UNIQUE INDEX [IX_Carts_User_Variant] ON [Carts] ([UserId], [VariantId]);
GO

-- =============================================
-- SEED DATA (Dữ liệu mẫu Voucher)
-- =============================================
INSERT INTO [Vouchers] 
([Code], [Name], [DiscountType], [DiscountValue], [MinOrderValue], [StartDate], [EndDate], [Quantity]) 
VALUES 
('WELCOME', N'Giảm 50k cho đơn đầu tiên', 0, 50000, 500000, GETUTCDATE(), DATEADD(year, 1, GETUTCDATE()), 1000),
('TET2026', N'Lì xì 10%', 1, 10, 1000000, GETUTCDATE(), DATEADD(month, 1, GETUTCDATE()), 500);
GO