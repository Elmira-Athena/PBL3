# Tài liệu Đặc tả Use Case

## 1. UC001: Khách hàng tự đăng ký tài khoản

### 1.1 Thông tin chung
| Thành phần | Nội dung |
| :--- | :--- |
| **Mã Use case** | UC001 |
| **Tên Use case** | Khách hàng tự đăng ký tài khoản |
| **Tác nhân** | Khách hàng (Chưa đăng nhập) |
| **Mô tả** | Khách hàng vãng lai tạo mới một tài khoản trên hệ thống để mua hàng và lưu trữ lịch sử giao dịch. |
| **Sự kiện kích hoạt** | Khách hàng chọn chức năng "Đăng ký" trên trang chủ hệ thống. |
| **Điều kiện tiên quyết** | Khách hàng chưa đăng nhập. |

### 1.2 Luồng sự kiện chính
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| 1 | Người dùng | Chọn chức năng "Đăng ký tài khoản". |
| 2 | Hệ thống | Hiển thị biểu mẫu đăng ký yêu cầu các thông tin cần thiết (Họ tên, SĐT, Email, Mật khẩu, Xác nhận mật khẩu). |
| 3 | Người dùng | Cung cấp thông tin đầy đủ và yêu cầu khởi tạo tài khoản. |
| 4 | Hệ thống | Kiểm tra tính hợp lệ của thông tin và đảm bảo SĐT/Email chưa được sử dụng. |
| 5 | Hệ thống | Khởi tạo tài khoản mới trong cơ sở dữ liệu với trạng thái Hoạt động. |
| 6 | Hệ thống | Thông báo đăng ký thành công và yêu cầu người dùng đăng nhập (hoặc tự động đăng nhập). |

### 1.3 Luồng ngoại lệ
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| **4a** | **Sai định dạng hoặc Trùng lặp dữ liệu** | |
| 4a.1 | Hệ thống | Thông báo chi tiết các dữ liệu không hợp lệ hoặc SĐT/Email đã tồn tại. |
| 4a.2 | Hệ thống | Đánh dấu các trường thông tin bị lỗi, giữ nguyên các thông tin hợp lệ khác. |
| 4a.3 | Hệ thống | Yêu cầu người dùng kiểm tra và cập nhật lại thông tin. Quay lại bước 3. |

---

## 2. UC010: Quản lý tài khoản khách hàng (Admin/Nhân viên)

### 2.1 Thông tin chung
| Thành phần | Nội dung |
| :--- | :--- |
| **Mã Use case** | UC010 |
| **Tên Use case** | Quản lý tài khoản khách hàng |
| **Tác nhân** | Nhân viên, Quản trị viên |
| **Mô tả** | Người dùng thực hiện các chức năng quản lý tài khoản trên hệ thống nội bộ bao gồm: Xem chi tiết, Thêm mới, Cập nhật, Khóa tài khoản. |
| **Sự kiện kích hoạt** | Người dùng truy cập danh sách khách hàng và chọn các hành động tương ứng. |
| **Điều kiện tiên quyết** | Đã đăng nhập vào hệ thống với phân quyền Nhân viên hoặc Quản trị viên. |

### 2.2 Tạo tài khoản khách hàng (Thêm mới bởi Admin)

**Sự kiện kích hoạt riêng:** Nhấn chọn chức năng thêm tài khoản mới.

#### Luồng sự kiện chính
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| 1 | Người dùng | Chọn chức năng "Thêm tài khoản mới". |
| 2 | Hệ thống | Hiển thị biểu mẫu yêu cầu các thông tin cần thiết (Họ tên, SĐT, Email, Mật khẩu, Xác nhận mật khẩu, Địa chỉ). |
| 3 | Người dùng | Cung cấp thông tin đầy đủ và yêu cầu tạo mới. |
| 4 | Hệ thống | Kiểm tra tính hợp lệ của thông tin và kiểm tra trùng lặp dữ liệu (SĐT/Email). |
| 5 | Hệ thống | Khởi tạo tài khoản mới trong cơ sở dữ liệu. |
| 6 | Hệ thống | Thông báo khởi tạo thành công và điều hướng về danh sách khách hàng. |

#### Luồng rẽ nhánh
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| **3a** | **Hủy tạo mới** | |
| 3a.1 | Người dùng | Chọn chức năng "Hủy bỏ". |
| 3a.2 | Hệ thống | Hủy bỏ dữ liệu chưa lưu và điều hướng trở lại giao diện danh sách. |

#### Luồng ngoại lệ
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| **4a** | **Sai định dạng hoặc Trùng lặp** | |
| 4a.1 | Hệ thống | Thông báo chi tiết các dữ liệu không hợp lệ hoặc đã tồn tại (SDT/Email). |
| 4a.2 | Hệ thống | Đánh dấu các trường dữ liệu bị lỗi, giữ nguyên các thông tin hợp lệ. |
| 4a.3 | Hệ thống | Yêu cầu người dùng kiểm tra và cập nhật lại thông tin. Quay lại bước 3. |

### 2.3 Xem chi tiết tài khoản khách hàng

**Sự kiện kích hoạt riêng:** Chọn một tài khoản từ danh sách.

#### Luồng sự kiện chính
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| 1 | Người dùng | Yêu cầu xem chi tiết một tài khoản cụ thể. |
| 2 | Hệ thống | Trình bày thông tin chi tiết khách hàng bao gồm:<br>- **Thông tin chung**: Ảnh đại diện, Họ tên, Email, SĐT, Địa chỉ, Trạng thái hoạt động.<br>- **Lịch sử đơn hàng**: Danh sách các đơn hàng gần nhất.<br>- **Giỏ hàng**: Danh sách sản phẩm hiện tại trong giỏ của khách. |

#### Luồng rẽ nhánh
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| **1a** | **Trở về** | |
| 1a.1 | Người dùng | Yêu cầu quay lại. |
| 1a.2 | Hệ thống | Điều hướng về danh sách khách hàng. |
| **2a** | **Xem đơn hàng** | |
| 2a.1 | Người dùng | Chọn xem chi tiết một đơn hàng trong phần Lịch sử. |
| 2a.2 | Hệ thống | Điều hướng sang chức năng Xem chi tiết đơn hàng (UC011). |
| **2b** | **Xem giỏ hàng** | |
| 2b.1 | Người dùng | Chọn xem thông tin một sản phẩm trong phần Giỏ hàng. |
| 2b.2 | Hệ thống | Điều hướng sang chức năng Xem chi tiết sản phẩm (UC005). |
| **2c** | **Yêu cầu cập nhật** | |
| 2c.1 | Người dùng | Chọn chức năng cập nhật thông tin. |
| 2c.2 | Hệ thống | Chuyển sang luồng Cập nhật thông tin (Mục 2.4). |
| **2d** | **Yêu cầu khóa tài khoản** | |
| 2d.1 | Người dùng | Chọn chức năng khóa/ngưng hoạt động tài khoản. |
| 2d.2 | Hệ thống | Chuyển sang luồng Khóa tài khoản (Mục 2.5). |

### 2.4 Cập nhật thông tin tài khoản

**Sự kiện kích hoạt riêng:** Chọn chức năng chỉnh sửa từ trang chi tiết hoặc danh sách.

#### Luồng sự kiện chính
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| 1 | Người dùng | Yêu cầu cập nhật thông tin tài khoản. |
| 2 | Hệ thống | Hiển thị biểu mẫu cho phép chỉnh sửa thông tin. Hệ thống có thể giới hạn quyền sửa đổi một số trường nhạy cảm nhất định. |
| 3 | Người dùng | Cung cấp thông tin đã điều chỉnh. |
| 4 | Người dùng | Yêu cầu lưu thay đổi. |
| 5 | Hệ thống | Kiểm tra tính hợp lệ của dữ liệu mới (định dạng, trùng lặp). |
| 6 | Hệ thống | Cập nhật thông tin vào cơ sở dữ liệu và thông báo cập nhật thành công. |
| 7 | Hệ thống | Điều hướng trở lại trang xem chi tiết tài khoản. |

#### Luồng rẽ nhánh
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| **3a** | **Hủy bỏ cập nhật** | |
| 3a.1 | Người dùng | Yêu cầu hủy bỏ. |
| 3a.2 | Hệ thống | Bỏ qua các thay đổi chưa lưu, quay lại giao diện trước đó. |
| **3b** | **Khôi phục dữ liệu gốc** | |
| 3b.1 | Người dùng | Yêu cầu làm mới/khôi phục dữ liệu nguyên bản. |
| 3b.2 | Hệ thống | Tải lại thông tin ban đầu vào biểu mẫu. Quay lại bước 3. |

#### Luồng ngoại lệ
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| **5a** | **Dữ liệu không hợp lệ** | |
| 5a.1 | Hệ thống | Thông báo chi tiết lỗi (ví dụ: sai định dạng, thiếu thông tin bắt buộc, trùng lặp email). |
| 5a.2 | Hệ thống | Yêu cầu người dùng kiểm tra và chỉnh sửa. Quay lại bước 3. |

### 2.5 Khóa tài khoản khách hàng (Deactivate)

**Sự kiện kích hoạt riêng:** Chọn chức năng khóa/ngưng hoạt động tài khoản.

#### Luồng sự kiện chính
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| 1 | Người dùng | Yêu cầu khóa tài khoản khách hàng. |
| 2 | Hệ thống | Yêu cầu người dùng xác nhận hành động (chỉ rõ hậu quả: khách hàng sẽ không truy cập được tài khoản nhưng dữ liệu lịch sử vẫn được lưu trữ). |
| 3 | Người dùng | Cung cấp xác nhận. |
| 4 | Hệ thống | Kiểm tra các quy tắc nghiệp vụ ràng buộc (có đang tham gia giao dịch dở dang nào không). |
| 5 | Hệ thống | Cập nhật trạng thái tài khoản thành Không hoạt động (Inactive) trong cơ sở dữ liệu. |
| 6 | Hệ thống | Thông báo khóa thành công và điều hướng về danh sách khách hàng. |

#### Luồng rẽ nhánh
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| **3a** | **Hủy khóa tài khoản** | |
| 3a.1 | Người dùng | Từ chối xác nhận hành động khóa. |
| 3a.2 | Hệ thống | Hủy phiên xử lý, trở lại giao diện xem chi tiết khách hàng. |

#### Luồng ngoại lệ
| STT | Thực hiện bởi | Hành động |
| :--- | :--- | :--- |
| **4a** | **Vi phạm ràng buộc nghiệp vụ** | |
| 4a.1 | Hệ thống | Thông báo không thể khóa (ví dụ: "Tài khoản đang có đơn hàng chờ xử lý"). |
| 4a.2 | Hệ thống | Từ chối yêu cầu và trở lại giao diện xem chi tiết. |

---

## 3. Quy tắc nghiệp vụ (Business Rules)
- **BR1:** Một Email hoặc Số điện thoại chỉ được phép liên kết độc nhất với một tài khoản khách hàng tại một thời điểm.
- **BR2:** Mật khẩu khi khởi tạo phải tuân thủ chính sách bảo mật của hệ thống.
- **BR3:** Hệ thống giới hạn quyền cập nhật một số thông tin (Email, Số điện thoại) nếu tài khoản đã xác thực, có thể yêu cầu quy trình duyệt từ quản trị viên.
- **BR4:** Tài khoản bị khóa (Inactive) không thể đăng nhập hoặc phát sinh giao dịch mới, nhưng lịch sử đơn hàng và dữ liệu thanh toán cũ được giữ lại phục vụ báo cáo.

## 4. Hậu điều kiện (Postconditions)
- Thông tin của khách hàng (mới hoặc cập nhật) được ghi nhận an toàn vào cơ sở dữ liệu.
- Hệ thống hỗ trợ tra cứu, đánh giá lịch sử mua hàng liền mạch với các phân hệ khác (bán hàng, kho, doanh thu).