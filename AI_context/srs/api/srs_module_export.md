# Use Case Documentation: Xuất kho hàng

**ID**: UC014
**Name**: Xuất kho hàng
**Primary Actor**: Nhân viên (Nhân viên kho)
**Secondary Actor**: Hệ thống

**Description**: Nhân viên tiến hành xuất các sản phẩm vật lý ra khỏi kho dựa trên một "Yêu cầu xuất hàng" (do có Đơn hàng, Chuyển kho, hoặc Bảo hành,...). Quá trình này bao gồm việc quét mã Serial của từng thiết bị thực tế để trừ tồn kho và cập nhật trạng thái thiết bị.

**Preconditions**:
1. Nhân viên đã đăng nhập vào hệ thống với quyền hạn phù hợp (Quản lý kho/Nhân viên kho).
2. Tồn tại ít nhất một "Yêu cầu xuất hàng" đang ở trạng thái chờ xử lý (Chờ xuất kho).
3. Hàng hóa vật lý thực tế có sẵn trong kho để tiến hành xuất.

**Trigger**: Nhân viên chọn chức năng "Xuất kho" trên thanh điều hướng của hệ thống.

**Main Success Scenario (Happy Path)**:
1. Nhân viên chọn chức năng "Xuất kho".
2. Hệ thống hiển thị danh sách các "Yêu cầu xuất hàng" đang chờ xử lý.
3. Nhân viên chọn một yêu cầu xuất hàng cụ thể cần thực hiện.
4. Hệ thống hiển thị chi tiết yêu cầu xuất hàng (danh sách sản phẩm, số lượng cần xuất của từng sản phẩm).
5. Nhân viên chọn thao tác "Quét mã" tại một dòng sản phẩm cần xuất.
6. Hệ thống hiển thị giao diện quét mã cho sản phẩm tương ứng, bao gồm thông tin sản phẩm và tiến độ đếm (ví dụ: 0/5).
7. Nhân viên dùng máy quét để đọc (hoặc nhập tay) mã Serial của thiết bị thực tế.
8. Hệ thống xác thực tính hợp lệ của mã Serial (tồn tại trong hệ thống, đúng mã sản phẩm, đang ở trạng thái "Trong kho").
9. Hệ thống ghi nhận mã Serial thành công, cập nhật tiến độ đếm (ví dụ: 1/5) và hiển thị thông báo/âm thanh thành công.
10. Nhân viên lặp lại các bước từ 7 đến 9 cho đến khi quét đủ số lượng của sản phẩm đó.
11. Nhân viên lặp lại các bước từ 5 đến 10 cho các sản phẩm khác trong yêu cầu xuất hàng (nếu có).
12. Nhân viên chọn "Lưu phiếu xuất hàng" sau khi đã hoàn tất việc quét mã cho toàn bộ sản phẩm.
13. Hệ thống kiểm tra toàn vẹn dữ liệu của phiên xuất kho (đảm bảo đã quét đủ số lượng theo yêu cầu).
14. Hệ thống lưu thông tin phiếu xuất, trừ số lượng tồn kho tương ứng và cập nhật trạng thái các mã Serial vừa quét thành "Đã xuất kho".
15. Hệ thống hiển thị thông báo "Đã lưu phiếu xuất hàng thành công" và quay trở lại danh sách yêu cầu xuất hàng. Use case kết thúc.

**Alternative Flows**:

*   **A1: Xóa một mã Serial đã quét nhầm (tại Bước 9)**
    1. Nhân viên phát hiện quét nhầm và chọn "Xóa" tại mã Serial vừa quét trong danh sách tạm.
    2. Hệ thống loại bỏ mã Serial đó khỏi danh sách tạm, giảm tiến độ đếm (ví dụ: từ 2/5 về 1/5).
    3. Resume at Step 7.

*   **A2: Hủy bỏ tiến độ quét của một sản phẩm (tại Bước 10)**
    1. Nhân viên chọn "Làm lại/Xóa tất cả" cho dòng sản phẩm đang quét.
    2. Hệ thống xóa bỏ toàn bộ các mã Serial đã quét tạm thời của sản phẩm đó, cập nhật lại tiến độ về 0.
    3. Resume at Step 5.

*   **A3: Hủy bỏ toàn bộ phiên xuất kho (tại Bước 12)**
    1. Nhân viên chọn "Hủy bỏ".
    2. Hệ thống hiển thị popup: "Bạn có chắc chắn muốn hủy phiên xuất kho này?".
    3. Nhân viên chọn "Xác nhận".
    4. Hệ thống xóa toàn bộ dữ liệu quét tạm thời của yêu cầu này và trở lại danh sách yêu cầu xuất kho ban đầu.
    5. Use case ends.

**Exception Flows**:

*   **E1: Mã Serial không hợp lệ (tại Bước 8)**
    1. Hệ thống kiểm tra và phát hiện lỗi: mã Serial không tồn tại, mã Serial thuộc sản phẩm khác, hoặc mã Serial không ở trạng thái "Trong kho" (đã xuất, đang bảo hành, lỗi...).
    2. Hệ thống phát âm thanh cảnh báo lỗi và hiển thị thông báo chi tiết (Ví dụ: "Sản phẩm không tồn tại hoặc đã được xuất").
    3. Hệ thống từ chối ghi nhận mã Serial này.
    4. Nhân viên bỏ thiết bị lỗi ra và lấy thiết bị khác để quét thay thế.
    5. Resume at Step 7.

*   **E2: Quét thừa số lượng mã Serial (tại Bước 8)**
    1. Nhân viên tiếp tục quét mã Serial khi tiến độ đếm đã đạt tối đa (ví dụ: 5/5).
    2. Hệ thống phát âm thanh cảnh báo và hiển thị thông báo: "Đã quét đủ số lượng cho sản phẩm này".
    3. Hệ thống từ chối ghi nhận mã Serial vừa quét.
    4. Resume at Step 11.

*   **E3: Chưa quét đủ số lượng sản phẩm khi Lưu (tại Bước 13)**
    1. Hệ thống phát hiện tổng số lượng Serial đã quét ít hơn tổng số lượng yêu cầu của phiếu xuất.
    2. Hệ thống hiển thị thông báo lỗi: "Chưa quét đủ số lượng sản phẩm yêu cầu. Vui lòng kiểm tra lại."
    3. Hệ thống chặn việc lưu phiếu xuất.
    4. Resume at Step 5 (Nhân viên tiếp tục chọn sản phẩm còn thiếu để quét bổ sung).

**Postconditions**:
- Thông tin chi tiết của phiếu xuất kho được lưu trữ vĩnh viễn vào cơ sở dữ liệu.
- Số lượng tồn kho thực tế của các sản phẩm tương ứng bị giảm đi.
- Lịch sử giao dịch (transaction log) được tạo để phục vụ đối soát.
- Trạng thái của các mã Serial thiết bị vừa xuất được chuyển từ "Trong kho" sang "Đã xuất kho" (hoặc trạng thái tương ứng với mục đích xuất).
