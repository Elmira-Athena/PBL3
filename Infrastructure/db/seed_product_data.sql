-- =============================================
-- SEED DATA: LINH KIỆN MÁY TÍNH & PC BUILD SẴN
-- =============================================
-- Category slugs khớp với BuildPc.razor _slots
-- ============================================
-- 0. XÓA DỮ LIỆU CŨ (theo slug, không phụ thuộc ID)
-- ============================================
-- Bảng tạm thay cho `DECLARE @X TABLE` của T-SQL. Cột đặt tên chữ thường (`id`) chứ
-- không phải `"Id"`: đây là bảng của riêng script này, không phải bảng EF, nên theo
-- quy ước mặc định của PostgreSQL cho đỡ phải nháy.
DROP TABLE IF EXISTS root_cat_ids;
CREATE TEMP TABLE root_cat_ids (id int);
INSERT INTO root_cat_ids
    SELECT "Id" FROM "Categories"
    WHERE "Slug" IN ('linh-kien-may-tinh', 'pc-build-san',
                     'man-hinh', 'ban-phim', 'tai-nghe', 'phan-mem');

DROP TABLE IF EXISTS all_cat_ids;
CREATE TEMP TABLE all_cat_ids (id int);
INSERT INTO all_cat_ids
    SELECT "Id" FROM "Categories"
    WHERE "Id" IN (SELECT id FROM root_cat_ids)
       OR "ParentId" IN (SELECT id FROM root_cat_ids);

DELETE FROM "ProductVariants"
WHERE "ProductId" IN (
    SELECT "Id" FROM "Products"
    WHERE "CategoryId" IN (SELECT id FROM all_cat_ids)
);
DELETE FROM "Products"
WHERE "CategoryId" IN (SELECT id FROM all_cat_ids);
DELETE FROM "Categories"
WHERE "Id" IN (SELECT id FROM all_cat_ids);
DELETE FROM "Manufacturers"
WHERE "Name" IN (
    'Intel', 'AMD', 'NVIDIA', 'Samsung', 'Kingston', 'Corsair',
    'ASUS', 'Gigabyte', 'MSI', 'Seagate', 'Western Digital',
    'Cooler Master', 'Noctua', 'be quiet!', 'Seasonic', 'LG',
    'Logitech', 'Razer', 'DeepCool', 'Microsoft', 'HushStore Custom'
);

-- ============================================
-- 1. MANUFACTURERS
-- ============================================

INSERT INTO "Manufacturers" ("Id", "Name", "Website", "SupportEmail", "CreatedDate", "IsDeleted") VALUES
( 1, 'Intel',               'https://www.intel.com',          'support@intel.com',        (now() AT TIME ZONE 'utc'), false),
( 2, 'AMD',                 'https://www.amd.com',            'support@amd.com',           (now() AT TIME ZONE 'utc'), false),
( 3, 'NVIDIA',              'https://www.nvidia.com',         'support@nvidia.com',        (now() AT TIME ZONE 'utc'), false),
( 4, 'Samsung',             'https://www.samsung.com',        'support@samsung.com',       (now() AT TIME ZONE 'utc'), false),
( 5, 'Kingston',            'https://www.kingston.com',       'support@kingston.com',      (now() AT TIME ZONE 'utc'), false),
( 6, 'Corsair',             'https://www.corsair.com',        'support@corsair.com',       (now() AT TIME ZONE 'utc'), false),
( 7, 'ASUS',                'https://www.asus.com',           'support@asus.com',          (now() AT TIME ZONE 'utc'), false),
( 8, 'Gigabyte',            'https://www.gigabyte.com',       'support@gigabyte.com',      (now() AT TIME ZONE 'utc'), false),
( 9, 'MSI',                 'https://www.msi.com',            'support@msi.com',           (now() AT TIME ZONE 'utc'), false),
(10, 'Seagate',             'https://www.seagate.com',        'support@seagate.com',       (now() AT TIME ZONE 'utc'), false),
(11, 'Western Digital',     'https://www.westerndigital.com', 'support@wd.com',            (now() AT TIME ZONE 'utc'), false),
(12, 'Cooler Master',       'https://www.coolermaster.com',   'support@coolermaster.com',  (now() AT TIME ZONE 'utc'), false),
(13, 'Noctua',              'https://noctua.at',              'support@noctua.at',         (now() AT TIME ZONE 'utc'), false),
(14, 'be quiet!',           'https://www.bequiet.com',        'support@bequiet.com',       (now() AT TIME ZONE 'utc'), false),
(15, 'Seasonic',            'https://www.seasonic.com',       'support@seasonic.com',      (now() AT TIME ZONE 'utc'), false),
(16, 'LG',                  'https://www.lg.com',             'support@lg.com',            (now() AT TIME ZONE 'utc'), false),
(17, 'Logitech',            'https://www.logitech.com',       'support@logitech.com',      (now() AT TIME ZONE 'utc'), false),
(18, 'Razer',               'https://www.razer.com',          'support@razer.com',         (now() AT TIME ZONE 'utc'), false),
(19, 'DeepCool',            'https://www.deepcool.com',       'support@deepcool.com',      (now() AT TIME ZONE 'utc'), false),
(20, 'Microsoft',           'https://www.microsoft.com',      'support@microsoft.com',     (now() AT TIME ZONE 'utc'), false),
(21, 'HushStore Custom',    NULL,                              'build@hushstore.com',       (now() AT TIME ZONE 'utc'), false);

-- ============================================
-- 2. CATEGORIES
-- ============================================

-- Root: linh kiện
INSERT INTO "Categories" ("Id", "Name", "Slug", "ParentId", "Level", "SortOrder", "IsVisible", "CreatedDate", "IsDeleted") VALUES
(100, 'Linh kiện máy tính', 'linh-kien-may-tinh', NULL, 0, 1, true, (now() AT TIME ZONE 'utc'), false);

-- Sub-categories linh kiện — slugs PHẢI khớp CategorySlug trong BuildPc.razor
INSERT INTO "Categories" ("Id", "Name", "Slug", "ParentId", "Level", "SortOrder", "IsVisible", "CreatedDate", "IsDeleted") VALUES
(101, 'CPU - Bộ vi xử lý',       'cpu',                100, 1,  1, true, (now() AT TIME ZONE 'utc'), false),
(102, 'Bo mạch chủ (Mainboard)', 'bo-mach-chu',        100, 1,  2, true, (now() AT TIME ZONE 'utc'), false),
(103, 'RAM - Bộ nhớ trong',      'ram',                100, 1,  3, true, (now() AT TIME ZONE 'utc'), false),
(104, 'Ổ cứng HDD',              'hdd',                100, 1,  4, true, (now() AT TIME ZONE 'utc'), false),
(105, 'Ổ cứng SSD',              'ssd',                100, 1,  5, true, (now() AT TIME ZONE 'utc'), false),
(106, 'Card màn hình (VGA)',      'vga',                100, 1,  6, true, (now() AT TIME ZONE 'utc'), false),
(107, 'Nguồn máy tính (PSU)',     'nguon',              100, 1,  7, true, (now() AT TIME ZONE 'utc'), false),
(108, 'Vỏ Case máy tính',        'vo-case',            100, 1,  8, true, (now() AT TIME ZONE 'utc'), false),
(109, 'Fan Case',                'fan-case',           100, 1,  9, true, (now() AT TIME ZONE 'utc'), false),
(111, 'Chuột máy tính',          'chuot',              100, 1, 10, true, (now() AT TIME ZONE 'utc'), false),
(113, 'Tản nhiệt khí',           'tan-nhiet-khi',      100, 1, 11, true, (now() AT TIME ZONE 'utc'), false),
(114, 'Tản nhiệt nước AIO',      'tan-nhiet-nuoc-aio', 100, 1, 12, true, (now() AT TIME ZONE 'utc'), false);

-- Root categories độc lập
INSERT INTO "Categories" ("Id", "Name", "Slug", "ParentId", "Level", "SortOrder", "IsVisible", "CreatedDate", "IsDeleted") VALUES
(110, 'Màn hình máy tính', 'man-hinh',   NULL, 0, 3, true, (now() AT TIME ZONE 'utc'), false),
(112, 'Bàn phím máy tính', 'ban-phim',   NULL, 0, 4, true, (now() AT TIME ZONE 'utc'), false),
(115, 'Tai nghe',          'tai-nghe',   NULL, 0, 5, true, (now() AT TIME ZONE 'utc'), false),
(116, 'Phần mềm',          'phan-mem',   NULL, 0, 6, true, (now() AT TIME ZONE 'utc'), false),
(200, 'PC Build sẵn',      'pc-build-san',NULL, 0, 7, true, (now() AT TIME ZONE 'utc'), false);

-- ============================================
-- 3. PRODUCTS
-- ============================================

-- CPU (101)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
( 1, 'Intel Core i5-13600K',  'intel-core-i5-13600k', 'CPU Intel Raptor Lake 14 nhân 20 luồng, socket LGA1700', 1, 101, 1, (now() AT TIME ZONE 'utc'), false),
( 2, 'Intel Core i7-13700K',  'intel-core-i7-13700k', 'CPU Intel Raptor Lake 16 nhân 24 luồng, socket LGA1700', 1, 101, 1, (now() AT TIME ZONE 'utc'), false),
( 3, 'AMD Ryzen 5 7600X',     'amd-ryzen-5-7600x',    'CPU AMD Zen4 6 nhân 12 luồng, socket AM5, PCIe 5.0',    2, 101, 1, (now() AT TIME ZONE 'utc'), false);

-- Mainboard (102)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
( 4, 'ASUS ROG STRIX Z790-E Gaming WiFi',  'asus-rog-strix-z790-e',        'Mainboard Z790 DDR5 cao cấp, PCIe 5.0, WiFi 6E',       7, 102, 1, (now() AT TIME ZONE 'utc'), false),
( 5, 'Gigabyte Z790 AORUS Elite AX',       'gigabyte-z790-aorus-elite-ax', 'Mainboard Z790 DDR5, WiFi 6E, Bluetooth 5.2',           8, 102, 1, (now() AT TIME ZONE 'utc'), false),
( 6, 'MSI MAG B650 TOMAHAWK WIFI',         'msi-mag-b650-tomahawk-wifi',   'Mainboard B650 cho AM5, DDR5, WiFi 6E, giá tốt',       9, 102, 1, (now() AT TIME ZONE 'utc'), false);

-- RAM (103)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
( 7, 'Corsair Vengeance DDR5',          'corsair-vengeance-ddr5',       'RAM DDR5 5600MHz, tản nhiệt nhôm anodized, XMP 3.0',   6, 103, 1, (now() AT TIME ZONE 'utc'), false),
( 8, 'Samsung DDR5 5600MHz',            'samsung-ddr5-5600',            'RAM DDR5 Samsung chính hãng, hiệu năng ổn định',       4, 103, 1, (now() AT TIME ZONE 'utc'), false),
( 9, 'Kingston FURY Beast DDR4 3600MHz','kingston-fury-beast-ddr4',     'RAM DDR4 3600MHz, tương thích Intel/AMD, XMP 2.0',     5, 103, 1, (now() AT TIME ZONE 'utc'), false);

-- HDD (104)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(10, 'Seagate Barracuda 2TB',   'seagate-barracuda-2tb',  'HDD 3.5" 2TB 7200 RPM SATA III, cache 256MB', 10, 104, 1, (now() AT TIME ZONE 'utc'), false),
(11, 'Western Digital Blue 4TB','wd-blue-4tb',            'HDD 3.5" 4TB 5400 RPM SATA III, đáng tin cậy',11, 104, 1, (now() AT TIME ZONE 'utc'), false),
(12, 'Seagate IronWolf 4TB',    'seagate-ironwolf-4tb',   'HDD NAS 3.5" 4TB, tối ưu cho NAS 24/7',       10, 104, 1, (now() AT TIME ZONE 'utc'), false);

-- SSD (105)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(13, 'Samsung 980 Pro M.2 NVMe',    'samsung-980-pro',       'SSD PCIe 4.0 NVMe, đọc 7000MB/s, DRAM cache',  4, 105, 1, (now() AT TIME ZONE 'utc'), false),
(14, 'WD Black SN850X M.2 NVMe',    'wd-black-sn850x',       'SSD PCIe 4.0 NVMe, đọc 7300MB/s, bảo hành 5 năm',11, 105, 1, (now() AT TIME ZONE 'utc'), false),
(15, 'Kingston KC3000 M.2 NVMe',    'kingston-kc3000',       'SSD PCIe 4.0 NVMe, đọc 7000MB/s, bảo hành 5 năm', 5, 105, 1, (now() AT TIME ZONE 'utc'), false);

-- VGA (106)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(16, 'ASUS TUF Gaming RTX 4070 OC',      'asus-tuf-rtx-4070-oc',         'RTX 4070 12GB GDDR6X, 3 quạt AXIAL-tech, DLSS 3',  7, 106, 1, (now() AT TIME ZONE 'utc'), false),
(17, 'Gigabyte RTX 4060 Ti Gaming OC',   'gigabyte-rtx-4060ti-gaming-oc','RTX 4060 Ti 8GB GDDR6, WINDFORCE 3X, Ray Tracing',  8, 106, 1, (now() AT TIME ZONE 'utc'), false),
(18, 'MSI MECH Radeon RX 7800 XT 16G',  'msi-rx-7800xt-mech',           'RX 7800 XT 16GB GDDR6, RDNA3, tốt cho 1440p',       9, 106, 1, (now() AT TIME ZONE 'utc'), false);

-- PSU (107)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(19, 'Corsair RM850x 850W Gold',         'corsair-rm850x-850w',          'PSU 850W 80+ Gold, Full Modular, fanless tải thấp',  6, 107, 1, (now() AT TIME ZONE 'utc'), false),
(20, 'Seasonic Focus GX-850 850W',       'seasonic-focus-gx-850',        'PSU 850W 80+ Gold, Full Modular, bảo hành 10 năm',  15, 107, 1, (now() AT TIME ZONE 'utc'), false),
(21, 'be quiet! Straight Power 11 750W', 'bequiet-straight-power-11-750w','PSU 750W 80+ Gold, siêu im lặng, Full Modular',    14, 107, 1, (now() AT TIME ZONE 'utc'), false);

-- Case (108)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(22, 'Corsair 4000D Airflow ATX',        'corsair-4000d-airflow',        'Case Mid-Tower ATX, airflow tối ưu, 2 fan 120mm',   6, 108, 1, (now() AT TIME ZONE 'utc'), false),
(23, 'Cooler Master MasterBox TD500 Mesh','coolermaster-td500-mesh',     'Case ATX lưới thép, 5 fan ARGB, kính cường lực',    12, 108, 1, (now() AT TIME ZONE 'utc'), false),
(24, 'ASUS TUF Gaming GT502',            'asus-tuf-gaming-gt502',        'Case ATX, 6 fan ARGB, hỗ trợ 360mm AIO',            7, 108, 1, (now() AT TIME ZONE 'utc'), false);

-- Fan Case (109)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(25, 'DeepCool FC120 ARGB 3-pack',       'deepcool-fc120-argb-3pack',    'Bộ 3 fan 120mm ARGB kèm Hub Controller',            19, 109, 1, (now() AT TIME ZONE 'utc'), false),
(26, 'Cooler Master SickleFlow 120 ARGB','coolermaster-sickleflow-120',  'Fan 120mm ARGB, vòng bi FDB, tiếng ồn thấp',        12, 109, 1, (now() AT TIME ZONE 'utc'), false),
(27, 'Corsair LL120 RGB 3-pack',         'corsair-ll120-rgb-3pack',      'Bộ 3 fan 120mm RGB 16 LED, kèm Lighting Node Core',  6, 109, 1, (now() AT TIME ZONE 'utc'), false);

-- Màn hình (110)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(28, 'LG 27GP850-B 27" QHD 165Hz',      'lg-27gp850-b',                 'IPS 27" 2560x1440 165Hz 1ms, FreeSync Premium',     16, 110, 1, (now() AT TIME ZONE 'utc'), false),
(29, 'MSI MAG274QRF-QD 27" 165Hz',      'msi-mag274qrf-qd',             'IPS 27" QHD 165Hz Quantum Dot, G-Sync Compatible',   9, 110, 1, (now() AT TIME ZONE 'utc'), false),
(30, 'ASUS ROG Swift PG279QM 27" 240Hz','asus-rog-pg279qm',             'IPS 27" QHD 240Hz G-Sync, HDR400, cao cấp',          7, 110, 1, (now() AT TIME ZONE 'utc'), false);

-- Chuột (111)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(31, 'Logitech G Pro X Superlight 2',   'logitech-g-pro-x-superlight-2','Chuột gaming không dây 60g, HERO 2 25K DPI',         17, 111, 1, (now() AT TIME ZONE 'utc'), false),
(32, 'Razer DeathAdder V3 Pro',         'razer-deathadder-v3-pro',      'Chuột gaming không dây ergonomic, Focus Pro 30K DPI',18, 111, 1, (now() AT TIME ZONE 'utc'), false),
(33, 'Logitech G502 X Plus',            'logitech-g502-x-plus',         'Chuột gaming không dây có trọng lượng, HERO 25K DPI',17, 111, 1, (now() AT TIME ZONE 'utc'), false);

-- Bàn phím (112)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(34, 'Logitech G Pro X TKL Rapid',      'logitech-g-pro-x-tkl-rapid',   'Bàn phím TKL switch quang học, không dây 2.4GHz',   17, 112, 1, (now() AT TIME ZONE 'utc'), false),
(35, 'Razer BlackWidow V4 Pro',         'razer-blackwidow-v4-pro',      'Bàn phím cơ full-size, Razer Green, RGB Chroma',     18, 112, 1, (now() AT TIME ZONE 'utc'), false),
(36, 'MSI Vigor GK71 Sonic',            'msi-vigor-gk71-sonic',         'Bàn phím cơ gaming, switch đỏ/xanh, RGB per-key',    9, 112, 1, (now() AT TIME ZONE 'utc'), false);

-- Tản nhiệt khí (113)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(37, 'Noctua NH-D15',                   'noctua-nh-d15',                'Tản nhiệt khí dual tower, 2 fan NF-A15, TDP 250W+',  13, 113, 1, (now() AT TIME ZONE 'utc'), false),
(38, 'Cooler Master Hyper 212 EVO V2',  'coolermaster-hyper-212-evo-v2','Tản nhiệt khí 4 ống đồng, fan 120mm ARGB, TDP 150W',12, 113, 1, (now() AT TIME ZONE 'utc'), false),
(39, 'DeepCool AK620',                  'deepcool-ak620',               'Tản nhiệt khí dual tower, 2 fan 120mm, TDP 260W',    19, 113, 1, (now() AT TIME ZONE 'utc'), false);

-- Tản nhiệt nước AIO (114)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(40, 'Corsair iCUE H150i Elite CAPELLIX 360mm','corsair-h150i-elite-360','AIO 360mm, 3 fan LL120 RGB, đầu bơm CAPELLIX LED', 6, 114, 1, (now() AT TIME ZONE 'utc'), false),
(41, 'DeepCool LT720 360mm LCD',        'deepcool-lt720-360',           'AIO 360mm, màn LCD đầu bơm, 3 fan ARGB',             19, 114, 1, (now() AT TIME ZONE 'utc'), false),
(42, 'Cooler Master MasterLiquid 360L ARGB','coolermaster-ml360l-argb', 'AIO 360mm, 3 fan 120mm ARGB, lắp đặt dễ dàng',     12, 114, 1, (now() AT TIME ZONE 'utc'), false);

-- Tai nghe (115)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(43, 'Logitech G Pro X 2 LIGHTSPEED',   'logitech-g-pro-x2-lightspeed', 'Tai nghe gaming không dây, Blue VO!CE mic, 50 giờ', 17, 115, 1, (now() AT TIME ZONE 'utc'), false),
(44, 'Razer BlackShark V2 Pro',         'razer-blackshark-v2-pro',      'Tai nghe gaming không dây, THX Spatial, 70 giờ pin',18, 115, 1, (now() AT TIME ZONE 'utc'), false);

-- Phần mềm (116)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(45, 'Microsoft Windows 11 Home',       'ms-windows-11-home',           'Hệ điều hành bản quyền ESD, 1 PC, nâng cấp free từ Win10',20, 116, 1, (now() AT TIME ZONE 'utc'), false),
(46, 'Microsoft Office 2024 Home & Student','ms-office-2024-home-student','Word, Excel, PowerPoint, OneNote — bản quyền vĩnh viễn',20, 116, 1, (now() AT TIME ZONE 'utc'), false);

-- PC Build sẵn (200)
INSERT INTO "Products" ("Id", "Name", "Slug", "ShortDescription", "ManufacturerId", "CategoryId", "Status", "CreatedDate", "IsDeleted") VALUES
(47, 'PC Gaming Tầm Trung RTX 4060 Ti',  'pc-gaming-tam-trung-rtx-4060ti','i5-13600K + RTX 4060 Ti 8GB + 16GB DDR5 + 1TB NVMe',21, 200, 1, (now() AT TIME ZONE 'utc'), false),
(48, 'PC Gaming Đỉnh Cao RTX 4090',       'pc-gaming-dinh-cao-rtx-4090',   'i9-13900K + RTX 4090 24GB + 64GB DDR5 + 2TB NVMe',  21, 200, 1, (now() AT TIME ZONE 'utc'), false),
(49, 'PC Văn Phòng Doanh Nghiệp',         'pc-van-phong-doanh-nghiep',     'i5-12400 + 16GB DDR4 + 512GB SSD, bền bỉ văn phòng',21, 200, 1, (now() AT TIME ZONE 'utc'), false);

-- ============================================
-- 4. PRODUCT VARIANTS
-- ============================================

-- CPU variants
INSERT INTO "ProductVariants" ("Id", "ProductId", "SKU", "VariantName", "Slug", "Price", "OriginalPrice", "WarrantyMonth", "Specifications", "StockQuantity", "CreatedDate", "IsDeleted") VALUES
(1, 1, 'CPU-I5-13600K-BOX', 'Intel Core i5-13600K Box', 'intel-core-i5-13600k-box',
 7490000, 7990000, 36,
 '{"Socket":"LGA1700","Số nhân":"14 (6P+8E)","Số luồng":"20","Xung cơ bản":"3.5 GHz","Xung tối đa":"5.1 GHz","Cache L3":"24MB","TDP":"125W","Tiến trình":"Intel 7","RAM hỗ trợ":"DDR4/DDR5"}',
 0, (now() AT TIME ZONE 'utc'), false),

(2, 2, 'CPU-I7-13700K-BOX', 'Intel Core i7-13700K Box', 'intel-core-i7-13700k-box',
 10490000, 11490000, 36,
 '{"Socket":"LGA1700","Số nhân":"16 (8P+8E)","Số luồng":"24","Xung cơ bản":"3.4 GHz","Xung tối đa":"5.4 GHz","Cache L3":"30MB","TDP":"125W","Tiến trình":"Intel 7","RAM hỗ trợ":"DDR4/DDR5"}',
 0, (now() AT TIME ZONE 'utc'), false),

(3, 3, 'CPU-R5-7600X-BOX', 'AMD Ryzen 5 7600X Box', 'amd-ryzen-5-7600x-box',
 5990000, 6490000, 36,
 '{"Socket":"AM5","Số nhân":"6","Số luồng":"12","Xung cơ bản":"4.7 GHz","Xung tối đa":"5.3 GHz","Cache L3":"32MB","TDP":"105W","Tiến trình":"TSMC 5nm","RAM hỗ trợ":"DDR5"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Mainboard variants
(4, 4, 'MB-ASUS-Z790-E', 'ASUS ROG STRIX Z790-E Gaming WiFi II', 'asus-rog-strix-z790-e-wifi',
 12990000, 13990000, 36,
 '{"Socket":"LGA1700","Chipset":"Z790","RAM":"DDR5","Khe RAM":"4","PCIe":"5.0 x16","M.2":"5 khe","WiFi":"6E","Bluetooth":"5.3","Form factor":"ATX"}',
 0, (now() AT TIME ZONE 'utc'), false),

(5, 5, 'MB-GIGA-Z790-AX', 'Gigabyte Z790 AORUS Elite AX DDR5', 'gigabyte-z790-aorus-elite-ax-ddr5',
 8490000, 9490000, 36,
 '{"Socket":"LGA1700","Chipset":"Z790","RAM":"DDR5","Khe RAM":"4","PCIe":"5.0 x16","M.2":"4 khe","WiFi":"6E","Bluetooth":"5.2","Form factor":"ATX"}',
 0, (now() AT TIME ZONE 'utc'), false),

(6, 6, 'MB-MSI-B650-TW', 'MSI MAG B650 TOMAHAWK WIFI DDR5', 'msi-mag-b650-tomahawk-wifi-ddr5',
 5990000, 6490000, 36,
 '{"Socket":"AM5","Chipset":"B650","RAM":"DDR5","Khe RAM":"4","PCIe":"4.0 x16","M.2":"3 khe","WiFi":"6E","Bluetooth":"5.2","Form factor":"ATX"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- RAM variants (Corsair có 2 dung lượng)
(7, 7, 'RAM-COR-DDR5-16GB', 'Corsair Vengeance DDR5 16GB (2x8GB) 5600MHz', 'corsair-vengeance-ddr5-16gb-5600',
 1790000, 1990000, 36,
 '{"Dung lượng":"16GB (2×8GB)","Loại":"DDR5","Xung nhịp":"5600MHz","Latency":"CL36","Điện áp":"1.25V","XMP":"XMP 3.0","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

(8, 7, 'RAM-COR-DDR5-32GB', 'Corsair Vengeance DDR5 32GB (2x16GB) 5600MHz', 'corsair-vengeance-ddr5-32gb-5600',
 3290000, 3690000, 36,
 '{"Dung lượng":"32GB (2×16GB)","Loại":"DDR5","Xung nhịp":"5600MHz","Latency":"CL36","Điện áp":"1.25V","XMP":"XMP 3.0","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

(9, 8, 'RAM-SAM-DDR5-16GB', 'Samsung DDR5 16GB 5600MHz', 'samsung-ddr5-16gb-5600',
 1590000, 1790000, 36,
 '{"Dung lượng":"16GB","Loại":"DDR5","Xung nhịp":"5600MHz","Latency":"CL40","Điện áp":"1.1V","Form":"UDIMM"}',
 0, (now() AT TIME ZONE 'utc'), false),

(10, 9, 'RAM-KING-DDR4-32GB', 'Kingston FURY Beast DDR4 32GB (2x16GB) 3600MHz', 'kingston-fury-beast-ddr4-32gb-3600',
 2190000, 2490000, 36,
 '{"Dung lượng":"32GB (2×16GB)","Loại":"DDR4","Xung nhịp":"3600MHz","Latency":"CL18","Điện áp":"1.35V","XMP":"XMP 2.0","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- HDD variants
(11, 10, 'HDD-ST-BARRA-2TB', 'Seagate Barracuda 2TB 7200RPM', 'seagate-barracuda-2tb-7200rpm',
 1390000, 1490000, 24,
 '{"Dung lượng":"2TB","Giao tiếp":"SATA III 6Gb/s","Tốc độ quay":"7200 RPM","Cache":"256MB","Form factor":"3.5 inch"}',
 0, (now() AT TIME ZONE 'utc'), false),

(12, 11, 'HDD-WD-BLUE-4TB', 'WD Blue 4TB 5400RPM', 'wd-blue-4tb-5400rpm',
 2190000, 2390000, 24,
 '{"Dung lượng":"4TB","Giao tiếp":"SATA III 6Gb/s","Tốc độ quay":"5400 RPM","Cache":"256MB","Form factor":"3.5 inch"}',
 0, (now() AT TIME ZONE 'utc'), false),

(13, 12, 'HDD-ST-IRONWOLF-4TB', 'Seagate IronWolf 4TB NAS 5400RPM', 'seagate-ironwolf-4tb-nas',
 2490000, 2690000, 36,
 '{"Dung lượng":"4TB","Giao tiếp":"SATA III 6Gb/s","Tốc độ quay":"5400 RPM","Cache":"256MB","Dùng cho":"NAS 24/7"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- SSD variants (Samsung 980 Pro có 2 dung lượng)
(14, 13, 'SSD-SAM-980P-1TB', 'Samsung 980 Pro 1TB PCIe 4.0 NVMe', 'samsung-980-pro-1tb-nvme',
 2290000, 2690000, 36,
 '{"Dung lượng":"1TB","Giao tiếp":"PCIe 4.0 x4 NVMe","Form factor":"M.2 2280","Đọc tối đa":"7000 MB/s","Ghi tối đa":"5000 MB/s","TBW":"600 TBW"}',
 0, (now() AT TIME ZONE 'utc'), false),

(15, 13, 'SSD-SAM-980P-2TB', 'Samsung 980 Pro 2TB PCIe 4.0 NVMe', 'samsung-980-pro-2tb-nvme',
 4390000, 4990000, 36,
 '{"Dung lượng":"2TB","Giao tiếp":"PCIe 4.0 x4 NVMe","Form factor":"M.2 2280","Đọc tối đa":"7000 MB/s","Ghi tối đa":"6900 MB/s","TBW":"1200 TBW"}',
 0, (now() AT TIME ZONE 'utc'), false),

(16, 14, 'SSD-WD-SN850X-1TB', 'WD Black SN850X 1TB PCIe 4.0 NVMe', 'wd-black-sn850x-1tb-nvme',
 2490000, 2890000, 60,
 '{"Dung lượng":"1TB","Giao tiếp":"PCIe 4.0 x4 NVMe","Form factor":"M.2 2280","Đọc tối đa":"7300 MB/s","Ghi tối đa":"6300 MB/s","TBW":"600 TBW"}',
 0, (now() AT TIME ZONE 'utc'), false),

(17, 15, 'SSD-KING-KC3000-2TB', 'Kingston KC3000 2TB PCIe 4.0 NVMe', 'kingston-kc3000-2tb-nvme',
 3990000, 4490000, 60,
 '{"Dung lượng":"2TB","Giao tiếp":"PCIe 4.0 x4 NVMe","Form factor":"M.2 2280","Đọc tối đa":"7000 MB/s","Ghi tối đa":"7000 MB/s","TBW":"1600 TBW"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- GPU variants
(18, 16, 'GPU-ASUS-TUF-RTX4070', 'ASUS TUF Gaming RTX 4070 OC 12GB GDDR6X', 'asus-tuf-rtx-4070-oc-12gb',
 16990000, 18990000, 36,
 '{"Chip":"NVIDIA GeForce RTX 4070","VRAM":"12GB GDDR6X","Bus":"192-bit","Xung Boost":"2580 MHz","CUDA Cores":"5888","TDP":"200W","Cổng ra":"HDMI 2.1 + 3×DP 1.4a","PCIe":"4.0 x16"}',
 0, (now() AT TIME ZONE 'utc'), false),

(19, 17, 'GPU-GIGA-RTX4060TI', 'Gigabyte RTX 4060 Ti Gaming OC 8GB GDDR6', 'gigabyte-rtx-4060ti-gaming-oc-8gb',
 11490000, 12990000, 36,
 '{"Chip":"NVIDIA GeForce RTX 4060 Ti","VRAM":"8GB GDDR6","Bus":"128-bit","Xung Boost":"2610 MHz","CUDA Cores":"4352","TDP":"160W","Cổng ra":"HDMI 2.1 + 3×DP 1.4a","PCIe":"4.0 x16"}',
 0, (now() AT TIME ZONE 'utc'), false),

(20, 18, 'GPU-MSI-RX7800XT', 'MSI MECH RX 7800 XT 16GB GDDR6', 'msi-mech-rx-7800xt-16gb',
 12990000, 14490000, 36,
 '{"Chip":"AMD Radeon RX 7800 XT","VRAM":"16GB GDDR6","Bus":"256-bit","Xung Game":"2430 MHz","Compute Units":"60","TDP":"263W","Cổng ra":"HDMI 2.1 + 3×DP 2.1","PCIe":"4.0 x16"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- PSU variants
(21, 19, 'PSU-COR-RM850X', 'Corsair RM850x 850W 80+ Gold Full Modular', 'corsair-rm850x-850w-gold',
 3290000, 3690000, 84,
 '{"Công suất":"850W","Hiệu suất":"80+ Gold","Modular":"Full Modular","Fan":"135mm Zero RPM Mode","Bảo hành":"7 năm","PCIe 5.0":"2×16-pin 12VHPWR"}',
 0, (now() AT TIME ZONE 'utc'), false),

(22, 20, 'PSU-SEA-GX850', 'Seasonic Focus GX-850 850W 80+ Gold Full Modular', 'seasonic-focus-gx-850-gold',
 3490000, 3990000, 120,
 '{"Công suất":"850W","Hiệu suất":"80+ Gold","Modular":"Full Modular","Fan":"120mm Fluid Dynamic","Bảo hành":"10 năm"}',
 0, (now() AT TIME ZONE 'utc'), false),

(23, 21, 'PSU-BQ-SP11-750', 'be quiet! Straight Power 11 750W 80+ Gold', 'bequiet-straight-power-11-750w-gold',
 2890000, 3290000, 60,
 '{"Công suất":"750W","Hiệu suất":"80+ Gold","Modular":"Full Modular","Fan":"135mm SilentWings 3","Tiếng ồn":"<14 dBA","Bảo hành":"5 năm"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Case variants
(24, 22, 'CASE-COR-4000D-BLK', 'Corsair 4000D Airflow Mid-Tower ATX (Đen)', 'corsair-4000d-airflow-black',
 1990000, 2290000, 24,
 '{"Form factor":"ATX Mid-Tower","Fan đi kèm":"2×120mm","Fan tối đa":"6×120mm hoặc 3×140mm","Radiator":"360mm","GPU dài tối đa":"360mm","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

(25, 23, 'CASE-CM-TD500-BLK', 'Cooler Master TD500 Mesh V2 ATX (Đen)', 'coolermaster-td500-mesh-v2-black',
 1790000, 2090000, 24,
 '{"Form factor":"ATX Mid-Tower","Fan đi kèm":"5×120mm ARGB","Fan tối đa":"6×120mm","Radiator":"360mm","Mặt trước":"Lưới thép","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

(26, 24, 'CASE-ASUS-GT502-BLK', 'ASUS TUF Gaming GT502 ATX (Đen)', 'asus-tuf-gt502-black',
 2490000, 2890000, 24,
 '{"Form factor":"ATX Mid-Tower","Fan đi kèm":"6×120mm ARGB","Radiator":"360mm trước/trên","GPU dài tối đa":"400mm","CPU cao tối đa":"170mm","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Fan Case variants
(27, 25, 'FAN-DC-FC120-3P', 'DeepCool FC120 ARGB 3-pack + Hub', 'deepcool-fc120-argb-3pack-hub',
 890000, 990000, 12,
 '{"Số lượng":"3 cái + Hub","Kích thước":"120×120×25mm","Đèn":"ARGB","Tốc độ":"500–1800 RPM","Lưu lượng khí":"56.5 CFM","Kết nối":"3-pin ARGB + 4-pin PWM"}',
 0, (now() AT TIME ZONE 'utc'), false),

(28, 26, 'FAN-CM-SF120-ARGB', 'Cooler Master SickleFlow 120 ARGB (1 cái)', 'coolermaster-sickleflow-120-argb-1pc',
 190000, 220000, 12,
 '{"Số lượng":"1 cái","Kích thước":"120×120×25mm","Đèn":"ARGB","Tốc độ":"650–1800 RPM","Lưu lượng khí":"62 CFM","Vòng bi":"FDB"}',
 0, (now() AT TIME ZONE 'utc'), false),

(29, 27, 'FAN-COR-LL120-3P', 'Corsair LL120 RGB 3-pack + Lighting Node Core', 'corsair-ll120-rgb-3pack-node',
 1590000, 1890000, 24,
 '{"Số lượng":"3 cái + Lighting Node Core","Kích thước":"120×120×25mm","Đèn":"RGB 16 LED dual-ring","Tốc độ":"600–1500 RPM","PWM":"Có"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Monitor variants
(30, 28, 'MON-LG-27GP850', 'LG 27GP850-B 27" QHD 165Hz IPS', 'lg-27gp850-b-27-qhd-165hz',
 7490000, 8490000, 36,
 '{"Kích thước":"27 inch","Độ phân giải":"2560×1440 (QHD)","Tần số quét":"165Hz","Thời gian phản hồi":"1ms GtG","Tấm nền":"IPS","HDR":"HDR10","Sync":"FreeSync Premium"}',
 0, (now() AT TIME ZONE 'utc'), false),

(31, 29, 'MON-MSI-MAG274', 'MSI MAG274QRF-QD 27" QHD 165Hz IPS', 'msi-mag274qrf-qd-27-qhd-165hz',
 8490000, 9490000, 36,
 '{"Kích thước":"27 inch","Độ phân giải":"2560×1440 (QHD)","Tần số quét":"165Hz","Thời gian phản hồi":"1ms","Tấm nền":"IPS","Công nghệ":"Quantum Dot","Sync":"G-Sync Compatible"}',
 0, (now() AT TIME ZONE 'utc'), false),

(32, 30, 'MON-ASUS-PG279QM', 'ASUS ROG Swift PG279QM 27" QHD 240Hz G-Sync', 'asus-rog-pg279qm-27-qhd-240hz',
 14990000, 16990000, 36,
 '{"Kích thước":"27 inch","Độ phân giải":"2560×1440 (QHD)","Tần số quét":"240Hz","Thời gian phản hồi":"1ms GtG","Tấm nền":"IPS","HDR":"HDR400","Sync":"NVIDIA G-Sync"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Mouse variants
(33, 31, 'MOUSE-LG-GPX2-BLK', 'Logitech G Pro X Superlight 2 (Đen)', 'logitech-g-pro-x-superlight2-black',
 2890000, 3290000, 24,
 '{"Kết nối":"LIGHTSPEED 2.4GHz + USB-C","Sensor":"HERO 2 25K DPI","Cân nặng":"60g","Pin":"95 giờ","Nút":"5 nút","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

(34, 32, 'MOUSE-RAZER-DAV3PRO-BLK', 'Razer DeathAdder V3 Pro (Đen)', 'razer-deathadder-v3-pro-black',
 3290000, 3690000, 24,
 '{"Kết nối":"2.4GHz HyperSpeed + USB-C","Sensor":"Focus Pro 30K DPI","Cân nặng":"64g","Pin":"90 giờ","Nút":"6 nút","Màu":"Đen","Ergonomic":"Tay phải"}',
 0, (now() AT TIME ZONE 'utc'), false),

(35, 33, 'MOUSE-LG-G502X-BLK', 'Logitech G502 X Plus (Đen)', 'logitech-g502-x-plus-black',
 2490000, 2890000, 24,
 '{"Kết nối":"LIGHTSPEED 2.4GHz + USB-C","Sensor":"HERO 25K DPI","Cân nặng":"106g","Pin":"130 giờ","Nút":"13 nút","Màu":"Đen","Trọng lượng tùy chỉnh":"Có"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Keyboard variants
(36, 34, 'KB-LG-GPXTKL-BLK', 'Logitech G Pro X TKL Rapid (Đen)', 'logitech-g-pro-x-tkl-rapid-black',
 3490000, 3990000, 24,
 '{"Kết nối":"LIGHTSPEED 2.4GHz + USB-C","Layout":"TKL 87%","Switch":"GX Optical","Đèn":"RGB per-key","Pin":"30 giờ không đèn","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

(37, 35, 'KB-RAZER-BW-V4PRO-BLK', 'Razer BlackWidow V4 Pro (Đen)', 'razer-blackwidow-v4-pro-black',
 3990000, 4490000, 24,
 '{"Kết nối":"HyperSpeed 2.4GHz + USB-C + BT","Layout":"Full-size 100%","Switch":"Razer Green Mechanical","Đèn":"Chroma RGB per-key","Pin":"200 giờ không đèn","Macro keys":"6 phím"}',
 0, (now() AT TIME ZONE 'utc'), false),

(38, 36, 'KB-MSI-GK71-RED', 'MSI Vigor GK71 Sonic Red (Có dây)', 'msi-vigor-gk71-sonic-red',
 1490000, 1690000, 24,
 '{"Kết nối":"USB-A","Layout":"Full-size 100%","Switch":"MSI Sonic Red (tuyến tính)","Đèn":"RGB per-key","Form factor":"Full-size","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Tản nhiệt khí variants
(39, 37, 'COOL-NOCTUA-NHD15', 'Noctua NH-D15 Dual Tower (Màu nâu)', 'noctua-nh-d15-brown',
 2090000, 2290000, 72,
 '{"Loại":"Dual Tower","Fan":"2×NF-A15 140mm","Tốc độ fan":"300–1500 RPM","TDP":"250W+","Chiều cao":"165mm","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4","Tiếng ồn":"24.6 dBA"}',
 0, (now() AT TIME ZONE 'utc'), false),

(40, 38, 'COOL-CM-H212EVO-V2', 'Cooler Master Hyper 212 EVO V2 ARGB', 'coolermaster-hyper-212-evo-v2-argb',
 790000, 890000, 24,
 '{"Loại":"Single Tower","Fan":"1×120mm ARGB PWM","Tốc độ fan":"650–2000 RPM","TDP":"150W","Chiều cao":"158.8mm","Ống đồng":"4","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4"}',
 0, (now() AT TIME ZONE 'utc'), false),

(41, 39, 'COOL-DC-AK620', 'DeepCool AK620 Dual Tower', 'deepcool-ak620-dual-tower',
 1190000, 1390000, 36,
 '{"Loại":"Dual Tower","Fan":"2×120mm PWM","Tốc độ fan":"500–1850 RPM","TDP":"260W","Chiều cao":"160mm","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4","Tiếng ồn":"28 dBA"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- AIO variants
(42, 40, 'AIO-COR-H150I-360', 'Corsair iCUE H150i Elite CAPELLIX 360mm', 'corsair-h150i-elite-capellix-360',
 5490000, 6290000, 60,
 '{"Loại":"AIO Liquid Cooler","Radiator":"360mm","Fan":"3×LL120 RGB","Tốc độ bơm":"2400 RPM","Đầu bơm":"CAPELLIX LED","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4"}',
 0, (now() AT TIME ZONE 'utc'), false),

(43, 41, 'AIO-DC-LT720-360', 'DeepCool LT720 360mm LCD AIO', 'deepcool-lt720-360-lcd',
 3990000, 4490000, 36,
 '{"Loại":"AIO Liquid Cooler","Radiator":"360mm","Fan":"3×120mm ARGB","LCD":"2 inch IPS display","Tốc độ bơm":"2800 RPM","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4"}',
 0, (now() AT TIME ZONE 'utc'), false),

(44, 42, 'AIO-CM-ML360L-V2', 'Cooler Master MasterLiquid 360L ARGB V2', 'coolermaster-ml360l-argb-v2',
 2290000, 2690000, 24,
 '{"Loại":"AIO Liquid Cooler","Radiator":"360mm","Fan":"3×120mm ARGB","Đầu bơm":"ARGB ring","Tốc độ bơm":"2400 RPM","Socket Intel":"LGA1700/1200/115x","Socket AMD":"AM5/AM4"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Headset variants
(45, 43, 'HS-LG-GPX2LS-BLK', 'Logitech G Pro X 2 LIGHTSPEED (Đen)', 'logitech-g-pro-x2-lightspeed-black',
 3990000, 4490000, 24,
 '{"Kết nối":"LIGHTSPEED 2.4GHz","Driver":"50mm PRO-G Graphene","Micro":"Blue VO!CE tháo được","Pin":"50 giờ","Trọng lượng":"345g","Màu":"Đen"}',
 0, (now() AT TIME ZONE 'utc'), false),

(46, 44, 'HS-RAZER-BSV2PRO-BLK', 'Razer BlackShark V2 Pro (Đen)', 'razer-blackshark-v2-pro-black',
 3390000, 3890000, 24,
 '{"Kết nối":"2.4GHz + 3.5mm","Driver":"50mm TriForce Titanium","Micro":"HyperClear Super Wideband","Âm thanh":"THX Spatial Audio","Pin":"70 giờ","Trọng lượng":"320g"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- Software variants
(47, 45, 'SW-WIN11-HOME-ESD', 'Windows 11 Home 64-bit (ESD - Key điện tử)', 'ms-windows-11-home-esd',
 3490000, 3990000, 0,
 '{"Phiên bản":"Windows 11 Home","Kiến trúc":"64-bit","License":"ESD vĩnh viễn","Số PC":"1","Kèm theo":"Key kích hoạt qua email"}',
 0, (now() AT TIME ZONE 'utc'), false),

(48, 46, 'SW-OFFICE2024-HS-ESD', 'Microsoft Office 2024 Home & Student (ESD)', 'ms-office-2024-home-student-esd',
 2990000, 3490000, 0,
 '{"Phiên bản":"Office 2024","Ứng dụng":"Word, Excel, PowerPoint, OneNote","License":"ESD vĩnh viễn","Số PC":"1","Hệ điều hành":"Windows 10/11 hoặc macOS"}',
 0, (now() AT TIME ZONE 'utc'), false),

-- PC Build sẵn variants
(49, 47, 'PC-GAMING-MID-16GB', 'PC Gaming RTX 4060 Ti - i5-13600K / 16GB DDR5', 'pc-gaming-tam-trung-rtx4060ti-16gb',
 26990000, 29990000, 12,
 '{"CPU":"Intel Core i5-13600K","Mainboard":"MSI B660M MORTAR WIFI","RAM":"16GB DDR5 5600MHz","GPU":"RTX 4060 Ti 8GB","SSD":"Samsung 980 Pro 1TB","PSU":"Corsair RM650x 650W Gold","Case":"Corsair 4000D Airflow","Tản nhiệt":"Cooler Master Hyper 212 EVO"}',
 0, (now() AT TIME ZONE 'utc'), false),

(50, 47, 'PC-GAMING-MID-32GB', 'PC Gaming RTX 4060 Ti - i5-13600K / 32GB DDR5', 'pc-gaming-tam-trung-rtx4060ti-32gb',
 30990000, 33990000, 12,
 '{"CPU":"Intel Core i5-13600K","Mainboard":"MSI B660M MORTAR WIFI","RAM":"32GB DDR5 5600MHz","GPU":"RTX 4060 Ti 8GB","SSD":"Samsung 980 Pro 1TB","PSU":"Corsair RM650x 650W Gold","Case":"Corsair 4000D Airflow","Tản nhiệt":"Cooler Master Hyper 212 EVO"}',
 0, (now() AT TIME ZONE 'utc'), false),

(51, 48, 'PC-GAMING-HIGH-64GB', 'PC Gaming RTX 4090 - i9-13900K / 64GB DDR5', 'pc-gaming-dinh-cao-rtx4090-64gb',
 89990000, 99990000, 12,
 '{"CPU":"Intel Core i9-13900K","Mainboard":"ASUS ROG MAXIMUS Z790 HERO","RAM":"64GB DDR5 6000MHz","GPU":"ASUS ROG RTX 4090 24GB","SSD":"Samsung 980 Pro 2TB","PSU":"Seasonic PRIME TX-1000W Platinum","Case":"Lian Li O11 Dynamic EVO","Tản nhiệt":"Corsair H150i Elite 360mm AIO"}',
 0, (now() AT TIME ZONE 'utc'), false),

(52, 49, 'PC-OFFICE-I5-16GB', 'PC Văn Phòng i5-12400 / 16GB DDR4 / 512GB SSD', 'pc-van-phong-i5-12400-16gb-512ssd',
 11990000, 13490000, 12,
 '{"CPU":"Intel Core i5-12400","Mainboard":"ASUS PRIME H610M-K","RAM":"16GB DDR4 3200MHz","GPU":"Intel UHD Graphics 730 (tích hợp)","SSD":"512GB NVMe PCIe 3.0","PSU":"650W 80+ Bronze","Case":"Micro-ATX","Ghi chú":"Không kèm màn hình"}',
 5, (now() AT TIME ZONE 'utc'), false);
DO $$ BEGIN RAISE NOTICE 'Seed data linh kien may tinh va PC Build san da duoc tao thanh cong.'; END $$;
-- ============================================
-- ĐỒNG BỘ SEQUENCE — BẮT BUỘC, ĐỪNG XOÁ
-- ============================================
-- 🔴 Bốn bảng trên nhận `Id` TƯỜNG MINH trong các INSERT ở trên (122 dòng dữ liệu).
-- Trên SQL Server việc đó cần `SET IDENTITY_INSERT ON` và sau đó identity tự biết đã
-- tới đâu. PostgreSQL thì KHÔNG: sequence sinh ra cùng cột identity vẫn đứng ở 1, vì
-- nó chưa từng được gọi.
--
-- Hậu quả nếu thiếu khối này — và đây là lý do nó nguy hiểm: seed xong MỌI THỨ XANH,
-- danh sách sản phẩm hiện đủ, demo chạy tốt. Lỗi chỉ nổ ra ở LẦN ĐẦU admin tạo một
-- sản phẩm mới, khi sequence cấp `Id = 1` và đâm khoá chính với hàng đã seed.
-- Đó là kiểu hỏng muộn nhất và khó truy nhất trong cả đợt chuyển provider.
--
-- `coalesce(max, 0) + 1` chứ không phải `max`: setval với tham số thứ ba mặc định
-- (true) nghĩa là "giá trị này ĐÃ dùng", nên lần gọi sau trả max+1. Dùng false ở đây
-- để đúng cả khi bảng rỗng (max = NULL → bắt đầu từ 1).
SELECT setval(pg_get_serial_sequence('"Manufacturers"',   'Id'), coalesce((SELECT max("Id") FROM "Manufacturers"),   0) + 1, false);
SELECT setval(pg_get_serial_sequence('"Categories"',      'Id'), coalesce((SELECT max("Id") FROM "Categories"),      0) + 1, false);
SELECT setval(pg_get_serial_sequence('"Products"',        'Id'), coalesce((SELECT max("Id") FROM "Products"),        0) + 1, false);
SELECT setval(pg_get_serial_sequence('"ProductVariants"', 'Id'), coalesce((SELECT max("Id") FROM "ProductVariants"), 0) + 1, false);
