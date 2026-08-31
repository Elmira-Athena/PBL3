# LoadProbe — đo tính đúng đắn dưới tải đồng thời

**Đây không phải công cụ đo hiệu năng.** Nó bắn request song song rồi **khẳng định
bất biến bằng LINQ trên DB**.

Lý do phải làm vậy: **mọi lỗi đúng đắn dữ liệu tìm thấy ở repo này đều trả HTTP 200.**
Voucher vượt hạn mức, sổ tổn thất nhân đôi, hai phiếu dịch vụ cùng một serial — tất cả
đều "thành công" ở tầng HTTP. Một script bash đếm mã lỗi không phát hiện được cái nào
trong số đó. Console app .NET tham chiếu thẳng `HushStoreDbContext` nên phần khẳng định
chỉ là ba dòng LINQ.

## Chạy

Cần **DB đang chạy** và **API đang chạy** (xem `docs/bat-dau-phien-moi.md` mục 3).

```bash
PW=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HushStoreDb;User Id=sa;Password=${PW};TrustServerCertificate=True;MultipleActiveResultSets=True"
export JwtSettings__SecretKey="<ĐÚNG khoá mà API đang chạy đang dùng>"

dotnet run --project tools/LoadProbe -- --out docs/evidence/loadprobe
```

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `--api` | `http://localhost:5222` | Gốc API. Trỏ vào `http://localhost:8088` để đo qua nginx 2 replica. |
| `--scenarios` | `all` | `S01,S04` hoặc `1,4`. |
| `--out` | `docs/evidence/loadprobe` | Thư mục ghi báo cáo markdown. |
| `--pace` | `11` | Giây chờ giữa hai kịch bản. **Xem cảnh báo rate limiter bên dưới.** |
| `--keep` | tắt | Giữ dữ liệu seed để soi tay thay vì dọn. |

Mã thoát: `0` tất cả đạt · `1` có bất biến sai · `2` có kịch bản không kết luận được ·
`3` lỗi cấu hình/môi trường.

## Chín kịch bản

| # | Bắn | Bất biến kiểm sau |
|---|---|---|
| S01 | 50 khách checkout đồng thời | Không `OrderCode` trùng, đủ 50 đơn, 0 lỗi 5xx |
| S02 | Voucher `Quantity=1`, 20 khách | `UsedCount = 1` và `COUNT(VoucherUsages) = 1` |
| S03 | Cùng khách, `MaxUsesPerUser=1`, 10 request | `COUNT(...) = 1` |
| S04 | 10 lần `intake` cùng một serial | Đúng 1 phiếu chưa đóng |
| S05 | 10 lần `accept-quotation` cùng báo giá | `Status=1` và đúng 1 bản ghi lịch sử |
| S06 | 5 lần `approve` cùng phiếu kiểm kê | `InventoryAdjustmentLogs` không nhân đôi |
| S07 | POS bán serial S **xen kẽ** kiểm kê đánh S là Lost | Nếu đã bán thì `Status ≠ Lost` |
| S08 | 2 lần `create-quotation` song song | Đúng 1 báo giá `Status=0` |
| S09 | 2 lần `refresh-token` cùng cặp | Không ai bị đăng xuất oan |

## Bốn quyết định thiết kế, mỗi cái đóng một cách đo sai

### 1. Hạng `KHÔNG KẾT LUẬN` tồn tại để chặn bằng chứng giả

Kịch bản nào bị **429**, lỗi tầng vận chuyển, hay bắn thiếu request đều bị gán
`KHÔNG KẾT LUẬN` **trước khi** phần khẳng định bất biến kịp chạy.

Vì sao bắt buộc: một kịch bản mà 49/50 request ăn 429 sẽ **thoả mọi bất biến** — không
có voucher nào vượt hạn mức, không có mã đơn nào trùng — chỉ vì code cần đo chưa từng
chạy. Nếu in ra "ĐẠT" thì báo cáo đang nói dối, và nói dối theo hướng an toàn giả.

### 2. Probe **tự ký** access token, không gọi `/api/auth/login`

Đăng nhập bị giới hạn **5 lần/phút mỗi IP**, đăng ký **3 lần/giờ**. Kịch bản "50 khách
checkout đồng thời" mà đi qua login thì 45 khách ăn 429 trước khi chạm tới code cần đo.

Probe dùng **đúng khoá ký** mà API đang chạy dùng (`JwtSettings__SecretKey`), nên nó
không vòng qua lớp xác thực — nó đóng vai người đã đăng nhập hợp lệ. Middleware kiểm
`IsActive` vẫn đọc thẳng DB và vẫn chạy đầy đủ.

### 3. `--pace` mặc định 11 giây **không phải trang trí**

Trần chung là **100 request / 10 giây cho MỖI IP**. Probe bắn từ đúng một IP, nên hai
kịch bản chạy sát nhau dùng chung một cửa sổ và kịch bản sau ăn 429. 11 giây để cửa sổ
10 giây chắc chắn lăn qua. Đặt `--pace 0` thì phải chấp nhận đọc `KHÔNG KẾT LUẬN`.

### 4. Mỗi lần khẳng định mở **DbContext MỚI**

Dùng lại context đã seed là cái bẫy im lặng số một của công cụ kiểu này: Change Tracker
trả về thực thể trong RAM chứ không đọc lại DB, nên bất biến được kiểm trên **đúng dữ
liệu mà probe vừa tự ghi** — luôn đúng, không bao giờ bắt được lỗi.

## Dữ liệu: seed và dọn

Mọi bản ghi probe tạo ra mang tiền tố `LP-` ở cột mã, hoặc thuộc miền email
`@loadprobe.local`. Probe **dọn trước khi chạy và dọn sau khi chạy**, nên một lần chạy
bị giết giữa chừng không để lại rác cho lần sau.

⚠️ Đơn hàng và phiếu dịch vụ **do chính API tạo ra trong lúc đo** mang mã thật
(`ORD-…`, `ST-…`) chứ không mang `LP-`. Dọn dẹp nhận diện chúng qua thứ chúng **tham
chiếu tới** (biến thể, serial, tài khoản của probe) — xem `ProbeFixture.CleanupAsync`.
Thêm kịch bản mới mà tạo ra loại bản ghi khác thì **phải bổ sung vào đó**.

⚠️ **Tên bảng ≠ tên `DbSet`.** `ServiceTicketStatusHistory` là **số ít** trong DB.
Tra bằng `SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES` trước khi viết SQL thô.

Probe cần tài khoản **`admin@hushstore.com`** có thật và đang hoạt động — nó đóng vai
nhân viên trong các kịch bản kho và dịch vụ.

## Thêm một kịch bản

Kế thừa `ScenarioBase`, cài ba pha, rồi thêm vào danh sách trong `Program.cs`.

Phần đáng đầu tư là `AssertAsync`. Câu hỏi để tự kiểm: *nếu lỗi này xảy ra thật, mã HTTP
có đổi không?* Nếu không — và với repo này thì gần như luôn là không — thì bất biến phải
đọc từ bảng.
