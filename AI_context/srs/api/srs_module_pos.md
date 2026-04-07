# Use Case: Bán hàng tại quầy

**ID**: UC009
**Name**: Bán hàng tại quầy
**Primary Actor**: Nhân viên bán hàng
**Secondary Actor**: Hệ thống Kho, Hệ thống Bảo hành, Hệ thống Thanh toán (Ngân hàng/Ví điện tử)

**Description**: Nhân viên thực hiện quét mã sản phẩm, tra cứu khách hàng, tính tiền, in hóa đơn và nhận thanh toán từ khách trực tiếp tại quầy POS.

**Preconditions**:
1. Nhân viên đã đăng nhập vào hệ thống POS.
2. Hệ thống POS đang ở trạng thái sẵn sàng (Online).
3. Máy quét mã vạch và máy in hóa đơn đã được kết nối.

**Trigger**: Khách hàng mang sản phẩm tới quầy để thanh toán.

**Main Success Scenario (Happy Path)**:
1. Nhân viên quét mã vạch sản phẩm hoặc nhập thủ công mã Seri/IMEI/Mã linh kiện.
2. Hệ thống kiểm tra, thêm sản phẩm vào danh sách chờ và hiển thị thông tin (Tên, Đơn giá, mã Seri).
3. Hệ thống tự động tính toán tổng tiền tạm thời.
4. Nhân viên lặp lại bước 1-3 cho đến khi hết sản phẩm.
5. Nhân viên nhấn nút “Thanh toán”.
6. Hệ thống hiển thị form nhập thông tin khách hàng.
7. Nhân viên nhập số điện thoại để tìm kiếm thông tin khách hàng.
8. Hệ thống truy xuất và hiển thị thông tin (Tên, Hạng thành viên, Điểm tích lũy).
9. Nhân viên (Tùy chọn) áp dụng mã Voucher hoặc chọn trừ điểm tích lũy theo yêu cầu của khách.
10. Hệ thống tính toán lại tổng tiền cuối cùng và hiển thị các phương thức thanh toán.
11. Nhân viên chọn phương thức thanh toán (Tiền mặt, Chuyển khoản, Quẹt thẻ).
12. Nhân viên xác nhận đã nhận đủ tiền và nhấn “Hoàn tất đơn hàng”.
13. Hệ thống thực hiện:
    - Trừ tồn kho theo đúng mã Seri/Mã linh kiện.
    - Lưu thông tin kích hoạt bảo hành điện tử (Ngày bắt đầu = ngày hiện tại).
    - Tạo giao dịch thanh toán và lưu lịch sử mua hàng.
    - Mở két tiền (nếu thanh toán tiền mặt) và in Hóa đơn/Phiếu xuất kho.
14. Use case kết thúc.

**Alternative Flows**:

*   **A1: Chỉnh sửa danh sách sản phẩm (tại bước 4)**
    1. Nhân viên thay đổi số lượng (đối với linh kiện không có mã Seri) hoặc nhấn “Xóa” để loại bỏ sản phẩm.
    2. Hệ thống cập nhật lại danh sách và tổng tiền.
    3. Tiếp tục tại bước 4.

*   **A2: Khách vãng lai (tại bước 7)**
    1. Nhân viên chọn mục “Khách lẻ” hoặc bỏ qua nhập số điện thoại.
    2. Hệ thống ghi nhận đơn hàng cho khách vãng lai và không tích điểm.
    3. Tiếp tục tại bước 9.

*   **A3: Khách hàng mới (tại bước 7)**
    1. Hệ thống thông báo không tìm thấy thông tin khách hàng.
    2. Nhân viên chọn “Thêm khách hàng mới” (Kích hoạt UC010).
    3. Sau khi hoàn tất UC010, hệ thống tự động điền thông tin khách mới vào đơn hàng.
    4. Tiếp tục tại bước 9.

**Exception Flows**:

*   **E1: Mã Seri không hợp lệ hoặc đã bán (tại bước 2)**
    1. Hệ thống phát hiện mã Serial không tồn tại trong kho hoặc trạng thái không phải “Trong kho”.
    2. Hệ thống hiển thị cảnh báo lỗi.
    3. Nhân viên kiểm tra lại sản phẩm hoặc đổi sản phẩm khác.
    4. Quay lại bước 1.

*   **E2: Lưu đơn hàng tạm thời (tại bước 9)**
    1. Nhân viên nhấn nút “Lưu tạm” (do khách cần đi lấy thêm đồ hoặc chưa đủ tiền).
    2. Hệ thống lưu trạng thái đơn vào danh sách “Đơn chờ” và giải phóng màn hình.
    3. Khi khách quay lại, nhân viên mở “Đơn chờ” và tải lại dữ liệu.
    4. Nếu khách lấy thêm đồ, quay lại bước 1. Nếu thanh toán ngay, tiếp tục tại bước 9.

*   **E3: Thanh toán thất bại (tại bước 12)**
    1. Hệ thống nhận thông báo lỗi từ cổng thanh toán (Thẻ bị từ chối, QR hết hạn, lỗi kết nối ngân hàng).
    2. Hệ thống hiển thị thông báo "Thanh toán không thành công" và lý do (nếu có).
    3. Nhân viên trao đổi với khách và chọn phương thức thanh toán khác.
    4. Quay lại bước 11.

**Postconditions**:
- Đơn hàng được lưu vào hệ thống với trạng thái "Hoàn tất".
- Số lượng tồn kho và trạng thái mã Seri được cập nhật chính xác.
- Thông tin bảo hành được gán cho số điện thoại khách hàng.
- Hóa đơn được in và trao cho khách hàng.

**Business Rules**:
1. Sản phẩm có mã Seri/IMEI: Mỗi dòng chỉ cho phép số lượng là 1.
2. Linh kiện generic: Cho phép thay đổi số lượng tùy ý (trong giới hạn tồn kho).
3. Bảo hành điện tử: Ngày kích hoạt mặc định là ngày in hóa đơn.
4. Voucher & Điểm thưởng: Áp dụng tối đa 01 mã Voucher cho mỗi đơn hàng; Không áp dụng đồng thời Voucher và trừ điểm tích lũy trong cùng một đơn.
