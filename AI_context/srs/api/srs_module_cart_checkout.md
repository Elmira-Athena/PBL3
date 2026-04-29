# Use Case: Đặt hàng online (Checkout)

**ID**: UC-CHECKOUT-01
**Name**: Đặt hàng online
**Primary Actor**: Khách hàng
**Secondary Actor**: Hệ thống thanh toán trực tuyến (VNPay/Momo/Stripe), Hệ thống gửi Email/SMS

**Description**: Khách hàng tiến hành thanh toán cho các sản phẩm đã chọn từ Giỏ hàng hoặc thông qua tính năng "Mua ngay", cung cấp thông tin giao hàng, áp dụng khuyến mãi và chọn phương thức thanh toán để hoàn tất quá trình tạo đơn hàng.

**Preconditions**:
1. Khách hàng đã đăng nhập thành công vào hệ thống.
2. Hệ thống thanh toán (nếu có) và hệ thống kiểm tra tồn kho đang hoạt động bình thường.
3. Giỏ hàng có ít nhất 1 sản phẩm hợp lệ (nếu thanh toán từ giỏ hàng) hoặc khách hàng đã chọn 1 sản phẩm hợp lệ kèm thuộc tính (nếu Mua ngay).

**Trigger**: Khách hàng nhấn nút "Thanh toán" tại trang Giỏ hàng hoặc nút "Mua ngay" tại trang Chi tiết sản phẩm.

**Main Success Scenario (Happy Path)**:
1. Hệ thống tiếp nhận yêu cầu thanh toán (từ Giỏ hàng hoặc Mua ngay).
2. Hệ thống kiểm tra số lượng tồn kho khả dụng của các sản phẩm/phân loại được chọn.
3. Hệ thống hiển thị trang Thanh toán (Checkout) bao gồm: Danh sách sản phẩm, tạm tính, phí vận chuyển (dự kiến), và tổng tiền.
4. Hệ thống tự động tải và điền thông tin địa chỉ giao hàng mặc định của khách hàng (nếu đã từng lưu).
5. Khách hàng kiểm tra thông tin, có thể chọn một địa chỉ giao hàng khác hoặc thêm địa chỉ mới.
6. Hệ thống tính toán và cập nhật lại phí vận chuyển dựa trên địa chỉ giao hàng vừa chọn.
7. Khách hàng nhập hoặc chọn mã giảm giá/Voucher (nếu có).
8. Hệ thống kiểm tra tính hợp lệ của mã giảm giá và tính toán lại tổng tiền (trừ đi số tiền được giảm).
9. Khách hàng chọn phương thức thanh toán (Thanh toán khi nhận hàng - COD hoặc Thanh toán trực tuyến).
10. Khách hàng có thể nhập thêm ghi chú cho đơn hàng hoặc yêu cầu xuất hóa đơn (tùy chọn).
11. Khách hàng nhấn nút "Xác nhận đặt hàng".
12. Hệ thống kiểm tra lại lần cuối tính hợp lệ của dữ liệu: Số lượng tồn kho, giá trị tổng tiền, thông tin khách hàng.
13. Hệ thống tạo đơn hàng mới trên cơ sở dữ liệu:
    * Nếu chọn COD: Đơn hàng ở trạng thái "Chờ xác nhận".
    * Nếu chọn Thanh toán trực tuyến: Đơn hàng ở trạng thái "Chờ thanh toán".
14. (Dành cho Thanh toán trực tuyến) Hệ thống chuyển hướng khách hàng sang Cổng thanh toán. Khách hàng thực hiện thanh toán thành công và được điều hướng trở lại. Hệ thống cập nhật trạng thái đơn hàng thành "Đã thanh toán".
15. Hệ thống trừ đi số lượng tồn kho tương ứng của các sản phẩm trong đơn hàng (hoặc ghi nhận số lượng tạm giữ).
16. Hệ thống gửi email thông báo xác nhận đơn hàng tới địa chỉ email của khách hàng.
17. Hệ thống hiển thị trang "Đặt hàng thành công" kèm theo mã đơn hàng, thông tin tổng quan và thời gian giao hàng dự kiến.
18. Hệ thống tự động xóa các sản phẩm đã thanh toán thành công khỏi Giỏ hàng (nếu khách mua từ Giỏ hàng).
19. Use case kết thúc.

**Alternative Flows**:

*   **A1: Thêm/Thay đổi địa chỉ giao hàng (tại Bước 5)**
    1. Khách hàng nhấn vào "Thay đổi" tại phần địa chỉ nhận hàng.
    2. Hệ thống hiển thị danh sách các địa chỉ đã lưu và tùy chọn "Thêm địa chỉ mới".
    3. Nếu chọn "Thêm địa chỉ mới", hệ thống hiển thị biểu mẫu nhập thông tin (Tên người nhận, SĐT, Địa chỉ chi tiết, Phường/Xã, Quận/Huyện, Tỉnh/Thành).
    4. Khách hàng nhập thông tin hợp lệ và nhấn "Lưu".
    5. Hệ thống lưu địa chỉ mới, thiết lập làm địa chỉ giao hàng hiện tại.
    6. Quay lại Bước 6 (Tính toán lại phí vận chuyển).

*   **A2: Mã giảm giá/Voucher không hợp lệ (tại Bước 8)**
    1. Hệ thống kiểm tra mã giảm giá và xác định mã không hợp lệ (do hết hạn, hết lượt, hoặc đơn hàng không đủ điều kiện áp dụng).
    2. Hệ thống hiển thị cảnh báo: "Mã giảm giá không hợp lệ hoặc không đủ điều kiện áp dụng".
    3. Hệ thống giữ nguyên tổng tiền ban đầu.
    4. Khách hàng có thể nhập một mã khác hoặc bỏ trống mã giảm giá.
    5. Quay lại Bước 9.

*   **A3: Khách hàng hủy bỏ quá trình thanh toán (tại Bước 11)**
    1. Khách hàng quyết định không mua nữa và nhấn nút "Quay lại" hoặc quay về trang chủ.
    2. Hệ thống hủy bỏ phiên làm việc thanh toán hiện tại. Không có đơn hàng nào được tạo ra.
    3. Các sản phẩm vẫn được giữ nguyên trong Giỏ hàng (nếu xuất phát từ Giỏ hàng).
    4. Use case kết thúc.

*   **A4: Sản phẩm hết hàng hoặc không đủ số lượng tồn kho (tại Bước 2 hoặc Bước 12)**
    1. Hệ thống phát hiện số lượng tồn kho thực tế nhỏ hơn số lượng khách yêu cầu.
    2. Hệ thống hiển thị thông báo lỗi chỉ rõ sản phẩm nào không đủ (VD: "Sản phẩm [Tên sản phẩm] hiện chỉ còn [X] cái").
    3. Hệ thống yêu cầu khách hàng giảm số lượng hoặc xóa sản phẩm bị thiếu khỏi đơn hàng.
    4. Khách hàng thực hiện điều chỉnh theo yêu cầu.
    5. Quay lại Bước 2 (Kiểm tra lại toàn bộ giỏ hàng) hoặc Bước 3.

*   **A5: Giao dịch thanh toán trực tuyến thất bại/bị hủy (tại Bước 14)**
    1. Khách hàng chủ động hủy giao dịch trên giao diện cổng thanh toán, hoặc giao dịch bị từ chối do thẻ lỗi/hết hạn/không đủ số dư.
    2. Cổng thanh toán trả kết quả "Thất bại" hoặc "Hủy bỏ" về cho hệ thống.
    3. Hệ thống hiển thị trang "Thanh toán không thành công" và cung cấp lý do (nếu có).
    4. Đơn hàng được giữ lại trên hệ thống ở trạng thái "Chờ thanh toán" (hoặc "Thanh toán thất bại") trong một khoảng thời gian nhất định (VD: 15-30 phút), không trừ tồn kho cứng.
    5. Khách hàng có thể chọn nút "Thử thanh toán lại" hoặc "Đổi phương thức sang COD".
    6. Nếu khách hàng chọn thử lại, quay lại Bước 14.

**Postconditions**:
- Một đơn hàng mới được tạo ra trong hệ thống và gán cho tài khoản của khách hàng.
- Tồn kho của các sản phẩm tương ứng được trừ đi hoặc tạm giữ an toàn.
- Hệ thống ghi nhận lịch sử giao dịch.
- Email/SMS xác nhận được gửi đến khách hàng thành công.
- Giỏ hàng được cập nhật, xóa đi những sản phẩm đã chốt đơn.

**Business Rules**:
- **Tính toán tổng tiền**: Tổng thanh toán = Tổng giá trị sản phẩm + Phí vận chuyển - Số tiền giảm giá.
- **Tạm giữ tồn kho (Inventory Hold)**: Khi khách hàng nhấn "Xác nhận đặt hàng" và chuyển sang cổng thanh toán trực tuyến, tồn kho của sản phẩm có thể được "tạm giữ" trong 15 phút. Nếu quá thời gian chưa thanh toán, hệ thống sẽ nhả tồn kho và chuyển đơn hàng sang trạng thái "Đã hủy".
- **Hóa đơn điện tử**: Nếu khách hàng yêu cầu xuất hóa đơn, hệ thống sẽ đánh dấu đơn hàng cần xử lý VAT bởi Admin/Kế toán sau khi đơn hoàn thành.
