USE HushStoreDB;
GO

-- 1. Xoá dữ liệu cũ để tránh trùng lặp Primary Key
-- 1. Xoá dữ liệu cũ theo đúng thứ tự ràng buộc FK
DELETE FROM dbo.Carts; -- Nếu có
DELETE FROM dbo.OrderDetails; -- Nếu có
DELETE FROM dbo.ProductSerials; -- Nếu có
DELETE FROM dbo.ProductImages;
DELETE FROM dbo.ProductVariants;
DELETE FROM dbo.Products;
DELETE FROM dbo.Categories;
DELETE FROM dbo.Manufacturers;

-- 2. Seed Manufacturers (Thương hiệu)
SET IDENTITY_INSERT dbo.Manufacturers ON;
INSERT INTO dbo.Manufacturers (Id, Name, LogoUrl, Website, CreatedDate, IsDeleted)
VALUES 
(1, 'MSI', 'https://logos-world.net/wp-content/uploads/2020/11/MSI-Logo.png', 'https://msi.com', GETUTCDATE(), 0),
(2, 'Asus', 'https://logos-world.net/wp-content/uploads/2020/07/Asus-Logo.png', 'https://asus.com', GETUTCDATE(), 0),
(3, 'Logitech', 'https://logos-world.net/wp-content/uploads/2020/11/Logitech-Logo.png', 'https://logitech.com', GETUTCDATE(), 0);
SET IDENTITY_INSERT dbo.Manufacturers OFF;

-- 3. Seed Categories (Danh mục)
SET IDENTITY_INSERT dbo.Categories ON;
INSERT INTO dbo.Categories (Id, Name, Slug, Level, SortOrder, IsVisible, CreatedDate, IsDeleted)
VALUES 
(1, 'Laptop', 'laptop', 0, 1, 1, GETUTCDATE(), 0),
(2, 'Mainboard', 'mainboard', 0, 2, 1, GETUTCDATE(), 0),
(3, 'Chuột máy tính', 'chuot-may-tinh', 0, 3, 1, GETUTCDATE(), 0);
SET IDENTITY_INSERT dbo.Categories OFF;

-- 4. Seed Products
SET IDENTITY_INSERT dbo.Products ON;
INSERT INTO dbo.Products (Id, Name, Slug, ShortDescription, Description, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted)
VALUES 
(1, 'Mainboard MSI GAMING B650M PLUS WIFI DDR5', 'mainboard-msi-gaming-b650m-plus-wifi-ddr5', 
'Dòng bo mạch chủ chuyên game socket AM5 hỗ trợ RAM DDR5.', 
'<h2>Đánh giá chi tiết Mainboard MSI GAMING B650M PLUS WIFI DDR5</h2><p>Dành cho các PC Builder và game thủ thế hệ mới, MSI B650M PLUS WIFI cung cấp một nền tảng vững chắc...</p><img src="https://picsum.photos/800/400" alt="Mainboard image" />', 
1, 2, 1, GETUTCDATE(), 0),

(2, 'Laptop Asus ExpertBook B5', 'laptop-asus-expertbook-b5', 
'Laptop doanh nhân mỏng nhẹ, pin trâu.', 
'<h2>Đánh giá Asus ExpertBook B5</h2><p>Asus ExpertBook B5 là dòng laptop văn phòng cao cấp với độ bền quân đội...</p>', 
2, 1, 1, GETUTCDATE(), 0);
SET IDENTITY_INSERT dbo.Products OFF;

-- 5. Seed Product Variants
SET IDENTITY_INSERT dbo.ProductVariants ON;
INSERT INTO dbo.ProductVariants (Id, ProductId, SKU, VariantName, Slug, Price, OriginalPrice, WarrantyMonth, Specifications, StockQuantity, CreatedDate, IsDeleted)
VALUES 
-- Variants for Mainboard MSI (Id: 1)
(1, 1, 'MSI-B650M-PLUS', 'Phiên bản Tiêu chuẩn', 'phien-ban-tieu-chuan', 4390000, 4800000, 36, 
'{"CPU": "Hỗ trợ AMD Ryzen 7000/8000/9000 Series", "Chipset": "AMD B650", "Memory": "4x DDR5, tối đa 192GB, 7200+(OC) MHz", "Storage": "2x M.2 Gen4 x4, 4x SATA 6G"}', 
50, GETUTCDATE(), 0),

-- Variants for Laptop Asus (Id: 2)
(2, 2, 'ASUS-B5-I5-16-512', 'Intel Core i5 / 16GB / 512GB', 'i5-16gb-512gb', 21900000, 24000000, 24, 
'{"CPU": "Intel Core i5-1235U", "RAM": "16GB DDR4", "SSD": "512GB NVMe Gen4", "Màn hình": "14 inch Full HD"}', 
10, GETUTCDATE(), 0),

(3, 2, 'ASUS-B5-I7-32-1TB', 'Intel Core i7 / 32GB / 1TB', 'i7-32gb-1tb', 28900000, 31000000, 24, 
'{"CPU": "Intel Core i7-1255U", "RAM": "32GB DDR4", "SSD": "1TB NVMe Gen4", "Màn hình": "14 inch Full HD OLED"}', 
0, GETUTCDATE(), 0); -- Hết hàng để test logic UI
SET IDENTITY_INSERT dbo.ProductVariants OFF;

-- 6. Seed Product Images
SET IDENTITY_INSERT dbo.ProductImages ON;
INSERT INTO dbo.ProductImages (Id, VariantId, ImageUrl, IsMain, SortOrder)
VALUES 
-- Images for MSI Mainboard
(1, 1, 'https://picsum.photos/400/400?sig=1', 1, 1),
(2, 1, 'https://picsum.photos/400/400?sig=2', 0, 2),
(3, 1, 'https://picsum.photos/400/400?sig=3', 0, 3),

-- Images for Asus B5 i5
(4, 2, 'https://picsum.photos/400/400?sig=4', 1, 1),
(5, 2, 'https://picsum.photos/400/400?sig=5', 0, 2),

-- Images for Asus B5 i7
(6, 3, 'https://picsum.photos/400/400?sig=6', 1, 1);
SET IDENTITY_INSERT dbo.ProductImages OFF;
