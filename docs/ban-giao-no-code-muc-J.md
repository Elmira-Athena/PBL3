# Bàn giao — mục 🅹: 40 lời gọi `GetFromJsonAsync` vứt mất câu tiếng Việt của server

> **Viết cho một phiên không có ngữ cảnh gì.** File này tự chứa.
>
> **Cập nhật:** 2026-09-24 · **Nhánh:** `main` · **Đo tại chỗ**, không lấy từ trí nhớ.

---

## 1. Lỗi là gì — cơ chế, không phải triệu chứng

`HttpClient.GetFromJsonAsync<T>()` **tự gọi `EnsureSuccessStatusCode()` bên trong**.
Nghĩa là khi server trả `400`/`404`/`409`, nó **ném `HttpRequestException` và vứt luôn
thân phản hồi** — trước khi code của ta kịp đọc.

Mà thân phản hồi chính là chỗ chứa `ApiResult<T>.Message` — **câu tiếng Việt mà tầng
Service đã soạn riêng cho tình huống đó**:

```
"Mã 'SALE50' đã hết lượt sử dụng."
"Sản phẩm này đã có phiếu sửa chữa chưa đóng."
```

Người dùng không bao giờ thấy những câu đó. Họ thấy câu chung chung của khối `catch`:

```
"Không tải được danh sách nhà cung cấp. Vui lòng thử lại."
```

**Vì sao nghiêm trọng hơn vẻ ngoài:** người dùng mất đúng thông tin cần để **tự sửa**.
Họ bấm "thử lại" và hỏng y hệt — vì lỗi không nằm ở đường truyền, nó nằm ở dữ liệu họ
nhập. Đây là hồi quy UX, và mục 🅸 (2026-09-01) **không đóng được** nó: mục 🅸 chỉ đổi
chuỗi EF Core thành câu tiếng Việt cố định, tức làm cái sai **đỡ xấu** chứ không làm
nó đúng.

---

## 2. 🚨 Quy mô thật là **40**, không phải 35 — và chỗ lệch lặp lại một lỗi cũ

`CLAUDE.md` ghi **35** ở hai chỗ. Đếm lại hôm nay:

```bash
grep -rn "GetFromJsonAsync" src/Client/ | wc -l     # → 40
```

| Loại | Số lời gọi | Số file |
|---|---|---|
| `.cs` — client service | **35** | 13 |
| `.razor` — trang/dialog gọi thẳng | **5** | 3 |
| **Tổng** | **40** | **16** |

**35 là con số của riêng `.cs`.** Tức ai đó đã đếm bằng cách chỉ quét `.cs` và bỏ sót
`.razor`.

> 🔁 **Đây đúng là lỗi mà repo đã mắc một lần rồi.** `CLAUDE.md` tự ghi:
> *"bản trước chỉ quét `src/Client/**/*.cs` nên mù 108 file `.razor`, nơi còn 15 chỗ rò
> rỉ thật"*. Cùng một cách đếm, cùng một điểm mù, cách nhau vài tuần. Khi sửa xong nhớ
> cập nhật cả hai chỗ trong `CLAUDE.md` về **40**.

**5 lời gọi trong `.razor` là loại việc KHÁC**, không phải "35 + 5". Chúng gọi HTTP
**thẳng từ trang**, bỏ qua hẳn tầng client service — nên sửa chúng là **chuyển lời gọi
vào service trước**, rồi mới đổi sang `ApiCall`. Đừng nhét `ApiCall.SendAsync` vào
`.razor` để cho nhanh: làm vậy là đóng băng việc bỏ qua tầng service.

### Phân bố đầy đủ

| File | Lời gọi |
|---|---|
| `Services/Analytics/AnalyticsClientService.cs` | 6 |
| `Services/Inventory/InventoryCheckClientService.cs` | 4 |
| `Services/Product/ProductClientService.cs` | 3 |
| `Services/Manufacturer/ManufacturerClientService.cs` | 3 |
| `Services/Employee/EmployeeClientService.cs` | 3 |
| `Services/Customer/CustomerClientService.cs` | 3 |
| `Services/Banner/BannerClientService.cs` | 3 |
| `Services/Voucher/VoucherClientService.cs` | 2 |
| `Services/Supplier/SupplierClientService.cs` | 2 |
| `Services/Inventory/ImportReceiptClientService.cs` | 2 |
| `Services/Category/CategoryClientService.cs` | 2 |
| `Services/Reviews/ReviewClientService.cs` | 1 |
| `Services/Pos/PosClientService.cs` | 1 |
| **`Pages/Storefront/AddAddressDialog.razor`** | **2** ⚠️ |
| **`Pages/Pos/Index.razor`** | **2** ⚠️ |
| **`Pages/ServiceTickets/Components/SwapSerialPickerDialog.razor`** | **1** ⚠️ |

---

## 3. Đích đến: `ApiCall.SendAsync`

`src/Client/Services/Common/ApiCall.cs` — đã có sẵn, **đang chỉ được dùng 2 chỗ**
(cả hai ở `Services/Orders/OrderClientService.cs`). Nó:

- **không bao giờ ném** — luôn trả `ApiResult<T>`
- **đọc thân phản hồi để lấy câu server soạn**, và ưu tiên câu đó
- phân biệt `HttpRequestException` (mất mạng) với `TaskCanceledException` (quá lâu) —
  hai câu khác nhau cho người dùng
- có sẵn ánh xạ status → câu tiếng Việt **có tính hành động**, gồm cả **`409`**
- **không bao giờ giả vờ thành công** bằng một danh sách rỗng

> 🚨 Đọc doc-comment trong `ApiCall.cs` trước khi sửa. Nó ghi rõ cái bẫy mà class này
> sinh ra để chặn: `OrderClientService` từng trả `new PagedResult<T>()` khi request
> hỏng, nên người dùng thấy *"bạn chưa có đơn hàng nào"* trong khi thật ra token hết
> hạn. **Loại lỗi tệ nhất vì nó NÓI DỐI** — người dùng không có lý do gì để thử lại.

---

## 4. Khuôn đổi — ví dụ thật từ repo

**Trước** (`Services/Supplier/SupplierClientService.cs:40-48`):

```csharp
var result = await _httpClient
    .GetFromJsonAsync<ApiResult<PagedResult<SupplierDto>>>(url);
return result ?? ApiResult<PagedResult<SupplierDto>>.Fail("Không thể tải danh sách nhà cung cấp.");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách nhà cung cấp");
    return ApiResult<PagedResult<SupplierDto>>.Fail("Không tải được danh sách nhà cung cấp. Vui lòng thử lại.");
}
```

**Sau** — khuôn của `OrderClientService.cs:56-58`:

```csharp
return await ApiCall.SendAsync<PagedResult<SupplierDto>>(
    () => _httpClient.GetAsync(url),
    "tải danh sách nhà cung cấp");
```

Ba điều về khuôn này:

1. **`GetAsync`, không phải `GetFromJsonAsync`.** Đây là cả điểm mấu chốt —
   `GetAsync` không ném, nên `ApiCall` còn đọc được thân phản hồi.
2. **Tham số `action` là một cụm động từ thường**, ghép được vào câu: `"tải danh sách
   nhà cung cấp"` → *"Không kết nối được tới máy chủ khi tải danh sách nhà cung cấp."*
   Đừng viết hoa, đừng chấm câu.
3. **Khối `try/catch` biến mất.** `ApiCall` đã bắt hết. Giữ lại `catch` là tái tạo
   đúng bức tường mà việc này sinh ra để dỡ.

---

## 5. Bẫy — đọc trước khi gõ

1. **6 client service chưa có `ILogger<T>`** — và đó đúng là 6 lớp nhiều vấn đề nhất:
   `BuildPc`, `Image`, `InventoryExport`, `Cart`, `UserAddress`, `Review`.
   `ApiCall.SendAsync` **không tự ghi log**. Bỏ `catch` mà không có logger ở đâu đó là
   **vứt sạch chẩn đoán** — đúng cái mà mục 🅸 đã cảnh báo. Với 6 lớp này, **inject
   `ILogger<T>` trước**, rồi mới đổi.

2. **Đừng đụng `PostAsJsonAsync` / `PutAsJsonAsync`.** Chúng **không** tự gọi
   `EnsureSuccessStatusCode`, nên không mắc lỗi này. Gộp vào là mở rộng phạm vi mà
   không mở rộng giá trị.

3. **Cổng chống hồi quy phải giữ xanh.** Trạng thái hiện tại đo được:
   ```
   Sạch [all]: 252 file, 0 chỗ chở ex.Message của hạ tầng ra cho người dùng.   (exit 0)
   ```
   Chạy lại sau mỗi lô: `bash devops/scripts/check-error-message-leaks.sh`
   🚨 **Mã thoát `2` = KHÔNG KẾT LUẬN, không phải sạch.**
   🚨 **Đừng thay nó bằng `grep 'ex.Message'`** — grep không biết dòng đó nằm trong khối
   `catch` **nào**, nên nó đếm cả 41 chỗ relay **đúng** thành lỗi.

4. **`ApiResult<T>` lồng nhau.** Kiểu generic truyền cho `SendAsync<T>` là `T` **bên
   trong** `ApiResult`, không phải cả `ApiResult<T>`. Sai chỗ này thì deserialize ra
   `null` và bạn nhận câu *"Máy chủ trả về dữ liệu không đọc được"* — trông như lỗi
   mạng, thật ra là lỗi kiểu.

5. **Không có test tự động ở repo này.** `grep` chỉ chứng minh **hình dạng code**. Muốn
   chứng minh hành vi thì phải **ép server trả 400/409 và nhìn bằng mắt** xem câu tiếng
   Việt của server có tới UI không. Ít nhất một lần, cho một endpoint.

---

## 6. Thứ tự làm

| Lô | Việc | Vì sao thứ tự này |
|---|---|---|
| **0** | Inject `ILogger<T>` vào 6 lớp thiếu | Không có nó thì bỏ `catch` = mất chẩn đoán |
| **1** | `Supplier` (2) + `Category` (2) + `Voucher` (2) | Nhỏ, độc lập, có `ILogger` sẵn → dựng nhịp |
| **2** | `Analytics` (6) + `InventoryCheck` (4) | Hai file nặng nhất, chiếm 1/4 tổng số |
| **3** | 7 service `.cs` còn lại (19 lời gọi) | Cơ học, lặp lại khuôn |
| **4** | **5 lời gọi trong 3 file `.razor`** | **Việc khác**: chuyển vào service trước |

Sau mỗi lô: `dotnet build PBL3.sln` + chạy cổng chống hồi quy.

---

## 7. Xong là như thế nào

- [ ] `grep -rn "GetFromJsonAsync" src/Client/ | wc -l` → **0**
- [ ] `dotnet build PBL3.sln` → `0 Error(s)`
- [ ] `check-error-message-leaks.sh` → exit **0**, `0 chỗ`
- [ ] Không `.razor` nào còn gọi HTTP thẳng
- [ ] **Ít nhất 1 lần đo bằng mắt**: ép server trả `400`, xác nhận câu tiếng Việt của
      server hiện lên UI (không phải câu chung chung). Ghi vào `docs/evidence/ui/`.
- [ ] `CLAUDE.md` sửa **35 → 40** ở cả hai chỗ, rồi đánh dấu mục 🅹 xong

---

## 8. Đọc theo thứ tự

| # | File | Để biết |
|---|---|---|
| 1 | file này | việc cần làm |
| 2 | `src/Client/Services/Common/ApiCall.cs` | đích đến, và cái bẫy nó chặn |
| 3 | `src/Client/Services/Orders/OrderClientService.cs:54-58` | **bản mẫu đúng duy nhất** đang có |
| 4 | `CLAUDE.md`, mục "Coding Conventions" | luật về thông báo lỗi tiếng Việt |
| 5 | `docs/nhat-ky-sua-loi-nang-cap.md` §6.4 | vì sao điểm mù `.razor` từng xảy ra |
