-- =============================================
-- SEED DATA: LINH KIỆN MÁY TÍNH & PC BUILD SẴN
-- =============================================
-- Category slugs khớp với BuildPc.razor _slots

USE [HushStoreDb];
GO

-- ============================================
-- 0. XÓA DỮ LIỆU CŨ (theo slug, không phụ thuộc ID)
-- ============================================
DECLARE @RootCatIds TABLE (Id INT);
INSERT INTO @RootCatIds
    SELECT Id FROM [Categories]
    WHERE Slug IN (N'linh-kien-may-tinh', N'pc-build-san');

DECLARE @AllCatIds TABLE (Id INT);
INSERT INTO @AllCatIds
    SELECT Id FROM [Categories]
    WHERE Id IN (SELECT Id FROM @RootCatIds)
       OR ParentId IN (SELECT Id FROM @RootCatIds);

DELETE FROM [ProductVariants]
WHERE ProductId IN (
    SELECT Id FROM [Products]
    WHERE CategoryId IN (SELECT Id FROM @AllCatIds)
);
DELETE FROM [Products]
WHERE CategoryId IN (SELECT Id FROM @AllCatIds);
DELETE FROM [Categories]
WHERE Id IN (SELECT Id FROM @AllCatIds);
DELETE FROM [Manufacturers]
WHERE Name IN (
    N'Intel', N'AMD', N'NVIDIA', N'Samsung', N'Kingston', N'Corsair',
    N'ASUS', N'Gigabyte', N'MSI', N'Seagate', N'Western Digital',
    N'Cooler Master', N'Noctua', N'be quiet!', N'Seasonic', N'LG',
    N'Logitech', N'Razer', N'DeepCool', N'Microsoft', N'HushStore Custom'
);

-- ============================================
-- 1. MANUFACTURERS
-- ============================================
SET IDENTITY_INSERT [Manufacturers] ON;

INSERT INTO [Manufacturers] (Id, Name, Website, SupportEmail, CreatedDate, IsDeleted) VALUES
( 1, N'Intel',               N'https://www.intel.com',          N'support@intel.com',        GETUTCDATE(), 0),
( 2, N'AMD',                 N'https://www.amd.com',            N'support@amd.com',           GETUTCDATE(), 0),
( 3, N'NVIDIA',              N'https://www.nvidia.com',         N'support@nvidia.com',        GETUTCDATE(), 0),
( 4, N'Samsung',             N'https://www.samsung.com',        N'support@samsung.com',       GETUTCDATE(), 0),
( 5, N'Kingston',            N'https://www.kingston.com',       N'support@kingston.com',      GETUTCDATE(), 0),
( 6, N'Corsair',             N'https://www.corsair.com',        N'support@corsair.com',       GETUTCDATE(), 0),
( 7, N'ASUS',                N'https://www.asus.com',           N'support@asus.com',          GETUTCDATE(), 0),
( 8, N'Gigabyte',            N'https://www.gigabyte.com',       N'support@gigabyte.com',      GETUTCDATE(), 0),
( 9, N'MSI',                 N'https://www.msi.com',            N'support@msi.com',           GETUTCDATE(), 0),
(10, N'Seagate',             N'https://www.seagate.com',        N'support@seagate.com',       GETUTCDATE(), 0),
(11, N'Western Digital',     N'https://www.westerndigital.com', N'support@wd.com',            GETUTCDATE(), 0),
(12, N'Cooler Master',       N'https://www.coolermaster.com',   N'support@coolermaster.com',  GETUTCDATE(), 0),
(13, N'Noctua',              N'https://noctua.at',              N'support@noctua.at',         GETUTCDATE(), 0),
(14, N'be quiet!',           N'https://www.bequiet.com',        N'support@bequiet.com',       GETUTCDATE(), 0),
(15, N'Seasonic',            N'https://www.seasonic.com',       N'support@seasonic.com',      GETUTCDATE(), 0),
(16, N'LG',                  N'https://www.lg.com',             N'support@lg.com',            GETUTCDATE(), 0),
(17, N'Logitech',            N'https://www.logitech.com',       N'support@logitech.com',      GETUTCDATE(), 0),
(18, N'Razer',               N'https://www.razer.com',          N'support@razer.com',         GETUTCDATE(), 0),
(19, N'DeepCool',            N'https://www.deepcool.com',       N'support@deepcool.com',      GETUTCDATE(), 0),
(20, N'Microsoft',           N'https://www.microsoft.com',      N'support@microsoft.com',     GETUTCDATE(), 0),
(21, N'HushStore Custom',    NULL,                              N'build@hushstore.com',       GETUTCDATE(), 0);

SET IDENTITY_INSERT [Manufacturers] OFF;

-- ============================================
-- 2. CATEGORIES
-- ============================================
SET IDENTITY_INSERT [Categories] ON;

-- Root: linh kiện
INSERT INTO [Categories] (Id, Name, Slug, ParentId, Level, SortOrder, IsVisible, CreatedDate, IsDeleted) VALUES
(100, N'Linh kiện máy tính', N'linh-kien-may-tinh', NULL, 0, 1, 1, GETUTCDATE(), 0);

-- Sub-categories — slugs PHẢI khớp CategorySlug trong BuildPc.razor
INSERT INTO [Categories] (Id, Name, Slug, ParentId, Level, SortOrder, IsVisible, CreatedDate, IsDeleted) VALUES
(101, N'CPU - Bộ vi xử lý',        N'cpu',                100, 1,  1, 1, GETUTCDATE(), 0),
(102, N'Bo mạch chủ (Mainboard)',   N'bo-mach-chu',        100, 1,  2, 1, GETUTCDATE(), 0),
(103, N'RAM - Bộ nhớ trong',       N'ram',                100, 1,  3, 1, GETUTCDATE(), 0),
(104, N'Ổ cứng HDD',               N'hdd',                100, 1,  4, 1, GETUTCDATE(), 0),
(105, N'Ổ cứng SSD',               N'ssd',                100, 1,  5, 1, GETUTCDATE(), 0),
(106, N'Card màn hình (VGA)',       N'vga',                100, 1,  6, 1, GETUTCDATE(), 0),
(107, N'Nguồn máy tính (PSU)',      N'nguon',              100, 1,  7, 1, GETUTCDATE(), 0),
(108, N'Vỏ Case máy tính',         N'vo-case',            100, 1,  8, 1, GETUTCDATE(), 0),
(109, N'Fan Case',                 N'fan-case',           100, 1,  9, 1, GETUTCDATE(), 0),
(110, N'Màn hình máy tính',        N'man-hinh',           100, 1, 10, 1, GETUTCDATE(), 0),
(111, N'Chuột máy tính',           N'chuot',              100, 1, 11, 1, GETUTCDATE(), 0),
(112, N'Bàn phím máy tính',        N'ban-phim',           100, 1, 12, 1, GETUTCDATE(), 0),
(113, N'Tản nhiệt khí',            N'tan-nhiet-khi',      100, 1, 13, 1, GETUTCDATE(), 0),
(114, N'Tản nhiệt nước AIO',       N'tan-nhiet-nuoc-aio', 100, 1, 14, 1, GETUTCDATE(), 0),
(115, N'Tai nghe gaming',          N'tai-nghe',           100, 1, 15, 1, GETUTCDATE(), 0),
(116, N'Phần mềm',                 N'phan-mem',           100, 1, 16, 1, GETUTCDATE(), 0);

-- Root: PC Build sẵn (danh mục riêng, không thuộc linh kiện)
INSERT INTO [Categories] (Id, Name, Slug, ParentId, Level, SortOrder, IsVisible, CreatedDate, IsDeleted) VALUES
(200, N'PC Build sẵn', N'pc-build-san', NULL, 0, 2, 1, GETUTCDATE(), 0);

SET IDENTITY_INSERT [Categories] OFF;

-- ============================================
-- 3. PRODUCTS
-- ============================================
SET IDENTITY_INSERT [Products] ON;

-- CPU (101)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
( 1, N'Intel Core i5-13600K',  N'intel-core-i5-13600k', N'CPU Intel Raptor Lake 14 nhân 20 luồng, socket LGA1700', 1, 101, 1, GETUTCDATE(), 0),
( 2, N'Intel Core i7-13700K',  N'intel-core-i7-13700k', N'CPU Intel Raptor Lake 16 nhân 24 luồng, socket LGA1700', 1, 101, 1, GETUTCDATE(), 0),
( 3, N'AMD Ryzen 5 7600X',     N'amd-ryzen-5-7600x',    N'CPU AMD Zen4 6 nhân 12 luồng, socket AM5, PCIe 5.0',    2, 101, 1, GETUTCDATE(), 0);

-- Mainboard (102)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
( 4, N'ASUS ROG STRIX Z790-E Gaming WiFi',  N'asus-rog-strix-z790-e',        N'Mainboard Z790 DDR5 cao cấp, PCIe 5.0, WiFi 6E',       7, 102, 1, GETUTCDATE(), 0),
( 5, N'Gigabyte Z790 AORUS Elite AX',       N'gigabyte-z790-aorus-elite-ax', N'Mainboard Z790 DDR5, WiFi 6E, Bluetooth 5.2',           8, 102, 1, GETUTCDATE(), 0),
( 6, N'MSI MAG B650 TOMAHAWK WIFI',         N'msi-mag-b650-tomahawk-wifi',   N'Mainboard B650 cho AM5, DDR5, WiFi 6E, giá tốt',       9, 102, 1, GETUTCDATE(), 0);

-- RAM (103)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
( 7, N'Corsair Vengeance DDR5',          N'corsair-vengeance-ddr5',       N'RAM DDR5 5600MHz, tản nhiệt nhôm anodized, XMP 3.0',   6, 103, 1, GETUTCDATE(), 0),
( 8, N'Samsung DDR5 5600MHz',            N'samsung-ddr5-5600',            N'RAM DDR5 Samsung chính hãng, hiệu năng ổn định',       4, 103, 1, GETUTCDATE(), 0),
( 9, N'Kingston FURY Beast DDR4 3600MHz',N'kingston-fury-beast-ddr4',     N'RAM DDR4 3600MHz, tương thích Intel/AMD, XMP 2.0',     5, 103, 1, GETUTCDATE(), 0);

-- HDD (104)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(10, N'Seagate Barracuda 2TB',   N'seagate-barracuda-2tb',  N'HDD 3.5" 2TB 7200 RPM SATA III, cache 256MB', 10, 104, 1, GETUTCDATE(), 0),
(11, N'Western Digital Blue 4TB',N'wd-blue-4tb',            N'HDD 3.5" 4TB 5400 RPM SATA III, đáng tin cậy',11, 104, 1, GETUTCDATE(), 0),
(12, N'Seagate IronWolf 4TB',    N'seagate-ironwolf-4tb',   N'HDD NAS 3.5" 4TB, tối ưu cho NAS 24/7',       10, 104, 1, GETUTCDATE(), 0);

-- SSD (105)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(13, N'Samsung 980 Pro M.2 NVMe',    N'samsung-980-pro',       N'SSD PCIe 4.0 NVMe, đọc 7000MB/s, DRAM cache',  4, 105, 1, GETUTCDATE(), 0),
(14, N'WD Black SN850X M.2 NVMe',    N'wd-black-sn850x',       N'SSD PCIe 4.0 NVMe, đọc 7300MB/s, bảo hành 5 năm',11, 105, 1, GETUTCDATE(), 0),
(15, N'Kingston KC3000 M.2 NVMe',    N'kingston-kc3000',       N'SSD PCIe 4.0 NVMe, đọc 7000MB/s, bảo hành 5 năm', 5, 105, 1, GETUTCDATE(), 0);

-- VGA (106)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(16, N'ASUS TUF Gaming RTX 4070 OC',      N'asus-tuf-rtx-4070-oc',         N'RTX 4070 12GB GDDR6X, 3 quạt AXIAL-tech, DLSS 3',  7, 106, 1, GETUTCDATE(), 0),
(17, N'Gigabyte RTX 4060 Ti Gaming OC',   N'gigabyte-rtx-4060ti-gaming-oc',N'RTX 4060 Ti 8GB GDDR6, WINDFORCE 3X, Ray Tracing',  8, 106, 1, GETUTCDATE(), 0),
(18, N'MSI MECH Radeon RX 7800 XT 16G',  N'msi-rx-7800xt-mech',           N'RX 7800 XT 16GB GDDR6, RDNA3, tốt cho 1440p',       9, 106, 1, GETUTCDATE(), 0);

-- PSU (107)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(19, N'Corsair RM850x 850W Gold',         N'corsair-rm850x-850w',          N'PSU 850W 80+ Gold, Full Modular, fanless tải thấp',  6, 107, 1, GETUTCDATE(), 0),
(20, N'Seasonic Focus GX-850 850W',       N'seasonic-focus-gx-850',        N'PSU 850W 80+ Gold, Full Modular, bảo hành 10 năm',  15, 107, 1, GETUTCDATE(), 0),
(21, N'be quiet! Straight Power 11 750W', N'bequiet-straight-power-11-750w',N'PSU 750W 80+ Gold, siêu im lặng, Full Modular',    14, 107, 1, GETUTCDATE(), 0);

-- Case (108)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(22, N'Corsair 4000D Airflow ATX',        N'corsair-4000d-airflow',        N'Case Mid-Tower ATX, airflow tối ưu, 2 fan 120mm',   6, 108, 1, GETUTCDATE(), 0),
(23, N'Cooler Master MasterBox TD500 Mesh',N'coolermaster-td500-mesh',     N'Case ATX lưới thép, 5 fan ARGB, kính cường lực',    12, 108, 1, GETUTCDATE(), 0),
(24, N'ASUS TUF Gaming GT502',            N'asus-tuf-gaming-gt502',        N'Case ATX, 6 fan ARGB, hỗ trợ 360mm AIO',            7, 108, 1, GETUTCDATE(), 0);

-- Fan Case (109)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(25, N'DeepCool FC120 ARGB 3-pack',       N'deepcool-fc120-argb-3pack',    N'Bộ 3 fan 120mm ARGB kèm Hub Controller',            19, 109, 1, GETUTCDATE(), 0),
(26, N'Cooler Master SickleFlow 120 ARGB',N'coolermaster-sickleflow-120',  N'Fan 120mm ARGB, vòng bi FDB, tiếng ồn thấp',        12, 109, 1, GETUTCDATE(), 0),
(27, N'Corsair LL120 RGB 3-pack',         N'corsair-ll120-rgb-3pack',      N'Bộ 3 fan 120mm RGB 16 LED, kèm Lighting Node Core',  6, 109, 1, GETUTCDATE(), 0);

-- Màn hình (110)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(28, N'LG 27GP850-B 27" QHD 165Hz',      N'lg-27gp850-b',                 N'IPS 27" 2560x1440 165Hz 1ms, FreeSync Premium',     16, 110, 1, GETUTCDATE(), 0),
(29, N'MSI MAG274QRF-QD 27" 165Hz',      N'msi-mag274qrf-qd',             N'IPS 27" QHD 165Hz Quantum Dot, G-Sync Compatible',   9, 110, 1, GETUTCDATE(), 0),
(30, N'ASUS ROG Swift PG279QM 27" 240Hz',N'asus-rog-pg279qm',             N'IPS 27" QHD 240Hz G-Sync, HDR400, cao cấp',          7, 110, 1, GETUTCDATE(), 0);

-- Chuột (111)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(31, N'Logitech G Pro X Superlight 2',   N'logitech-g-pro-x-superlight-2',N'Chuột gaming không dây 60g, HERO 2 25K DPI',         17, 111, 1, GETUTCDATE(), 0),
(32, N'Razer DeathAdder V3 Pro',         N'razer-deathadder-v3-pro',      N'Chuột gaming không dây ergonomic, Focus Pro 30K DPI',18, 111, 1, GETUTCDATE(), 0),
(33, N'Logitech G502 X Plus',            N'logitech-g502-x-plus',         N'Chuột gaming không dây có trọng lượng, HERO 25K DPI',17, 111, 1, GETUTCDATE(), 0);

-- Bàn phím (112)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(34, N'Logitech G Pro X TKL Rapid',      N'logitech-g-pro-x-tkl-rapid',   N'Bàn phím TKL switch quang học, không dây 2.4GHz',   17, 112, 1, GETUTCDATE(), 0),
(35, N'Razer BlackWidow V4 Pro',         N'razer-blackwidow-v4-pro',      N'Bàn phím cơ full-size, Razer Green, RGB Chroma',     18, 112, 1, GETUTCDATE(), 0),
(36, N'MSI Vigor GK71 Sonic',            N'msi-vigor-gk71-sonic',         N'Bàn phím cơ gaming, switch đỏ/xanh, RGB per-key',    9, 112, 1, GETUTCDATE(), 0);

-- Tản nhiệt khí (113)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(37, N'Noctua NH-D15',                   N'noctua-nh-d15',                N'Tản nhiệt khí dual tower, 2 fan NF-A15, TDP 250W+',  13, 113, 1, GETUTCDATE(), 0),
(38, N'Cooler Master Hyper 212 EVO V2',  N'coolermaster-hyper-212-evo-v2',N'Tản nhiệt khí 4 ống đồng, fan 120mm ARGB, TDP 150W',12, 113, 1, GETUTCDATE(), 0),
(39, N'DeepCool AK620',                  N'deepcool-ak620',               N'Tản nhiệt khí dual tower, 2 fan 120mm, TDP 260W',    19, 113, 1, GETUTCDATE(), 0);

-- Tản nhiệt nước AIO (114)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(40, N'Corsair iCUE H150i Elite CAPELLIX 360mm',N'corsair-h150i-elite-360',N'AIO 360mm, 3 fan LL120 RGB, đầu bơm CAPELLIX LED', 6, 114, 1, GETUTCDATE(), 0),
(41, N'DeepCool LT720 360mm LCD',        N'deepcool-lt720-360',           N'AIO 360mm, màn LCD đầu bơm, 3 fan ARGB',             19, 114, 1, GETUTCDATE(), 0),
(42, N'Cooler Master MasterLiquid 360L ARGB',N'coolermaster-ml360l-argb', N'AIO 360mm, 3 fan 120mm ARGB, lắp đặt dễ dàng',     12, 114, 1, GETUTCDATE(), 0);

-- Tai nghe (115)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(43, N'Logitech G Pro X 2 LIGHTSPEED',   N'logitech-g-pro-x2-lightspeed', N'Tai nghe gaming không dây, Blue VO!CE mic, 50 giờ', 17, 115, 1, GETUTCDATE(), 0),
(44, N'Razer BlackShark V2 Pro',         N'razer-blackshark-v2-pro',      N'Tai nghe gaming không dây, THX Spatial, 70 giờ pin',18, 115, 1, GETUTCDATE(), 0);

-- Phần mềm (116)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(45, N'Microsoft Windows 11 Home',       N'ms-windows-11-home',           N'Hệ điều hành bản quyền ESD, 1 PC, nâng cấp free từ Win10',20, 116, 1, GETUTCDATE(), 0),
(46, N'Microsoft Office 2024 Home & Student',N'ms-office-2024-home-student',N'Word, Excel, PowerPoint, OneNote — bản quyền vĩnh viễn',20, 116, 1, GETUTCDATE(), 0);

-- PC Build sẵn (200)
INSERT INTO [Products] (Id, Name, Slug, ShortDescription, ManufacturerId, CategoryId, Status, CreatedDate, IsDeleted) VALUES
(47, N'PC Gaming Tầm Trung RTX 4060 Ti',  N'pc-gaming-tam-trung-rtx-4060ti',N'i5-13600K + RTX 4060 Ti 8GB + 16GB DDR5 + 1TB NVMe',21, 200, 1, GETUTCDATE(), 0),
(48, N'PC Gaming Đỉnh Cao RTX 4090',       N'pc-gaming-dinh-cao-rtx-4090',   N'i9-13900K + RTX 4090 24GB + 64GB DDR5 + 2TB NVMe',  21, 200, 1, GETUTCDATE(), 0),
(49, N'PC Văn Phòng Doanh Nghiệp',         N'pc-van-phong-doanh-nghiep',     N'i5-12400 + 16GB DDR4 + 512GB SSD, bền bỉ văn phòng',21, 200, 1, GETUTCDATE(), 0);

SET IDENTITY_INSERT [Products] OFF;

-- ============================================
-- 4. PRODUCT VARIANTS
-- ============================================
SET IDENTITY_INSERT [ProductVariants] ON;

-- CPU variants
INSERT INTO [ProductVariants] (Id, ProductId, SKU, VariantName, Slug, Price, OriginalPrice, WarrantyMonth, Specifications, StockQuantity, CreatedDate, IsDeleted) VALUES
(1, 1, N'CPU-I5-13600K-BOX', N'Intel Core i5-13600K Box', N'intel-core-i5-13600k-box',
 7490000, 7990000, 36,
 N'{"Socket":"LGA1700","Số nhân":"14 (6P+8E)","Số luồng":"20","Xung cơ bản":"3.5 GHz","Xung tối đa":"5.1 GHz","Cache L3":"24MB","TDP":"125W","Tiến trình":"Intel 7","RAM hỗ trợ":"DDR4/DDR5"}',
 10, GETUTCDATE(), 0),

(2, 2, N'CPU-I7-13700K-BOX', N'Intel Core i7-13700K Box', N'intel-core-i7-13700k-box',
 10490000, 11490000, 36,
 N'{"Socket":"LGA1700","Số nhân":"16 (8P+8E)","Số luồng":"24","Xung cơ bản":"3.4 GHz","Xung tối đa":"5.4 GHz","Cache L3":"30MB","TDP":"125W","Tiến trình":"Intel 7","RAM hỗ trợ":"DDR4/DDR5"}',
 8, GETUTCDATE(), 0),

(3, 3, N'CPU-R5-7600X-BOX', N'AMD Ryzen 5 7600X Box', N'amd-ryzen-5-7600x-box',
 5990000, 6490000, 36,
 N'{"Socket":"AM5","Số nhân":"6","Số luồng":"12","Xung cơ bản":"4.7 GHz","Xung tối đa":"5.3 GHz","Cache L3":"32MB","TDP":"105W","Tiến trình":"TSMC 5nm","RAM hỗ trợ":"DDR5"}',
 12, GETUTCDATE(), 0),

-- Mainboard variants
(4, 4, N'MB-ASUS-Z790-E', N'ASUS ROG STRIX Z790-E Gaming WiFi II', N'asus-rog-strix-z790-e-wifi',
 12990000, 13990000, 36,
 N'{"Socket":"LGA1700","Chipset":"Z790","RAM":"DDR5","Khe RAM":"4","PCIe":"5.0 x16","M.2":"5 khe","WiFi":"6E","Bluetooth":"5.3","Form factor":"ATX"}',
 5, GETUTCDATE(), 0),

(5, 5, N'MB-GIGA-Z790-AX', N'Gigabyte Z790 AORUS Elite AX DDR5', N'gigabyte-z790-aorus-elite-ax-ddr5',
 8490000, 9490000, 36,
 N'{"Socket":"LGA1700","Chipset":"Z790","RAM":"DDR5","Khe RAM":"4","PCIe":"5.0 x16","M.2":"4 khe","WiFi":"6E","Bluetooth":"5.2","Form factor":"ATX"}',
 6, GETUTCDATE(), 0),

(6, 6, N'MB-MSI-B650-TW', N'MSI MAG B650 TOMAHAWK WIFI DDR5', N'msi-mag-b650-tomahawk-wifi-ddr5',
 5990000, 6490000, 36,
 N'{"Socket":"AM5","Chipset":"B650","RAM":"DDR5","Khe RAM":"4","PCIe":"4.0 x16","M.2":"3 khe","WiFi":"6E","Bluetooth":"5.2","Form factor":"ATX"}',
 7, GETUTCDATE(), 0),

-- RAM variants (Corsair có 2 dung lượng)
(7, 7, N'RAM-COR-DDR5-16GB', N'Corsair Vengeance DDR5 16GB (2x8GB) 5600MHz', N'corsair-vengeance-ddr5-16gb-5600',
 1790000, 1990000, 36,
 N'{"Dung lượng":"16GB (2×8GB)","Loại":"DDR5","Xung nhịp":"5600MHz","Latency":"CL36","Điện áp":"1.25V","XMP":"XMP 3.0","Màu":"Đen"}',
 20, GETUTCDATE(), 0),

(8, 7, N'RAM-COR-DDR5-32GB', N'Corsair Vengeance DDR5 32GB (2x16GB) 5600MHz', N'corsair-vengeance-ddr5-32gb-5600',
 3290000, 3690000, 36,
 N'{"Dung lượng":"32GB (2×16GB)","Loại":"DDR5","Xung nhịp":"5600MHz","Latency":"CL36","Điện áp":"1.25V","XMP":"XMP 3.0","Màu":"Đen"}',
 15, GETUTCDATE(), 0),

(9, 8, N'RAM-SAM-DDR5-16GB', N'Samsung DDR5 16GB 5600MHz', N'samsung-ddr5-16gb-5600',
 1590000, 1790000, 36,
 N'{"Dung lượng":"16GB","Loại":"DDR5","Xung nhịp":"5600MHz","Latency":"CL40","Điện áp":"1.1V","Form":"UDIMM"}',
 25, GETUTCDATE(), 0),

(10, 9, N'RAM-KING-DDR4-32GB', N'Kingston FURY Beast DDR4 32GB (2x16GB) 3600MHz', N'kingston-fury-beast-ddr4-32gb-3600',
 2190000, 2490000, 36,
 N'{"Dung lượng":"32GB (2×16GB)","Loại":"DDR4","Xung nhịp":"3600MHz","Latency":"CL18","Điện áp":"1.35V","XMP":"XMP 2.0","Màu":"Đen"}',
 18, GETUTCDATE(), 0),

-- HDD variants
(11, 10, N'HDD-ST-BARRA-2TB', N'Seagate Barracuda 2TB 7200RPM', N'seagate-barracuda-2tb-7200rpm',
 1390000, 1490000, 24,
 N'{"Dung lượng":"2TB","Giao tiếp":"SATA III 6Gb/s","Tốc độ quay":"7200 RPM","Cache":"256MB","Form factor":"3.5 inch"}',
 30, GETUTCDATE(), 0),

(12, 11, N'HDD-WD-BLUE-4TB', N'WD Blue 4TB 5400RPM', N'wd-blue-4tb-5400rpm',
 2190000, 2390000, 24,
 N'{"Dung lượng":"4TB","Giao tiếp":"SATA III 6Gb/s","Tốc độ quay":"5400 RPM","Cache":"256MB","Form factor":"3.5 inch"}',
 20, GETUTCDATE(), 0),

(13, 12, N'HDD-ST-IRONWOLF-4TB', N'Seagate IronWolf 4TB NAS 5400RPM', N'seagate-ironwolf-4tb-nas',
 2490000, 2690000, 36,
 N'{"Dung lượng":"4TB","Giao tiếp":"SATA III 6Gb/s","Tốc độ quay":"5400 RPM","Cache":"256MB","Dùng cho":"NAS 24/7"}',
 15, GETUTCDATE(), 0),

-- SSD variants (Samsung 980 Pro có 2 dung lượng)
(14, 13, N'SSD-SAM-980P-1TB', N'Samsung 980 Pro 1TB PCIe 4.0 NVMe', N'samsung-980-pro-1tb-nvme',
 2290000, 2690000, 36,
 N'{"Dung lượng":"1TB","Giao tiếp":"PCIe 4.0 x4 NVMe","Form factor":"M.2 2280","Đọc tối đa":"7000 MB/s","Ghi tối đa":"5000 MB/s","TBW":"600 TBW"}',
 20, GETUTCDATE(), 0),

(15, 13, N'SSD-SAM-980P-2TB', N'Samsung 980 Pro 2TB PCIe 4.0 NVMe', N'samsung-980-pro-2tb-nvme',
 4390000, 4990000, 36,
 N'{"Dung lượng":"2TB","Giao tiếp":"PCIe 4.0 x4 NVMe","Form factor":"M.2 2280","Đọc tối đa":"7000 MB/s","Ghi tối đa":"6900 MB/s","TBW":"1200 TBW"}',
 12, GETUTCDATE(), 0),

(16, 14, N'SSD-WD-SN850X-1TB', N'WD Black SN850X 1TB PCIe 4.0 NVMe', N'wd-black-sn850x-1tb-nvme',
 2490000, 2890000, 60,
 N'{"Dung lượng":"1TB","Giao tiếp":"PCIe 4.0 x4 NVMe","Form factor":"M.2 2280","Đọc tối đa":"7300 MB/s","Ghi tối đa":"6300 MB/s","TBW":"600 TBW"}',
 15, GETUTCDATE(), 0),

(17, 15, N'SSD-KING-KC3000-2TB', N'Kingston KC3000 2TB PCIe 4.0 NVMe', N'kingston-kc3000-2tb-nvme',
 3990000, 4490000, 60,
 N'{"Dung lượng":"2TB","Giao tiếp":"PCIe 4.0 x4 NVMe","Form factor":"M.2 2280","Đọc tối đa":"7000 MB/s","Ghi tối đa":"7000 MB/s","TBW":"1600 TBW"}',
 10, GETUTCDATE(), 0),

-- GPU variants
(18, 16, N'GPU-ASUS-TUF-RTX4070', N'ASUS TUF Gaming RTX 4070 OC 12GB GDDR6X', N'asus-tuf-rtx-4070-oc-12gb',
 16990000, 18990000, 36,
 N'{"Chip":"NVIDIA GeForce RTX 4070","VRAM":"12GB GDDR6X","Bus":"192-bit","Xung Boost":"2580 MHz","CUDA Cores":"5888","TDP":"200W","Cổng ra":"HDMI 2.1 + 3×DP 1.4a","PCIe":"4.0 x16"}',
 8, GETUTCDATE(), 0),

(19, 17, N'GPU-GIGA-RTX4060TI', N'Gigabyte RTX 4060 Ti Gaming OC 8GB GDDR6', N'gigabyte-rtx-4060ti-gaming-oc-8gb',
 11490000, 12990000, 36,
 N'{"Chip":"NVIDIA GeForce RTX 4060 Ti","VRAM":"8GB GDDR6","Bus":"128-bit","Xung Boost":"2610 MHz","CUDA Cores":"4352","TDP":"160W","Cổng ra":"HDMI 2.1 + 3×DP 1.4a","PCIe":"4.0 x16"}',
 10, GETUTCDATE(), 0),

(20, 18, N'GPU-MSI-RX7800XT', N'MSI MECH RX 7800 XT 16GB GDDR6', N'msi-mech-rx-7800xt-16gb',
 12990000, 14490000, 36,
 N'{"Chip":"AMD Radeon RX 7800 XT","VRAM":"16GB GDDR6","Bus":"256-bit","Xung Game":"2430 MHz","Compute Units":"60","TDP":"263W","Cổng ra":"HDMI 2.1 + 3×DP 2.1","PCIe":"4.0 x16"}',
 7, GETUTCDATE(), 0),

-- PSU variants
(21, 19, N'PSU-COR-RM850X', N'Corsair RM850x 850W 80+ Gold Full Modular', N'corsair-rm850x-850w-gold',
 3290000, 3690000, 84,
 N'{"Công suất":"850W","Hiệu suất":"80+ Gold","Modular":"Full Modular","Fan":"135mm Zero RPM Mode","Bảo hành":"7 năm","PCIe 5.0":"2×16-pin 12VHPWR"}',
 12, GETUTCDATE(), 0),

(22, 20, N'PSU-SEA-GX850', N'Seasonic Focus GX-850 850W 80+ Gold Full Modular', N'seasonic-focus-gx-850-gold',
 3490000, 3990000, 120,
 N'{"Công suất":"850W","Hiệu suất":"80+ Gold","Modular":"Full Modular","Fan":"120mm Fluid Dynamic","Bảo hành":"10 năm"}',
 10, GETUTCDATE(), 0),

(23, 21, N'PSU-BQ-SP11-750', N'be quiet! Straight Power 11 750W 80+ Gold', N'bequiet-straight-power-11-750w-gold',
 2890000, 3290000, 60,
 N'{"Công suất":"750W","Hiệu suất":"80+ Gold","Modular":"Full Modular","Fan":"135mm SilentWings 3","Tiếng ồn":"<14 dBA","Bảo hành":"5 năm"}',
 8, GETUTCDATE(), 0),

-- Case variants
(24, 22, N'CASE-COR-4000D-BLK', N'Corsair 4000D Airflow Mid-Tower ATX (Đen)', N'corsair-4000d-airflow-black',
 1990000, 2290000, 24,
 N'{"Form factor":"ATX Mid-Tower","Fan đi kèm":"2×120mm","Fan tối đa":"6×120mm hoặc 3×140mm","Radiator":"360mm","GPU dài tối đa":"360mm","Màu":"Đen"}',
 15, GETUTCDATE(), 0),

(25, 23, N'CASE-CM-TD500-BLK', N'Cooler Master TD500 Mesh V2 ATX (Đen)', N'coolermaster-td500-mesh-v2-black',
 1790000, 2090000, 24,
 N'{"Form factor":"ATX Mid-Tower","Fan đi kèm":"5×120mm ARGB","Fan tối đa":"6×120mm","Radiator":"360mm","Mặt trước":"Lưới thép","Màu":"Đen"}',
 12, GETUTCDATE(), 0),

(26, 24, N'CASE-ASUS-GT502-BLK', N'ASUS TUF Gaming GT502 ATX (Đen)', N'asus-tuf-gt502-black',
 2490000, 2890000, 24,
 N'{"Form factor":"ATX Mid-Tower","Fan đi kèm":"6×120mm ARGB","Radiator":"360mm trước/trên","GPU dài tối đa":"400mm","CPU cao tối đa":"170mm","Màu":"Đen"}',
 8, GETUTCDATE(), 0),

-- Fan Case variants
(27, 25, N'FAN-DC-FC120-3P', N'DeepCool FC120 ARGB 3-pack + Hub', N'deepcool-fc120-argb-3pack-hub',
 890000, 990000, 12,
 N'{"Số lượng":"3 cái + Hub","Kích thước":"120×120×25mm","Đèn":"ARGB","Tốc độ":"500–1800 RPM","Lưu lượng khí":"56.5 CFM","Kết nối":"3-pin ARGB + 4-pin PWM"}',
 25, GETUTCDATE(), 0),

(28, 26, N'FAN-CM-SF120-ARGB', N'Cooler Master SickleFlow 120 ARGB (1 cái)', N'coolermaster-sickleflow-120-argb-1pc',
 190000, 220000, 12,
 N'{"Số lượng":"1 cái","Kích thước":"120×120×25mm","Đèn":"ARGB","Tốc độ":"650–1800 RPM","Lưu lượng khí":"62 CFM","Vòng bi":"FDB"}',
 40, GETUTCDATE(), 0),

(29, 27, N'FAN-COR-LL120-3P', N'Corsair LL120 RGB 3-pack + Lighting Node Core', N'corsair-ll120-rgb-3pack-node',
 1590000, 1890000, 24,
 N'{"Số lượng":"3 cái + Lighting Node Core","Kích thước":"120×120×25mm","Đèn":"RGB 16 LED dual-ring","Tốc độ":"600–1500 RPM","PWM":"Có"}',
 15, GETUTCDATE(), 0),

-- Monitor variants
(30, 28, N'MON-LG-27GP850', N'LG 27GP850-B 27" QHD 165Hz IPS', N'lg-27gp850-b-27-qhd-165hz',
 7490000, 8490000, 36,
 N'{"Kích thước":"27 inch","Độ phân giải":"2560×1440 (QHD)","Tần số quét":"165Hz","Thời gian phản hồi":"1ms GtG","Tấm nền":"IPS","HDR":"HDR10","Sync":"FreeSync Premium"}',
 8, GETUTCDATE(), 0),

(31, 29, N'MON-MSI-MAG274', N'MSI MAG274QRF-QD 27" QHD 165Hz IPS', N'msi-mag274qrf-qd-27-qhd-165hz',
 8490000, 9490000, 36,
 N'{"Kích thước":"27 inch","Độ phân giải":"2560×1440 (QHD)","Tần số quét":"165Hz","Thời gian phản hồi":"1ms","Tấm nền":"IPS","Công nghệ":"Quantum Dot","Sync":"G-Sync Compatible"}',
 6, GETUTCDATE(), 0),

(32, 30, N'MON-ASUS-PG279QM', N'ASUS ROG Swift PG279QM 27" QHD 240Hz G-Sync', N'asus-rog-pg279qm-27-qhd-240hz',
 14990000, 16990000, 36,
 N'{"Kích thước":"27 inch","Độ phân giải":"2560×1440 (QHD)","Tần số quét":"240Hz","Thời gian phản hồi":"1ms GtG","Tấm nền":"IPS","HDR":"HDR400","Sync":"NVIDIA G-Sync"}',
 5, GETUTCDATE(), 0),

-- Mouse variants
(33, 31, N'MOUSE-LG-GPX2-BLK', N'Logitech G Pro X Superlight 2 (Đen)', N'logitech-g-pro-x-superlight2-black',
 2890000, 3290000, 24,
 N'{"Kết nối":"LIGHTSPEED 2.4GHz + USB-C","Sensor":"HERO 2 25K DPI","Cân nặng":"60g","Pin":"95 giờ","Nút":"5 nút","Màu":"Đen"}',
 15, GETUTCDATE(), 0),

(34, 32, N'MOUSE-RAZER-DAV3PRO-BLK', N'Razer DeathAdder V3 Pro (Đen)', N'razer-deathadder-v3-pro-black',
 3290000, 3690000, 24,
 N'{"Kết nối":"2.4GHz HyperSpeed + USB-C","Sensor":"Focus Pro 30K DPI","Cân nặng":"64g","Pin":"90 giờ","Nút":"6 nút","Màu":"Đen","Ergonomic":"Tay phải"}',
 12, GETUTCDATE(), 0),

(35, 33, N'MOUSE-LG-G502X-BLK', N'Logitech G502 X Plus (Đen)', N'logitech-g502-x-plus-black',
 2490000, 2890000, 24,
 N'{"Kết nối":"LIGHTSPEED 2.4GHz + USB-C","Sensor":"HERO 25K DPI","Cân nặng":"106g","Pin":"130 giờ","Nút":"13 nút","Màu":"Đen","Trọng lượng tùy chỉnh":"Có"}',
 10, GETUTCDATE(), 0),

-- Keyboard variants
(36, 34, N'KB-LG-GPXTKL-BLK', N'Logitech G Pro X TKL Rapid (Đen)', N'logitech-g-pro-x-tkl-rapid-black',
 3490000, 3990000, 24,
 N'{"Kết nối":"LIGHTSPEED 2.4GHz + USB-C","Layout":"TKL 87%","Switch":"GX Optical","Đèn":"RGB per-key","Pin":"30 giờ không đèn","Màu":"Đen"}',
 10, GETUTCDATE(), 0),

(37, 35, N'KB-RAZER-BW-V4PRO-BLK', N'Razer BlackWidow V4 Pro (Đen)', N'razer-blackwidow-v4-pro-black',
 3990000, 4490000, 24,
 N'{"Kết nối":"HyperSpeed 2.4GHz + USB-C + BT","Layout":"Full-size 100%","Switch":"Razer Green Mechanical","Đèn":"Chroma RGB per-key","Pin":"200 giờ không đèn","Macro keys":"6 phím"}',
 8, GETUTCDATE(), 0),

(38, 36, N'KB-MSI-GK71-RED', N'MSI Vigor GK71 Sonic Red (Có dây)', N'msi-vigor-gk71-sonic-red',
 1490000, 1690000, 24,
 N'{"Kết nối":"USB-A","Layout":"Full-size 100%","Switch":"MSI Sonic Red (tuyến tính)","Đèn":"RGB per-key","Form factor":"Full-size","Màu":"Đen"}',
 20, GETUTCDATE(), 0),

-- Tản nhiệt khí variants
(39, 37, N'COOL-NOCTUA-NHD15', N'Noctua NH-D15 Dual Tower (Màu nâu)', N'noctua-nh-d15-brown',
 2090000, 2290000, 72,
 N'{"Loại":"Dual Tower","Fan":"2×NF-A15 140mm","Tốc độ fan":"300–1500 RPM","TDP":"250W+","Chiều cao":"165mm","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4","Tiếng ồn":"24.6 dBA"}',
 12, GETUTCDATE(), 0),

(40, 38, N'COOL-CM-H212EVO-V2', N'Cooler Master Hyper 212 EVO V2 ARGB', N'coolermaster-hyper-212-evo-v2-argb',
 790000, 890000, 24,
 N'{"Loại":"Single Tower","Fan":"1×120mm ARGB PWM","Tốc độ fan":"650–2000 RPM","TDP":"150W","Chiều cao":"158.8mm","Ống đồng":"4","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4"}',
 20, GETUTCDATE(), 0),

(41, 39, N'COOL-DC-AK620', N'DeepCool AK620 Dual Tower', N'deepcool-ak620-dual-tower',
 1190000, 1390000, 36,
 N'{"Loại":"Dual Tower","Fan":"2×120mm PWM","Tốc độ fan":"500–1850 RPM","TDP":"260W","Chiều cao":"160mm","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4","Tiếng ồn":"28 dBA"}',
 15, GETUTCDATE(), 0),

-- AIO variants
(42, 40, N'AIO-COR-H150I-360', N'Corsair iCUE H150i Elite CAPELLIX 360mm', N'corsair-h150i-elite-capellix-360',
 5490000, 6290000, 60,
 N'{"Loại":"AIO Liquid Cooler","Radiator":"360mm","Fan":"3×LL120 RGB","Tốc độ bơm":"2400 RPM","Đầu bơm":"CAPELLIX LED","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4"}',
 8, GETUTCDATE(), 0),

(43, 41, N'AIO-DC-LT720-360', N'DeepCool LT720 360mm LCD AIO', N'deepcool-lt720-360-lcd',
 3990000, 4490000, 36,
 N'{"Loại":"AIO Liquid Cooler","Radiator":"360mm","Fan":"3×120mm ARGB","LCD":"2 inch IPS display","Tốc độ bơm":"2800 RPM","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4"}',
 10, GETUTCDATE(), 0),

(44, 42, N'AIO-CM-ML360L-V2', N'Cooler Master MasterLiquid 360L ARGB V2', N'coolermaster-ml360l-argb-v2',
 2290000, 2690000, 24,
 N'{"Loại":"AIO Liquid Cooler","Radiator":"360mm","Fan":"3×120mm ARGB","Đầu bơm":"ARGB ring","Tốc độ bơm":"2400 RPM","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4"}',
 12, GETUTCDATE(), 0),

-- Headset variants
(45, 43, N'HS-LG-GPX2LS-BLK', N'Logitech G Pro X 2 LIGHTSPEED (Đen)', N'logitech-g-pro-x2-lightspeed-black',
 3990000, 4490000, 24,
 N'{"Kết nối":"LIGHTSPEED 2.4GHz","Driver":"50mm PRO-G Graphene","Micro":"Blue VO!CE tháo được","Pin":"50 giờ","Trọng lượng":"345g","Màu":"Đen"}',
 10, GETUTCDATE(), 0),

(46, 44, N'HS-RAZER-BSV2PRO-BLK', N'Razer BlackShark V2 Pro (Đen)', N'razer-blackshark-v2-pro-black',
 3390000, 3890000, 24,
 N'{"Kết nối":"2.4GHz + 3.5mm","Driver":"50mm TriForce Titanium","Micro":"HyperClear Super Wideband","Âm thanh":"THX Spatial Audio","Pin":"70 giờ","Trọng lượng":"320g"}',
 10, GETUTCDATE(), 0),

-- Software variants
(47, 45, N'SW-WIN11-HOME-ESD', N'Windows 11 Home 64-bit (ESD - Key điện tử)', N'ms-windows-11-home-esd',
 3490000, 3990000, 0,
 N'{"Phiên bản":"Windows 11 Home","Kiến trúc":"64-bit","License":"ESD vĩnh viễn","Số PC":"1","Kèm theo":"Key kích hoạt qua email"}',
 999, GETUTCDATE(), 0),

(48, 46, N'SW-OFFICE2024-HS-ESD', N'Microsoft Office 2024 Home & Student (ESD)', N'ms-office-2024-home-student-esd',
 2990000, 3490000, 0,
 N'{"Phiên bản":"Office 2024","Ứng dụng":"Word, Excel, PowerPoint, OneNote","License":"ESD vĩnh viễn","Số PC":"1","Hệ điều hành":"Windows 10/11 hoặc macOS"}',
 999, GETUTCDATE(), 0),

-- PC Build sẵn variants
(49, 47, N'PC-GAMING-MID-16GB', N'PC Gaming RTX 4060 Ti - i5-13600K / 16GB DDR5', N'pc-gaming-tam-trung-rtx4060ti-16gb',
 26990000, 29990000, 12,
 N'{"CPU":"Intel Core i5-13600K","Mainboard":"MSI B660M MORTAR WIFI","RAM":"16GB DDR5 5600MHz","GPU":"RTX 4060 Ti 8GB","SSD":"Samsung 980 Pro 1TB","PSU":"Corsair RM650x 650W Gold","Case":"Corsair 4000D Airflow","Tản nhiệt":"Cooler Master Hyper 212 EVO"}',
 3, GETUTCDATE(), 0),

(50, 47, N'PC-GAMING-MID-32GB', N'PC Gaming RTX 4060 Ti - i5-13600K / 32GB DDR5', N'pc-gaming-tam-trung-rtx4060ti-32gb',
 30990000, 33990000, 12,
 N'{"CPU":"Intel Core i5-13600K","Mainboard":"MSI B660M MORTAR WIFI","RAM":"32GB DDR5 5600MHz","GPU":"RTX 4060 Ti 8GB","SSD":"Samsung 980 Pro 1TB","PSU":"Corsair RM650x 650W Gold","Case":"Corsair 4000D Airflow","Tản nhiệt":"Cooler Master Hyper 212 EVO"}',
 2, GETUTCDATE(), 0),

(51, 48, N'PC-GAMING-HIGH-64GB', N'PC Gaming RTX 4090 - i9-13900K / 64GB DDR5', N'pc-gaming-dinh-cao-rtx4090-64gb',
 89990000, 99990000, 12,
 N'{"CPU":"Intel Core i9-13900K","Mainboard":"ASUS ROG MAXIMUS Z790 HERO","RAM":"64GB DDR5 6000MHz","GPU":"ASUS ROG RTX 4090 24GB","SSD":"Samsung 980 Pro 2TB","PSU":"Seasonic PRIME TX-1000W Platinum","Case":"Lian Li O11 Dynamic EVO","Tản nhiệt":"Corsair H150i Elite 360mm AIO"}',
 2, GETUTCDATE(), 0),

(52, 49, N'PC-OFFICE-I5-16GB', N'PC Văn Phòng i5-12400 / 16GB DDR4 / 512GB SSD', N'pc-van-phong-i5-12400-16gb-512ssd',
 11990000, 13490000, 12,
 N'{"CPU":"Intel Core i5-12400","Mainboard":"ASUS PRIME H610M-K","RAM":"16GB DDR4 3200MHz","GPU":"Intel UHD Graphics 730 (tích hợp)","SSD":"512GB NVMe PCIe 3.0","PSU":"650W 80+ Bronze","Case":"Micro-ATX","Ghi chú":"Không kèm màn hình"}',
 5, GETUTCDATE(), 0);

SET IDENTITY_INSERT [ProductVariants] OFF;

PRINT 'Seed data linh kien may tinh va PC Build san da duoc tao thanh cong.';
GO
