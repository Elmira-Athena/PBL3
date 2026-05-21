# ĐẶC TẢ USE CASE: KIỂM KÊ KHO HÀNG (INVENTORY AUDIT)

- **Mã Use case:** UC015
- **Tên Use case:** Kiểm kê kho hàng (Inventory Audit & Stocktaking)
- **Tác nhân chính (Primary Actor):** Employee (Nhân viên)
- **Tác nhân phụ (Secondary Actor):** Admin (Quản trị viên), Hệ thống (Backend System)
- **Mô tả:** Đối chiếu số lượng và mã Serial của hàng hóa thực tế trên kệ so với số liệu tồn kho lý thuyết (Snapshot) trên phần mềm tại thời điểm chốt, lập phiếu kiểm kê ghi nhận chênh lệch và thực hiện phê duyệt cân bằng kho để đồng bộ số liệu thực tế.
- **Sự kiện kích hoạt (Trigger):** Đến kỳ kiểm kê định kỳ (cuối tháng/quý) hoặc có yêu cầu kiểm kê đột xuất khi phát hiện nghi ngờ sai lệch hàng hóa.
- **Điều kiện tiên quyết (Preconditions):**
  1. Người dùng đã đăng nhập vào hệ thống với vai trò hợp lệ (Employee hoặc Admin).
  2. Các phiếu Nhập kho (`ImportReceipt`) và Xuất kho trước thời điểm chốt kiểm kê phải được hoàn tất (trạng thái lưu nháp phải được xử lý xong).
  3. Có thiết bị quét mã vạch (Barcode/QR scanner) hoạt động bình thường và kết nối với Blazor Client.

---

## Luồng sự kiện chính (Main Success Scenario)

| STT | Thực hiện bởi | Hành động |
|:---:|---|---|
| 1. | Employee | Chọn **"Tạo phiếu kiểm kê mới"**, chọn phạm vi kiểm kê (Toàn bộ kho hoặc theo Danh mục sản phẩm/Kệ hàng cụ thể) và nhập ghi chú (nếu có). |
| 2. | Hệ thống | Tạo phiếu kiểm kê mới ở trạng thái **`0: Draft` (Nháp)**, tự động sinh mã phiếu (VD: `KK-20260521-001`).<br>Đồng thời thực hiện chụp **Chốt số liệu lý thuyết (Theoretical Stock Snapshot)** của toàn bộ các biến thể sản phẩm (`ProductVariant`) trong phạm vi kiểm kê tại thời điểm đó (`SystemQuantity`). |
| 3. | Employee | Sử dụng máy quét mã vạch để quét lần lượt mã Serial Number (`SerialNumber`) của các sản phẩm thực tế đang nằm trên kệ. |
| 4. | Hệ thống | Với mỗi mã Serial được quét thành công:<br>1. Tra cứu thông tin Serial trong bảng `ProductSerials`.<br>2. Ghi nhận mã Serial vào danh sách kiểm đếm thực tế của phiếu kiểm kê (Lưu vào bảng `InventoryCheckDetailSerial` với trạng thái quét ban đầu là Khớp).<br>3. Tự động cập nhật số lượng kiểm đếm thực tế `ActualQuantity` của biến thể tương ứng trong bảng `InventoryCheckDetail`. |
| 5. | Hệ thống | Hiển thị bảng đối chiếu chênh lệch theo thời gian thực (Real-time Audit Dashboard):<br>- **Khớp (Matched):** Serial có trạng thái `Available` trong hệ thống và được quét thấy thực tế.<br>- **Thiếu (Missing):** Serial trạng thái `Available` trong hệ thống nhưng chưa được quét thấy.<br>- **Thừa (Surplus):** Quét thấy Serial thực tế nhưng hệ thống báo không có ở trạng thái `Available` (có thể đã bán, giữ chỗ hoặc hoàn toàn mới).<br>- **Lỗi (Defective):** Serial thực tế quét thấy nhưng sản phẩm bị lỗi vật lý (do nhân viên đánh dấu). |
| 6. | Employee | Sau khi quét xong toàn bộ khu vực, kiểm tra lại các mục bị lệch (Thừa/Thiếu), nhập **"Lý do chênh lệch"** cho từng mặt hàng và đề xuất hướng xử lý cho các Serial lệch. Nhấn nút **"Gửi duyệt phiếu"**. |
| 7. | Hệ thống | Chuyển trạng thái phiếu kiểm kê sang **`1: AwaitingApproval` (Chờ duyệt)** và khóa quyền chỉnh sửa số liệu kiểm đếm của Employee. Gửi thông báo đến Admin. |
| 8. | Admin | Đăng nhập hệ thống, mở phiếu kiểm kê ở trạng thái Chờ duyệt để xem xét báo cáo chênh lệch, các lý do thất thoát và hướng xử lý đề xuất của từng Serial lệch. (Có thể yêu cầu đếm lại nếu chênh lệch quá lớn). |
| 9. | Admin | Xác nhận đồng ý với kết quả kiểm kê và nhấn nút **"Phê duyệt & Cân bằng kho"**. |
| 10. | Hệ thống | Mở Database Transaction và thực hiện tự động cân bằng kho:<br>1. **Đối với Serial thiếu (`Missing`):** Chuyển `Status` của Serial trong bảng `ProductSerials` sang **`5: Lost` (Thất thoát)**. Lưu thông tin phiếu kiểm kê làm mất.<br>2. **Đối với Serial lỗi (`Defective`):** Chuyển `Status` của Serial trong bảng `ProductSerials` sang **`3: Defective` (Lỗi/Hỏng)**.<br>3. **Đối với Serial thừa (`Surplus`):** Thực hiện xử lý theo hướng xử lý đã duyệt (Khôi phục từ `Sold` thành `Available`, hủy giữ chỗ `Reserved`, hoặc tạo mới Serial thừa kiểm kê).<br>4. **Đồng bộ tồn kho:** Tính toán và cập nhật lại cột `StockQuantity` vật lý trong bảng `ProductVariants` bằng cách đếm lại số Serial có trạng thái `0: Available`.<br>5. **Lưu lịch sử:** Tạo bút toán điều chỉnh kho (Inventory Adjustment Log) để hạch toán chênh lệch lỗ/lãi kiểm kê cho kế toán. |
| 11. | Hệ thống | Chuyển trạng thái phiếu kiểm kê sang **`2: Completed` (Đã hoàn tất)**, ghi nhận người phê duyệt (`ApprovedBy`, `ApprovedAt`) và hiển thị thông báo thành công. Kết thúc Use case. |

---

## Luồng rẽ nhánh (Alternative Flows)

### **A1: Quét trúng Serial đã bán (`Sold`) (tại Bước 4)**
*   **A1.1.** Hệ thống phát hiện Serial vừa quét có trạng thái `2: Sold` trong DB (đã bán trong quá khứ).
*   **A1.2.** Hệ thống hiển thị cảnh báo: *"Serial [SN-XXX] đã được bán tại Đơn hàng [Order-YYY] ngày [Date]"* và tự động đánh dấu trạng thái quét của Serial này là **`Surplus` (Thừa kiểm kê)**.
*   **A1.3.** Hệ thống gợi ý hướng xử lý cho Employee:
    *   *Lựa chọn A:* Khách mang trả hàng bảo hành/đổi trả nhưng chưa nhập kho &rarr; Ghi nhận và đề xuất làm **Phiếu nhập trả hàng (Customer Return)** sau kiểm kê.
    *   *Lựa chọn B:* Sai sót lúc giao hàng (nhân viên lấy nhầm máy khác giao cho khách, máy này vẫn ở lại kho) &rarr; Đề xuất cập nhật lại Serial chính xác cho Đơn hàng cũ.
*   **A1.4.** Employee chọn lựa chọn phù hợp và tiếp tục quét.

### **A2: Quét trúng Serial đang giữ chỗ (`Reserved`) (tại Bước 4)**
*   **A2.1.** Hệ thống phát hiện Serial vừa quét có trạng thái `1: Reserved` trong DB (đã gán cho một đơn hàng online chưa hoàn tất hoặc đơn POS chưa thanh toán).
*   **A2.2.** Hệ thống hiển thị cảnh báo: *"Serial [SN-XXX] đã được giữ chỗ cho Đơn hàng [Order-ZZZ]"*.
*   **A2.3.** Hệ thống đưa ra chỉ dẫn:
    *   Yêu cầu nhân viên kho kiểm tra xem sản phẩm này có đang nằm nhầm vị trí sẵn bán không. Nếu đơn hàng online vẫn đang active, sản phẩm này phải được chuyển sang khu vực "Đóng gói/Chờ ship", không được tính vào kho sẵn bán lẻ.
    *   Nếu đơn hàng online đã bị hủy nhưng hệ thống chưa giải phóng Serial &rarr; Đề xuất duyệt **Giải phóng giữ chỗ (Release Reservation)** để Serial quay lại trạng thái `Available`.
*   **A2.4.** Employee xác nhận vị trí vật lý của máy và tiếp tục quét.

### **A3: Quét trúng Serial hoàn toàn lạ (Không tồn tại trong DB) (tại Bước 4)**
*   **A3.1.** Hệ thống phát hiện Serial vừa quét không có trong bảng `ProductSerials`.
*   **A3.2.** Hệ thống hiển thị cảnh báo: *"Mã Serial [SN-XYZ] không tồn tại trên hệ thống. Vui lòng xác nhận sản phẩm này thuộc Biến thể nào"*.
*   **A3.3.** Employee chọn Biến thể sản phẩm (`ProductVariant`) tương ứng trên giao diện Blazor.
*   **A3.4.** Hệ thống ghi nhận Serial này dưới trạng thái **`Surplus` (Thừa kiểm kê - Mã lạ)** và yêu cầu nhập giá vốn ước tính khi phê duyệt để hạch toán tăng tài sản.

### **A4: Tạm dừng kiểm kê (Lưu nháp) (tại Bước 6)**
*   **A4.1.** Employee chưa quét xong hoặc hết giờ làm việc, nhấn nút **"Lưu nháp"**.
*   **A4.2.** Hệ thống lưu lại toàn bộ danh sách Serial đã quét và số lượng tương ứng, giữ nguyên trạng thái phiếu là `0: Draft`. Kết thúc phiên làm việc.
*   **A4.3.** Ở phiên làm việc sau, Employee mở lại phiếu nháp này và tiếp tục quét các kệ tiếp theo (Quay lại Bước 3).

### **A5: Ghi nhận hàng lỗi phát hiện khi quét (tại Bước 4-5)**
*   **A5.1.** Employee phát hiện sản phẩm trên kệ bị móp méo, trầy xước hoặc hỏng hóc vật lý.
*   **A5.2.** Trên giao diện đối chiếu, Employee tích chọn đánh dấu Serial đó là **`Defective` (Lỗi vật lý)**.
*   **A5.3.** Hệ thống ghi nhận trạng thái kiểm kê của Serial là Lỗi. Khi duyệt phiếu ở bước 10, Serial này sẽ được chuyển sang trạng thái `3: Defective` (Kho hàng lỗi) để chờ thanh lý hoặc gửi trả nhà cung cấp bảo hành, không cộng vào kho sẵn sàng bán.

### **A6: Quản lý từ chối phê duyệt (Reject) (tại Bước 8-9)**
*   **A6.1.** Admin nhận thấy chênh lệch quá lớn hoặc lý do nhân viên giải trình chưa thỏa đáng, nhấn nút **"Từ chối phê duyệt"** và nhập lý do từ chối.
*   **A6.2.** Hệ thống chuyển trạng thái phiếu kiểm kê ngược lại **`0: Draft` (Nháp)** hoặc **`3: Cancelled` (Đã hủy)**.
*   **A6.3.** Nếu chuyển về Nháp, hệ thống cho phép Employee tiến hành quét và kiểm đếm lại (Re-count) từ đầu. Nếu hủy, phiếu kiểm kê đóng lại và không có bất kỳ thay đổi tồn kho nào được ghi nhận xuống DB.

---

## Hậu điều kiện (Postconditions)
1. Số liệu tồn kho vật lý (`StockQuantity` của `ProductVariant`) trên phần mềm khớp chính xác 100% với số lượng thực tế trên kệ.
2. Trạng thái của các mã Serial (`ProductSerial.Status`) bị lệch được điều chỉnh đúng đắn về trạng thái thực tế (`Lost`, `Defective`, `Available`).
3. Một Phiếu kiểm kê ở trạng thái `Completed` được lưu lại vĩnh viễn, lưu vết rõ ràng nhân viên kiểm kê, quản lý phê duyệt, danh sách chi tiết các Serial bị điều chỉnh và lý do chênh lệch để làm cơ sở đối chiếu tài chính/kế toán.

---

## Quy tắc nghiệp vụ (Business Rules)

1.  **Quy tắc Chốt số liệu (Snapshot Rule):** Hệ thống áp dụng kiểm kê theo thời điểm chốt (Snapshot-based). Khi tạo phiếu kiểm kê, hệ thống lưu lại số lượng tồn kho lý thuyết tại giây đó. Mọi hoạt động bán hàng (POS, Online) phát sinh sau thời điểm chốt vẫn diễn ra bình thường, hệ thống sẽ sử dụng timestamp giao dịch để đối chiếu chênh lệch thông minh, đảm bảo **không gián đoạn kinh doanh (Business Continuity)**.
2.  **Quy tắc phân quyền (Separation of Duties - SoD):** Employee chỉ được phép kiểm đếm, quét mã và đề xuất giải trình. Quyền phê duyệt cân bằng kho và ghi nhận thất thoát tài sản bắt buộc phải thuộc về Admin để chống gian lận.
3.  **Quy tắc bảo toàn dữ liệu (Data Integrity):** Tuyệt đối không được phép "Xóa cứng" (Hard Delete) bất kỳ mã Serial nào bị mất khỏi cơ sở dữ liệu. Serial bị thiếu bắt buộc phải chuyển sang trạng thái `5: Lost` để lưu giữ vết lịch sử nhập kho ban đầu, phục vụ công tác điều tra thất thoát.
4.  **Quy tắc kiểm kê cộng tác (Collaborative Stocktaking):** Đối với kho lớn, hệ thống cho phép nhiều Employee sử dụng các thiết bị quét khác nhau cùng đăng nhập quét trên một phiếu kiểm kê chính (hệ thống sẽ tự động gộp và loại bỏ mã quét trùng lặp), giúp tối ưu hóa thời gian và năng suất kiểm kho.
5.  **Quy tắc tính toán tự động (Computed stock sync):** Số lượng tồn kho sẵn bán (`StockQuantity` trong `ProductVariants`) là trường tính toán động. Không được phép cập nhật thủ công bằng câu lệnh UPDATE trực tiếp, mà phải được đồng bộ tự động thông qua việc đếm tổng số Serial có trạng thái `0: Available` sau khi hoàn tất cân bằng kho.
