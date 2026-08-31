# Bắt đầu phiên mới — đọc file này trước

**Cập nhật:** 2026-08-31 · **Trạng thái repo:** nhánh `feat/retry-safe-call-sites`, build `0 Error(s)`
· **Đã xong:** đợt 1, mục 4.1, đợt 2, **mục A** · **Kế tiếp:** mục B bên dưới

Tài liệu này viết cho một phiên **không có ngữ cảnh gì cả**. Nó trả lời đúng ba câu:
*đang ở đâu*, *làm gì tiếp*, và *chạy/kiểm bằng lệnh nào*.

---

## 0. Đọc theo thứ tự này

| # | File | Đọc để biết |
|---|---|---|
| 1 | **file này** | việc kế tiếp + cách chạy + cách kiểm |
| 2 | [`CLAUDE.md`](../CLAUDE.md) | quy ước bắt buộc của repo (có 4 quy tắc sinh ra từ lỗi thật) |
| 3 | [`docs/nang-cap-dot-1-ket-qua.md`](nang-cap-dot-1-ket-qua.md) | *vì sao* mọi thứ thành ra như hiện tại + bằng chứng đã chạy |
| 4 | `/Users/ml/.claude/plans/hi-n-t-i-t-i-ang-memoized-quill.md` | kế hoạch gốc đầy đủ (đợt 3→8) |

**Không cần đọc lại toàn bộ diff của 10 commit.** Mọi quyết định khó đều đã được ghi thành
comment **ngay tại chỗ code**, và commit message ghi lý do.

---

## 1. Đang ở đâu

Đợt 1 (sửa lỗi đồng thời, transaction, sinh mã), mục 4.1 (`EnableRetryOnFailure`), đợt 2
(frontend + vá bảo mật) và mục A (rà retry 18/18 call-site) đã xong và đã kiểm chạy thật.

Hai thứ **cố ý làm dở**, đều đã ghi lý do và đều nằm ở mục B/D dưới đây:

1. **23 nút gọi mutation chưa chống double-submit** — cơ chế đã có sẵn, chỉ chưa quét hết.
2. **3 gói NuGet mức High chưa vá** — cần PR riêng, xem mục D.

Đã đóng: **18/18 call-site transaction nay đều retry-safe** (mục A, xong 2026-08-31).

---

## 2. Việc kế tiếp, xếp theo thứ tự nên làm

### ✅ 🅰 Rà nốt call-site chưa retry-safe — **XONG (2026-08-31)**

18/18 call-site `ExecuteInTransactionAsync` nay đều `retrySafe: true`. Kiểm lại bất cứ lúc nào:

```bash
grep -rn "CHƯA RÀ RETRY" --include='*.cs' src/Service/          # kỳ vọng: rỗng
grep -rc "}, retrySafe: true)" --include='*.cs' src/Service/ | grep -v ':0'   # tổng 18
```

**Khuôn đã áp cho cả 14 chỗ** — hữu ích khi viết call-site transaction MỚI:

1. **Kiểm tra nghiệp vụ ở ngoài** bằng đọc **không tracking** (projection hoặc bản
   `AsNoTracking`), chỉ để trả lỗi đẹp mà không phải mở transaction.
2. **Nạp lại mọi entity sẽ ghi ở bên trong** delegate.
3. **Kiểm lại ở trong** làm chốt chống race — và **ném** chứ không `return`, vì `return` thì
   transaction vẫn commit.
4. Mọi giá trị **sinh một lần** (mã chứng từ, `DateTime.UtcNow` dùng để ghi) tính **bên trong**.

**Bốn bẫy đã gặp khi làm — đều là "chỉ đổi cờ thì hỏng":**

| Bẫy | Ở đâu | Hỏng thế nào |
|---|---|---|
| `+=` / `++` trên entity tracked | `InventoryCheckService.SubmitAsync`, `RejectAsync` | Cộng chồng thành `cũ + 2×missing`. EF **thấy** có thay đổi nên vẫn sinh `UPDATE` — với con số **sai**. Ghi sai số liệu âm thầm, tệ hơn mất dữ liệu vì kết quả trông vẫn hợp lệ. |
| Collection đã `Include` rồi `.Add` | `InventoryExportService.ExportOrderAsync` | `orderDetail.OrderSerials` giữ luôn bản ghi Add của lần thử trước → sinh `OrderSerial` **trùng**. |
| Entity `new` sẵn ở ngoài rồi `Add` ở trong | `OrderService` (`VoucherUsage`), `PosService` (`Order`, `OrderDetail`, `Warranty`) | Sau lần thử 1 chúng đã có Id; lần thử 2 `Add` lại là **no-op** hoặc ghi trùng. Đã sửa bằng cách trả **dữ liệu thuần** (`VoucherUsagePlan`, `itemPlan`) rồi mới `new` entity bên trong. |
| Danh sách tích luỹ khai ở ngoài | `PosService` (`newWarranties`) | Lần thử 2 `Add` chồng lên danh sách cũ → bảo hành nhân đôi. |

**Hai thay đổi kèm theo, cần biết khi đọc code:**

- **`ConcurrentModificationException`** (`src/Core/Exceptions/`) — kiểu riêng cho chốt chống
  race bên trong transaction. Cần nó vì các call-site trả `ApiResult` đều có
  `catch (Exception)` bọc ngoài sẽ nuốt mất thông báo cụ thể. Bắt
  `InvalidOperationException` thay thế thì **không an toàn**: EF Core và `IUnitOfWork` cũng ném
  đúng kiểu đó cho chuyện khác. Các call-site ở `ServiceTicketService` **không** dùng kiểu này
  vì chúng `catch { throw; }` — `InvalidOperationException` đã đi ra nguyên vẹn.
- **`ApplyVouchersAsync` nay trả `VoucherUsagePlan`** (record dữ liệu thuần) chứ không trả
  entity `VoucherUsage` dựng sẵn. Cả hai call-site tự `new` entity bên trong delegate.

**Đã kiểm chạy thật** (API local, 2026-08-31): tạo → gửi duyệt → phê duyệt → từ chối phiếu
kiểm kê đều 200, và **truy vấn thẳng DB xác nhận giá trị đã ghi** (`Status = 2` +
`ApprovedAt`, `Status = 0` + `RejectReason`) — đúng thứ mà chế độ hỏng "không sinh `UPDATE`"
sẽ làm sai. Dữ liệu test đã dọn, `InventoryChecks = 0` như cũ.

> ⚠️ Còn **chưa** kiểm chạy thật: luồng đặt hàng, POS, xuất kho và phiếu dịch vụ — DB local
> gần như rỗng (Products = 2, ProductSerials = 0, Orders = 0) nên không dựng nổi kịch bản.
> Bốn luồng này mới chỉ được rà bằng đọc code + build sạch.

---

### 🅱 Quét nốt chống double-submit — 23 nút

Cơ chế **đã có sẵn và đã chạy đúng** (`ActionButton` / `BusyScope` / `BusyState` ở
`src/Client/Shared/Components/Common/`). Việc còn lại thuần tuý là đổi `<MudButton>` →
`<ActionButton>` (bỏ luôn `Disabled="_isXxx"` và spinner thủ công nếu có — `ActionButton` tự lo).

**Ưu tiên cao — nút đụng tới tiền hoặc tồn kho:**

| File : dòng | Handler | Vì sao ưu tiên |
|---|---|---|
| `Storefront/Checkout.razor:215` | `PlaceOrder` | **Cao nhất.** Khách đặt hàng — bấm hai lần là hai đơn |
| `Warehouse/ImportReceiptPage.razor:190` | `SaveReceipt` | nhập kho hai lần = tồn kho sai |
| `Inventory/ExportOrder.razor:145` | `SubmitExportAsync` | xuất kho hai lần |
| `Orders/OrderDetail.razor:31,37,50` | `ConfirmCompleteOrder`, `ConfirmOrderApprove`, `ConfirmCancelOrder` | đổi trạng thái đơn |
| `Storefront/MyOrderDetail.razor:50,59` | `ConfirmCancelOrder`, `ConfirmReceived` | khách tự huỷ/xác nhận |
| `ServiceTickets/ServiceTicketQuotation.razor:110` | `SubmitQuotation` | tạo báo giá trùng |
| `ServiceTickets/ServiceTicketIntake.razor:128` | `SubmitIntake` | tạo phiếu trùng |

**Ưu tiên thường — CRUD admin (đều là nút `Submit` trong dialog):**

`Admin/Banners/BannerDialog.razor:161` · `Admin/Manufacturers/ManufacturerDialog.razor:123` ·
`Admin/Suppliers/SupplierDialog.razor:94` · `Admin/Vouchers/VoucherForm.razor:337` ·
`Categories/CategoryDialog.razor:128` · `Inventory/Components/CreateInventoryCheckDialog.razor:79` ·
`Inventory/Components/RejectInventoryCheckDialog.razor:52` ·
`Inventory/Components/UpdateReasonDialog.razor:49` ·
`Inventory/Components/UpdateSerialStatusDialog.razor:45` ·
`Storefront/AddAddressDialog.razor:107` · `Storefront/Profile.razor:125` ·
`Storefront/WriteReviewDialog.razor:39` · `Pos/Index.razor:23` (`SaveDraft`)

> Lấy lại danh sách bất cứ lúc nào: xem script ở §6.

**Hai lưu ý khi làm:**

- Nút trong `<MudForm>` có `ButtonType="ButtonType.Submit"` thì phải giữ nguyên thuộc tính đó —
  `ActionButton` có truyền `ButtonType` qua.
- Nút là `MudIconButton` (trong ô bảng) **không** dùng `ActionButton` được. Sửa cờ tại chỗ theo
  đúng mẫu đã làm ở `InventoryCheckDetailPage.HandleMarkDefective`: đặt cờ **trước mọi `await`**,
  gọi `StateHasChanged()`, nhả trong `finally`.

**⚠️ Nhắc lại cho rõ:** cơ chế này **không bảo vệ server**. Hai tab, F5 giữa chừng, hay `curl` —
vẫn double-submit. Phòng tuyến thật là conditional update + unique index (đợt 1 đã làm cho
voucher và báo giá; đợt 3 làm nốt). Đừng đọc mục này rồi tưởng nhóm lỗi đồng thời đã xong.

---

### 🅲 Đợt 0 còn thiếu (chưa động dòng nào)

- `tools/LoadProbe/` — console app .NET, 9 kịch bản `IProbeScenario`, in bảng markdown vào
  `docs/evidence/`. Giá trị không nằm ở "bắn N request đếm 500" mà ở **khẳng định bất biến sau
  khi bắn** bằng truy vấn LINQ (app tham chiếu thẳng `HushStoreDbContext` nên assertion là 3 dòng).
- `docker-compose` 2 replica API + nginx round-robin — hạ tầng tối thiểu để chứng minh tính đúng
  đắn đa-instance, và **miễn phí**.
- Đo `remainingResources` của container instance. Cảnh báo RAM chưa xác minh: api 512 + web 192 +
  migrator 512 = **1216 MiB** vs ~950–985 MiB khả dụng trên t3.micro. Sửa rẻ nhất nếu đúng: hạ
  `memory` của migrator 512 → 256 ở `infra/tf/modules/ecs/taskdef.tf`.

---

### 🅳 Vá 3 gói NuGet mức High — PR riêng

| Gói | Bản hiện tại | Số advisory |
|---|---|---|
| `System.Security.Cryptography.Xml` | 9.0.0 và 10.0.0 | **8** |
| `AutoMapper` | 16.0.0 | 1 (`GHSA-rvv3-g6hj-g44x`) |
| `Microsoft.OpenApi` | 2.4.1 | 1 (`GHSA-v5pm-xwqc-g5wc`) |

**Vì sao chưa làm:** nâng phiên bản có rủi ro hồi quy riêng (AutoMapper 16 → bản mới có breaking
change ở cấu hình profile) và repo **không có test tự động nào** để đỡ.

**Làm cùng lúc với việc quan trọng hơn:** thêm bước cho pipeline **fail** khi có lỗ hổng High —
đó mới là vấn đề thật, vì cảnh báo `NU1903` đã hiện sẵn ở **mỗi lần build** mà không có chỗ nào
bắt buộc xử lý:

```bash
dotnet list package --vulnerable --include-transitive
```

---

### 🅴 Đợt 3 — **VẪN BỊ CHẶN CỨNG**

Không bắt đầu đợt 3 trước khi có kết quả `Infrastructure/db/checks/pre_migration_checks.sql`
**chạy trên RDS**. Chạy trên DB local là vô nghĩa: local gần như rỗng (Orders = 0,
ProductSerials = 0, Products = 2).

Hai câu hỏi và hệ quả:

| Câu hỏi | Nếu kết quả là… | Thì… |
|---|---|---|
| `Vouchers.MaxUsesPerUser` có giá trị `> 1` không? | toàn `NULL`/`1` | unique index `(UserId, VoucherId)` là đủ |
| | có `> 1` | phải thêm cột `SeqPerUser` + backfill `ROW_NUMBER()` — **thêm ~1 ngày công** |
| `InventoryAdjustmentLogs` đã có bản ghi trùng chưa? | rỗng | thêm unique index thẳng |
| | **có** | phát sinh **việc nghiệp vụ**: dọn dữ liệu + đối chiếu sổ tổn thất |

Script **đã sửa cho khớp schema thật** và chạy sạch — chỉ cần trỏ connection string sang RDS.

---

## 3. Chạy môi trường local ($0)

### DB

```bash
cd Infrastructure/db && docker-compose up -d      # container: hushstore_sqlserver_dev, cổng 1433
```

Mật khẩu `sa` nằm ở `Infrastructure/db/.env` (khoá `SA_PASSWORD`). Đã khớp với volume hiện tại.

```bash
PW=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
docker exec hushstore_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$PW" -C -I -d HushStoreDb -Q "SELECT COUNT(*) FROM Products;"
```

> **Bắt buộc `-C` và `-I`.** `-C` bỏ qua kiểm chứng chỉ self-signed. `-I` bật
> `QUOTED_IDENTIFIER` — thiếu nó thì `seed_data.sql` **hỏng ngay câu INSERT đầu tiên**
> (`Msg 1934`), vì DB có filtered index / computed column.
> Cũng lưu ý: `-y` và `-W` **loại trừ nhau**, đừng dùng chung.

### API

```bash
PW=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HushStoreDb;User Id=sa;Password=${PW};TrustServerCertificate=True;MultipleActiveResultSets=True"
export JwtSettings__SecretKey="$(openssl rand -base64 48 | tr -d '\n')"
export ASPNETCORE_ENVIRONMENT=Development

dotnet run --project src/API/API.csproj --no-launch-profile --urls "http://localhost:5222"
```

**Ba cái bẫy đã mất thời gian, đừng vấp lại:**

1. **`JwtSettings__SecretKey` là bắt buộc.** `appsettings.json` để trống nó **có chủ đích**
   (không commit bí mật). Thiếu → mọi endpoint trả 500 `IDX10703: key length is zero`.
2. **Phải có `--no-launch-profile`.** Không có thì `launchSettings.json` **đè** `--urls` và app
   vẫn bind cổng 5111 → `address already in use` nếu đã có instance khác.
3. **Cổng bị chiếm** thì `pkill -f "API.dll"` **không ăn** (tiến trình là `dotnet run`). Dùng:
   ```bash
   lsof -ti tcp:5222 | xargs -r kill -9
   ```

### Client (chỉ khi cần kiểm giao diện/refresh bằng trình duyệt)

`wwwroot/appsettings.json` trỏ về **production** (`https://api.hushstore.io.vn`). Để trỏ về local,
tạo file **tạm** (nhớ xoá sau, **đừng commit**):

```bash
cat > src/Client/wwwroot/appsettings.Development.json <<'EOF'
{ "ApiBaseUrl": "http://localhost:5222" }
EOF
```

Và API phải cho CORS: khởi động API kèm `export AllowedOrigins="http://localhost:5214"`.

```bash
dotnet run --project src/Client/Client.csproj --no-launch-profile --urls "http://localhost:5214"
```

### Đăng nhập

**Đăng nhập bằng EMAIL, không phải username** (`LoginRequest.Email`).

| Tài khoản | Email | Mật khẩu | Vai trò |
|---|---|---|---|
| admin | `admin@hushstore.com` | `Admin@123` | Admin |
| employee | `employee@hushstore.com` | *(chưa đặt lại)* | Employee |

```bash
TOKEN=$(curl -s -X POST http://localhost:5222/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@hushstore.com","password":"Admin@123"}' \
  | python3 -c "import sys,json;print((json.load(sys.stdin).get('data') or {}).get('accessToken',''))")
```

---

## 4. Công thức kiểm chứng (đã dùng để tạo bằng chứng cho báo cáo)

```bash
B=http://localhost:5222

# Chặn trần pageSize — kỳ vọng 100 / 1 / 20
for p in 1000000 -5 20; do
  curl -s "$B/api/products?pageSize=$p" | python3 -c "import sys,json;print('$p ->',(json.load(sys.stdin).get('data') or {}).get('pageSize'))"
done

# Rate limit theo IP — kỳ vọng: một số lần 400 rồi chuyển sang 429
# ⚠️ Hạn mức là 5 request/phút cho MỖI IP, tính CẢ lần đăng nhập THÀNH CÔNG ở trên.
#    Nên nếu vừa lấy $TOKEN xong, bạn sẽ thấy 4 lần 400 rồi 429 — KHÔNG PHẢI LỖI.
#    Muốn thấy đủ 5, chờ hết cửa sổ 1 phút rồi chạy lại.
for i in $(seq 1 8); do
  curl -s -o /dev/null -w "%{http_code} " -X POST "$B/api/auth/login" \
    -H 'Content-Type: application/json' -d '{"email":"nobody@x.com","password":"wrong"}'
done; echo

# /health/* PHẢI được miễn trừ — kỳ vọng 0 lần khác 200
fails=0; for i in $(seq 1 150); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' $B/health/live)" != "200" ] && fails=$((fails+1)); done
echo "health non-200: $fails"

# Bề mặt ẩn danh — kỳ vọng 401 / 401 / 200
curl -s -o /dev/null -w "available-for-order %{http_code}\n" -X POST "$B/api/vouchers/available-for-order" -H 'Content-Type: application/json' -d '{"subTotal":100000}'
curl -s -o /dev/null -w "technicians        %{http_code}\n" "$B/api/employees/technicians"
curl -s -o /dev/null -w "validate-code      %{http_code}\n" -X POST "$B/api/vouchers/validate-code" -H 'Content-Type: application/json' -d '{"code":"KHONGCO","subTotal":100000}'

# Transaction chạy được dưới retrying strategy — kỳ vọng 200 + sinh mã KK-
curl -s -X POST "$B/api/inventory-checks" -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{"scopeType":0,"note":"smoke"}' | head -c 200
```

> Nhớ **dọn dữ liệu test** sau khi kiểm (phiếu kiểm kê tạo ra, mô tả sản phẩm đã sửa…).
> Phiên trước để lại DB đúng nguyên trạng: `InventoryChecks = 0`.

**Kiểm luồng refresh bằng trình duyệt** (chỉ khi đụng vào auth): đặt
`JwtSettings__AccessTokenExpirationMinutes=1`, đăng nhập, để token hết hạn **> 78 giây**
(phải vượt `ClockSkew` 60 giây, nếu không server vẫn chấp nhận token cũ và **không có 401 nào**),
rồi bấm một nút bắn nhiều request cùng lúc. Kỳ vọng: **N lời gọi 401 → đúng 1 lời gọi
`/api/auth/refresh-token` → N lời gọi lại thành công**, cả hai token xoay vòng, không về `/login`.

---

## 5. Bảy cái bẫy im lặng đã gặp — đọc trước khi sửa code

Tất cả đều **không sinh lỗi, không sinh cảnh báo**, và chỉ lộ ra khi đo.

1. **Ghi vào entity `AsNoTracking()`** — lệnh gán rơi vào hư vô, `SaveChanges` không sinh `UPDATE`.
   *Chống tái phát:* các phương thức no-tracking đã đổi tên thành `...ReadOnlyAsync`, nên gọi nhầm
   là **build vỡ**.
2. **Retry + Change Tracker** — lỗi transient lúc commit → entity thành `Unchanged` với snapshot =
   giá trị **mới** → lần thử lại gán cùng giá trị → **không sinh `UPDATE`** → mất dữ liệu.
   Biến thể tệ hơn: `+=` trên entity tracked → **ghi sai số** (xem mục A).
3. **`UseHsts()` đặt trước `UseForwardedHeaders()`** — no-op hoàn toàn im lặng. Container nhận HTTP
   thuần từ ALB nên `Request.IsHttps` chỉ đúng **sau khi** đọc `X-Forwarded-Proto`.
4. **`ClockSkew = 1 phút`** — vòng đời access token thực tế là **15 + 1** phút. Đừng bối rối khi đo.
5. **Cờ bận đặt SAU `await`** — `ComponentBase` chỉ tự `StateHasChanged()` sau phần **đồng bộ** của
   handler, nên cờ không bao giờ tới được UI. Bốn người đã viết đúng ý định và vẫn sai.
6. **`CascadingValue` mang `this`** — tham chiếu không đổi nên Blazor **không** render lại component
   con. Chốt chặn vẫn chạy đúng nhưng nút anh em **không chuyển sang mờ**. Phải phát event riêng.
7. **`ORDER BY Code DESC` để tìm mã cuối** — so sánh **chuỗi**, nên `-1000` sắp **trước** `-999`.
   Đây là nguyên nhân gốc của quả bom `{n:D3}`. Nay `IDocumentCodeGenerator` lấy max **theo số**.

---

## 6. Script lấy lại hai danh sách việc

Số dòng sẽ trôi khi code đổi. Chạy lại để lấy danh sách chính xác:

```bash
# A — ĐÃ XONG: hai lệnh này giờ là chốt chống hồi quy, không phải danh sách việc
grep -rn "CHƯA RÀ RETRY" --include='*.cs' src/Service/          # kỳ vọng: rỗng
grep -rc "}, retrySafe: true)" --include='*.cs' src/Service/ | grep -v ':0'   # tổng 18

# B — nút mutation chưa chuyển sang ActionButton
grep -rn "<MudButton" --include='*.razor' src/Client/Pages/ | wc -l
grep -rc "<ActionButton" --include='*.razor' src/Client/Pages/ | grep -v ':0'
```

---

## 7. Quy ước bắt buộc — vi phạm là tái tạo đúng lỗi cũ

Chi tiết trong [`CLAUDE.md`](../CLAUDE.md), nhắc lại bốn cái hay quên nhất:

- **Transaction — chỉ qua `IUnitOfWork.ExecuteInTransactionAsync`.** Cấm
  `_context.Database.BeginTransactionAsync()` thủ công: EF Core cấm nó khi có retrying strategy và
  ném **lúc chạy**. Việc làm *sau* commit phải nằm **ngoài** delegate.
- **Sinh mã chứng từ — chỉ qua `IDocumentCodeGenerator`.**
- **Không cache trạng thái phân quyền / khoá tài khoản trong `MemoryCache`.** Hướng nguy hiểm là
  hướng **mở khoá**. Đây là lỗi bảo mật, không phải lỗi hiệu năng.
- **DTO phân trang — kế thừa `PagedRequest`.** Trần `[1, 100]` dùng chung với `ClampPageSizeFilter`;
  đổi một bên phải đổi bên kia.

Và: **mọi thông báo lỗi cho người dùng phải bằng tiếng Việt có dấu.** Trường free-text mới mà sẽ
render bằng `MarkupString` thì **phải** đi qua `IHtmlContentSanitizer` lúc **ghi**.
