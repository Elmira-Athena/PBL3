# Mục 🅸 — 124 chỗ rò rỉ `ex.Message` ở tầng Client: **đã sửa, đã đo hai ca đối chứng**

> **Vì sao "chốt Sạch" một mình là chưa đủ.** `check-error-message-leaks.sh client` chỉ chứng
> minh **chuỗi đã biến mất khỏi mã nguồn**. Nó không chứng minh người dùng nhìn thấy gì, và
> tuyệt đối không chứng minh rằng việc thay chuỗi **không nuốt mất câu nghiệp vụ** mà server
> đã soạn — chính là bẫy #13, cái bẫy đã suýt cắn ở mục 🅷.
>
> Vì vậy phải đo **hai nửa ngược chiều nhau của cùng một bất biến**, trên **cùng một nút**:
>
> | | Ca | Bất biến cần giữ |
> |---|---|---|
> | **A** | API **tắt** → lỗi transport | Người dùng nhận **câu tiếng Việt cố định**, **không** nhận chuỗi tiếng Anh của `HttpRequestException` |
> | **B** | API **bật**, server trả `400` kèm message nghiệp vụ | Người dùng nhận **câu của server**, **không** bị câu cố định của client đè lên |
>
> Chỉ đo A là chứng minh được "hết tiếng Anh" nhưng bỏ lọt khả năng đã nuốt sạch mọi thông báo
> nghiệp vụ — một hồi quy UX nặng hơn lỗi ban đầu.
>
> **Kết quả: A ĐẠT · B ĐẠT.**

- **Ngày đo:** 2026-09-01 · API `http://localhost:5222`, Blazor client `http://localhost:5214`
- **Nút đo:** `Pages/Auth/Login.razor` · **Đăng nhập** — chọn nút này vì nó là nút duy nhất mà
  **cả hai** ca đều đi qua đúng một dòng hiển thị: [`Login.razor:120`](../../../src/Client/Pages/Auth/Login.razor#L120)
  `Snackbar.Add(result.Message ?? "Đăng nhập thất bại.", Severity.Error)`.
  Câu người dùng thấy **chính là** `ApiResult.Message` — không có tầng nào ở giữa biên tập lại,
  nên phép đo không thể ra kết quả đúng vì lý do sai.
- **Dữ liệu:** không cần seed. Tài khoản dùng để ép lỗi: `khong-ton-tai@hushstore.vn` (không tồn tại).

---

## 1. Số trước / sau

| Đại lượng | Trước | Sau |
|---|---|---|
| Chỗ chở `ex.Message` của hạ tầng ra người dùng (`client`) | **124** | **0** |
| — biến thể `$"Lỗi kết nối: {ex.Message}"` | 99 | 0 |
| — biến thể `$"Lỗi: {ex.Message}"` | 25 | 0 |
| File phải đụng | 18 | 18 (đã sửa hết) |
| Client service có inject `ILogger` | **0** | **18** |
| `check-error-message-leaks.sh server` | Sạch | Sạch (không hồi quy) |
| `dotnet build PBL3.sln` | 0 Error / 184 cảnh báo | **0 Error / 184 cảnh báo** (không thêm cảnh báo nào) |

Số 124 khớp **chính xác** ở cả ba cách đếm độc lập: `grep -c` trước khi sửa, tổng theo file, và
số lần thay của script. Script sửa **fail-closed**: nó thoát lỗi nếu gặp một method không có
trong bảng thông báo, hoặc nếu bảng có method không tìm thấy trong file — nên không có chỗ nào
bị thay bằng câu chung chung một cách im lặng.

---

## 2. Ca A — API **tắt**, lỗi transport

```
lsof -ti tcp:5222 | xargs -r kill -9     # tắt API
→ bấm "Đăng nhập"
```

**Snackbar hiện ra** (ghi lại bằng `MutationObserver` gắn lên `#mud-snackbar-container`, vì
snackbar tự tắt sau ~5s và biến mất trước khi kịp chụp):

```
Không thực hiện được đăng nhập do lỗi kết nối. Vui lòng kiểm tra đường truyền rồi thử lại.
```

- Khớp `/Response status code|No connection|TypeError|Failed to fetch|net::ERR/i` → **`false`** ✅
- Ảnh: [`2026-09-01-muc-I-ca-doi-chung-am-api-tat.png`](2026-09-01-muc-I-ca-doi-chung-am-api-tat.png)

**Trước khi sửa, đúng nút này sẽ hiện:**
`"Lỗi kết nối: TypeError: Failed to fetch"` — tiếng Anh, và không nói người dùng nên làm gì.

### `ex` không bốc hơi — đây là nửa mà "thay chuỗi" hay làm hỏng

Vì client service **không** có `ILogger` (0/18 trước khi sửa), cách sửa ngây thơ "thay chuỗi
bằng câu cố định" sẽ **vứt sạch chẩn đoán**: không log, không stack trace, không gì cả. Console
trình duyệt sau khi sửa:

```
fail: Client.Services.Auth.AuthClientService[0]
      Lỗi khi đăng nhập.
System.Net.Http.HttpRequestException: TypeError: Failed to fetch
 ---> TypeError: Failed to fetch
   at System.Net.Http.BrowserHttpController.CallFetch()
   at Client.Auth.AuthHeaderHandler.SendAsync(...) in src/Client/Auth/AuthHeaderHandler.cs:line 54
   at Client.Services.Auth.AuthClientService.LoginAsync(...) in src/Client/Services/Auth/AuthClientService.cs:line 48
```

Chi tiết kỹ thuật đi vào `ILogger<T>` (console trình duyệt), câu tiếng Việt đi ra `ISnackbar` —
đúng khuôn hai tầng của mục 🅷, nay áp cho tầng thứ ba.

---

## 3. Ca B — API **bật**, server trả `400` kèm message nghiệp vụ

```
POST http://localhost:5222/api/auth/login → 400
→ bấm "Đăng nhập" với tài khoản không tồn tại
```

**Snackbar hiện ra:**

```
Tài khoản hoặc mật khẩu không đúng.
```

Đây là câu **do server soạn**, về nguyên vẹn qua thân phản hồi. Câu cố định của client
(`"Không thực hiện được đăng nhập do lỗi kết nối…"`) **không** xuất hiện — đúng như phải thế,
vì đường `PostAsJsonAsync` + `ReadFromJsonAsync` **không ném** khi gặp `400`, nên khối `catch`
không bao giờ chạy.

✅ **Bẫy #13 không cắn.** Sửa 124 chỗ mà **không** nuốt mất một câu nghiệp vụ nào.

---

## 4. Hai file khó nhất đã có bằng chứng chạy thật

16/18 file dùng đúng một khuôn ctor (`(HttpClient httpClient)`), sửa bằng script. Hai file
lệch khuôn phải sửa tay — và **cả hai đều đã tự chứng minh ở runtime**, không chỉ ở build:

| File | Vì sao lệch khuôn | Bằng chứng runtime |
|---|---|---|
| `AuthClientService` | ctor **6 tham số** (localStorage, authStateProvider, navigationManager, …) | dòng log `Client.Services.Auth.AuthClientService[0]` ở §2 |
| `StorefrontClientService` | **primary constructor** (`class X(HttpClient httpClient) : IX`), không có khối ctor để chèn | dòng log `Client.Services.Storefront.StorefrontClientService[0] — Lỗi khi tải menu danh mục.` |

Cả hai in ra được nghĩa là DI đã resolve `ILogger<T>` thật. Build sạch **không** chứng minh
điều này: một `ILogger<T>` chưa đăng ký sẽ hỏng **lúc chạy**, không lúc biên dịch.

---

## 5. Một chi tiết đo suýt bị đọc nhầm

Lần chạy đầu, console có thêm:

```
Uncaught TypeError: Cannot read properties of undefined (reading 'trim')
```

Nó **không phải lỗi của sản phẩm** — nó là của chính `MutationObserver` mà phép đo cắm vào
(`n.innerText.trim()` trên node MudBlazor chèn thêm). Đã xác minh bằng cách **tải lại trang để
gỡ observer rồi bấm lại**: lỗi biến mất, hai dòng `fail:` của `ILogger` vẫn còn.

> Ghi lại vì đây đúng là kiểu nhiễu mà phép đo tự tạo ra rồi bị tính thành phát hiện. Quy tắc
> rút ra: **ca đo cuối cùng phải chạy trên trang sạch, không còn dụng cụ đo cắm vào.**

---

## 6. Việc CỐ Ý chưa làm — đừng đọc file này thành "tầng Client đã xong"

**35 lời gọi `GetFromJsonAsync` vẫn đang vứt thân phản hồi.** `GetFromJsonAsync` tự gọi
`EnsureSuccessStatusCode` bên trong, nên khi server trả `400` kèm câu tiếng Việt thì câu đó
**mất trước khi vào `catch`**. Sau khi sửa, 35 chỗ này hiện câu cố định của client thay vì chuỗi
tiếng Anh — **đỡ hơn, nhưng vẫn mất câu server đã soạn**.

Ca B ở §3 đo đường `Post`, **không** đo đường `Get`, nên nó **không** chứng minh gì cho 35 chỗ
đó. Đây là giới hạn đã biết của bằng chứng này.

Việc chuyển 35 chỗ sang `ApiCall.SendAsync` là **mục 🅹**, cố ý để ngoài gói 1 (refactor 35
call-site, mỗi chỗ một verb/payload). Xem mục 🅹 của `docs/bat-dau-phien-moi.md`.
