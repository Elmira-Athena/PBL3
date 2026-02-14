
# IMPLEMENTATION PLAN: IMPORT RECEIPT UI (PHASE 4)

**Project:** HushStore
**Technology:** Blazor WebAssembly, MudBlazor
**Reference:** Figma `Inbound.png`

---

## 1. MỤC TIÊU & THÁCH THỨC (UI ARCHITECTURE)

* Xây dựng trang Nhập hàng phức hợp (Master-Detail layout).
* **Trạng thái (State Management):** Cần duy trì một object `CreateImportReceiptRequest` (chứa list `Details`, mỗi detail lại chứa list `SerialNumbers`) liên tục trên giao diện mà không load lại trang.
* **Tương tác thời gian thực:** Quét mã vạch bên phải -> Số lượng "Đã nhập" bên trái tự động nhảy số.

---

## 2. CẤU TRÚC COMPONENTS

Nên tạo một trang riêng biệt: `Pages/Admin/Inventory/ImportReceiptForm.razor`.

Sử dụng `<MudGrid>` để chia màn hình làm 2 phần (Giống Figma):

* **Cột Trái (Khu vực Phiếu nhập & Danh sách sản phẩm):** `MudItem xs="12" md="8"`
* **Cột Phải (Khu vực Quét Serial):** `MudItem xs="12" md="4"`

---

## 3. LOGIC XỬ LÝ GIAO DIỆN (CRITICAL RULES)

### 3.1. Cột Trái (Phiếu nhập)

* **Header:**
* `MudSelect` chọn Nhà cung cấp (Load từ `SupplierClientService`).
* Ngày nhập (Mặc định ngày hiện tại, Read-only).


* **Bảng Sản phẩm (Sử dụng `MudTable` nhưng bind vào danh sách In-memory):**
* Nút "Thêm sản phẩm mới" sẽ mở ra một `MudDialog` để search và chọn Variant, sau đó add vào list `Details` hiện tại.
* Cột Số lượng và Giá nhập: Cho phép nhập trực tiếp trên bảng (Dùng `<MudNumericField>`). Tổng tiền = Số lượng * Giá nhập.
* Cột **Đã nhập**: Bằng `Detail.SerialNumbers.Count`. Nếu đủ số lượng thì hiện màu xanh, chưa đủ hiện màu đỏ.
* Cột **Trạng thái (Hành động)**: Có nút "Nhập mã" (Click vào sẽ đổi focus của Cột Phải sang sản phẩm này) và nút Xóa dòng.



### 3.2. Cột Phải (Khu vực Quét Serial)

* **Context (Ngữ cảnh):** Chỉ hiển thị khi user bấm nút "Nhập mã" ở Cột Trái. Biến `ActiveDetail` sẽ lưu trữ dòng sản phẩm đang được chọn.
* **Hiển thị thông tin:** Tên sản phẩm đang quét, Số lượng cần quét (VD: 2 / 3).
* **Input Quét mã:** `<MudTextField>` hứng dữ liệu từ súng bắn mã vạch.
* Lắng nghe sự kiện `OnKeyUp` (Khi súng bắn xong sẽ tự gửi phím Enter).
* Khi Enter: Push mã đó vào mảng `ActiveDetail.SerialNumbers`, sau đó xóa trắng ô input để quét tiếp.
* **Validation ngay trên UI:** Lớp Bảo Vệ (Bắt buộc phải có trong hàm OnSerialNumberScanned):

Check rỗng: Bỏ qua nếu giá trị rỗng.

Check Local: Nếu mã đã nằm trong ActiveDetail.SerialNumbers -> Bắn ISnackbar.Error("Mã này vừa được quét!"), xóa trắng ô nhập.

Check Database: * Bật cờ IsScanning = true (Hiện spinner).

Gọi await ProductSerialClientService.CheckExistAsync(scannedCode).

Nếu true -> Bắn ISnackbar.Error("LỖI: Mã Serial đã tồn tại trong hệ thống!").

Nếu false -> Thêm vào ActiveDetail.SerialNumbers, bắn tiếng "Bíp" (tùy chọn), xóa trắng ô nhập để quét tiếp.

Tắt cờ IsScanning = false


* **Danh sách mã đã quét:** Dùng `MudList` hoặc bảng nhỏ hiển thị các Serial kèm nút Xóa (icon thùng rác).

### 3.3. Nút "Lưu phiếu nhập hàng"

* Vòng lặp check Validation cuối cùng: Đảm bảo `Details.Count > 0` và ở tất cả các dòng, `Detail.Quantity == Detail.SerialNumbers.Count`.
* Gọi API: `ImportReceiptClientService.CreateAsync(request)`.
* Thành công: Chuyển hướng về trang Danh sách phiếu nhập.