# Đề trắc nghiệm HTTPS / SSL / TLS — Dự án HushStore (PBL3)

50 câu, mỗi câu 1 điểm. Phân bố: **25** nhận biết/thông hiểu · **15** vận dụng · **10** vận dụng cao.

Quy tắc ra đề: phương án là mệnh đề ngắn, không chứa lời giải thích; bốn phương án mỗi câu có độ dài tương đương (chênh lệch trung bình 5.8 ký tự, lớn nhất 10); vị trí đáp án đúng chia đều A 13 · B 13 · C 12 · D 12.

Đáp án đúng được **in đậm**. Bản Apps Script để dựng Google Form: [`tao-form-https-ssl-tls-quiz.gs`](tao-form-https-ssl-tls-quiz.gs).

## Phần A — Nhận biết & Thông hiểu (câu 1–25)

**Câu 1.** (Nhận biết) HTTPS là gì?

- **A. Một giao thức HTTP được mã hóa bằng SSL/TLS trước khi truyền**
- B. Một phiên bản HTTP nén dữ liệu để truyền nhanh hơn qua mạng
- C. Một giao thức thay thế HTTP, dùng riêng cho API nội bộ
- D. Một tiêu chuẩn nén ảnh giúp trang web tải nhanh hơn

**Câu 2.** (Nhận biết) Mục tiêu chính mà TLS mang lại cho kết nối web là gì?

- A. Giảm dung lượng dữ liệu truyền đi bằng cách nén nội dung
- **B. Mã hóa, xác thực máy chủ và bảo toàn dữ liệu khi truyền**
- C. Tăng tốc độ tải trang bằng cách bỏ qua bước xác thực
- D. Cân bằng tải giữa nhiều máy chủ trong cùng một cụm

**Câu 3.** (Thông hiểu) Trong TLS, mã hóa đối xứng và bất đối xứng được phối hợp ra sao?

- A. Đối xứng trao đổi khóa, bất đối xứng mã hóa dữ liệu phiên
- B. Cả hai cùng mã hóa song song mọi gói tin trong phiên
- **C. Bất đối xứng trao đổi khóa, đối xứng mã hóa dữ liệu phiên**
- D. Chỉ dùng bất đối xứng, đối xứng chỉ dự phòng khi lỗi

**Câu 4.** (Nhận biết) Chứng chỉ số (certificate) trong TLS dùng để làm gì?

- A. Nén dữ liệu trước khi gửi đi nhằm tiết kiệm băng thông
- B. Lưu mật khẩu người dùng để đăng nhập tự động lần sau
- C. Ghi lại nhật ký truy cập của trình duyệt vào máy chủ
- **D. Ràng buộc khóa công khai với danh tính của máy chủ**

**Câu 5.** (Nhận biết) Certificate Authority (CA) đóng vai trò gì trong hệ thống TLS?

- **A. Bên thứ ba đáng tin cấp và xác thực chứng chỉ số**
- B. Cung cấp băng thông mạng cho máy chủ chạy HTTPS
- C. Lưu trữ toàn bộ dữ liệu người dùng của website
- D. Giám sát hiệu năng máy chủ và cảnh báo khi quá tải

**Câu 6.** (Thông hiểu) Chuỗi tin cậy (chain of trust) trong TLS được xây dựng như thế nào?

- A. Mỗi chứng chỉ tự ký cho chính nó, không phụ thuộc bên nào
- **B. Root CA tin cậy sẵn ký cho Intermediate, Intermediate ký lá**
- C. Trình duyệt tự tạo chứng chỉ tạm cho từng phiên truy cập
- D. Máy chủ ký chứng chỉ cho chính CA để xác nhận qua lại

**Câu 7.** (Nhận biết) TLS handshake diễn ra nhằm mục đích gì?

- A. Xóa cache cũ của trình duyệt trước khi tải trang mới
- B. Đồng bộ đồng hồ hệ thống giữa client và máy chủ
- **C. Thỏa thuận khóa phiên và xác thực danh tính máy chủ**
- D. Nén nội dung trang trước khi gửi về cho trình duyệt

**Câu 8.** (Thông hiểu) Trong bước trao đổi khóa của handshake, khóa công khai và khóa riêng được dùng thế nào?

- A. Khóa riêng mã hóa dữ liệu, khóa công khai lo giải mã
- B. Cả hai khóa đều được giữ bí mật tuyệt đối như nhau
- C. Khóa công khai chỉ dùng ở phía client, không ở máy chủ
- **D. Khóa công khai mã hóa hoặc xác minh, khóa riêng giải mã**

**Câu 9.** (Nhận biết) HTTP và HTTPS khác nhau ở cổng mặc định như thế nào?

- **A. HTTP dùng cổng 80, HTTPS dùng cổng 443 mặc định**
- B. HTTP dùng cổng 8080, HTTPS dùng cổng 8443 mặc định
- C. HTTP dùng cổng 21, HTTPS dùng cổng 22 mặc định
- D. HTTP dùng cổng 443, HTTPS dùng cổng 80 mặc định

**Câu 10.** (Nhận biết) Biểu tượng ổ khóa trên thanh địa chỉ trình duyệt cho biết điều gì?

- A. Trang web đã được xác minh không chứa nội dung lừa đảo
- **B. Kết nối hiện tại giữa trình duyệt và máy chủ được mã hóa**
- C. Máy chủ đã được kiểm duyệt nội dung bởi nhà cung cấp CA
- D. Trình duyệt đã chặn toàn bộ quảng cáo trên trang này

**Câu 11.** (Thông hiểu) Cơ chế SNI trong TLS handshake giải quyết vấn đề gì?

- A. Cho phép một chứng chỉ dùng chung cho mọi tên miền bất kỳ
- B. Cho phép máy chủ nén nội dung trước khi gửi cho trình duyệt
- **C. Cho phép nhiều tên miền dùng chung IP vẫn chọn đúng chứng chỉ**
- D. Cho phép trình duyệt bỏ qua bước xác thực chứng chỉ máy chủ

**Câu 12.** (Nhận biết) Session resumption trong TLS mang lại lợi ích gì?

- A. Cho phép trình duyệt lưu mật khẩu đăng nhập vĩnh viễn
- B. Cho phép máy chủ đổi chứng chỉ mà không cần khởi động lại
- C. Cho phép nhiều người dùng chia sẻ chung một khóa riêng
- **D. Cho phép rút gọn bước bắt tay ở lần kết nối lại sau đó**

**Câu 13.** (Thông hiểu) Forward secrecy (bảo mật hướng tới) trong TLS nghĩa là gì?

- **A. Lộ khóa dài hạn không giúp giải mã được các phiên đã qua**
- B. Mỗi phiên dùng lại đúng một khóa cố định suốt vòng đời chứng chỉ
- C. Dữ liệu người dùng được lưu vĩnh viễn để phục vụ điều tra
- D. Máy chủ tự động xóa nhật ký truy cập sau một khoảng thời gian

**Câu 14.** (Nhận biết) Cipher suite trong TLS là gì?

- A. Danh sách các tên miền được phép truy cập vào máy chủ này
- **B. Tổ hợp thuật toán trao đổi khóa, mã hóa và băm được thống nhất**
- C. Bộ quy tắc nén ảnh riêng được áp dụng cho các kết nối HTTPS
- D. Tập hợp các cổng mạng mà máy chủ mở ra để lắng nghe kết nối

**Câu 15.** (Nhận biết) HSTS (HTTP Strict Transport Security) yêu cầu điều gì từ trình duyệt?

- A. Luôn xóa cookie sau mỗi lần đóng tab của trình duyệt
- B. Luôn hiển thị quảng cáo bảo mật trước khi tải trang
- **C. Luôn dùng HTTPS cho tên miền đó, kể cả khi người dùng gõ HTTP**
- D. Luôn chuyển hướng người dùng sang một tên miền dự phòng

**Câu 16.** (Thông hiểu) Mixed content trên một trang HTTPS là tình huống nào?

- A. Trang chứa cả nội dung tiếng Việt lẫn tiếng Anh trên cùng URL
- B. Trang dùng hai chứng chỉ khác nhau cho hai tên miền phụ
- C. Trang gửi dữ liệu form tới hai máy chủ backend cùng lúc
- **D. Trang tải xen kẽ tài nguyên qua HTTP dù trang chính đã là HTTPS**

**Câu 17.** (Thông hiểu) Chứng chỉ tự ký (self-signed) khác chứng chỉ do CA công cộng cấp ở điểm nào?

- **A. Tự ký không có bên thứ ba xác nhận nên trình duyệt cảnh báo**
- B. Tự ký không hỗ trợ được thuật toán mã hóa đối xứng
- C. Tự ký chỉ dùng được với giao thức HTTP, không dùng cho HTTPS
- D. Tự ký bắt buộc phải trả phí cao hơn chứng chỉ CA công cộng

**Câu 18.** (Nhận biết) Chứng chỉ wildcard (*.example.com) dùng để làm gì?

- A. Cho phép một chứng chỉ bảo vệ mọi tên miền cấp một bất kỳ
- **B. Cho phép một chứng chỉ bảo vệ mọi subdomain cấp một của tên miền**
- C. Cho phép máy chủ dùng chung một khóa riêng cho mọi khách hàng
- D. Cho phép chứng chỉ có hiệu lực vĩnh viễn không cần gia hạn

**Câu 19.** (Nhận biết) Trường SAN (Subject Alternative Name) trong chứng chỉ dùng để làm gì?

- A. Lưu mật khẩu quản trị dùng để gia hạn chứng chỉ sau này
- B. Ghi lại lịch sử các phiên bản trước đó của chứng chỉ
- **C. Liệt kê thêm các tên miền hoặc IP mà chứng chỉ đó bảo vệ**
- D. Xác định múi giờ mà máy chủ cấp chứng chỉ đang hoạt động

**Câu 20.** (Thông hiểu) mTLS (mutual TLS) khác TLS thông thường ở điểm nào?

- A. mTLS bỏ qua bước xác thực máy chủ để tăng tốc độ kết nối
- B. mTLS chỉ áp dụng được cho kết nối nội bộ trong một máy
- C. mTLS thay thế hoàn toàn nhu cầu dùng mật khẩu đăng nhập
- **D. mTLS bắt cả client lẫn máy chủ đều phải trình chứng chỉ**

**Câu 21.** (Nhận biết) Giao thức OCSP dùng để làm gì?

- **A. Kiểm tra trực tuyến xem một chứng chỉ có bị thu hồi hay không**
- B. Nén dữ liệu truyền giữa client và máy chủ trong phiên TLS
- C. Đồng bộ thời gian hệ thống giữa các máy chủ trong cụm
- D. Sinh khóa riêng mới cho máy chủ mỗi khi khởi động lại

**Câu 22.** (Nhận biết) CRL (Certificate Revocation List) là gì?

- A. Danh sách tên miền được phép cấp chứng chỉ miễn phí
- **B. Danh sách các chứng chỉ đã bị thu hồi trước khi hết hạn**
- C. Danh sách khóa riêng bị lộ được công khai để cảnh báo
- D. Danh sách cipher suite bị cấm sử dụng trên toàn hệ thống

**Câu 23.** (Thông hiểu) So với TLS 1.2, TLS 1.3 có thay đổi đáng chú ý nào?

- A. Bỏ hẳn khái niệm chứng chỉ số, chuyển sang xác thực khác
- B. Chỉ hoạt động được trên nền giao thức UDP thay vì TCP
- **C. Handshake rút gọn hơn và loại bỏ các thuật toán yếu cũ**
- D. Yêu cầu bắt buộc mọi kết nối phải dùng mTLS hai chiều

**Câu 24.** (Nhận biết) ALPN trong TLS handshake dùng để làm gì?

- A. Xác thực địa chỉ IP của client trước khi cho kết nối
- B. Nén phần header của gói tin TLS để giảm băng thông
- C. Chọn múi giờ hiển thị cho chứng chỉ khi hết hạn
- **D. Thỏa thuận giao thức ứng dụng sẽ dùng, ví dụ HTTP/2**

**Câu 25.** (Thông hiểu) Let's Encrypt đóng vai trò gì trong hệ sinh thái TLS hiện nay?

- **A. Một CA miễn phí, tự động hóa cấp chứng chỉ qua giao thức ACME**
- B. Một trình duyệt mã nguồn mở hỗ trợ riêng cho TLS 1.3
- C. Một chuẩn thay thế hoàn toàn cho giao thức HTTPS hiện tại
- D. Một công cụ quét lỗ hổng dành riêng cho cấu hình TLS

## Phần B — Vận dụng (câu 26–40)

**Câu 26.** (Vận dụng) Vì sao access token JWT của hệ thống bắt buộc truyền qua HTTPS thay vì HTTP?

- A. Vì HTTP không hỗ trợ được định dạng chuỗi Base64 của JWT
- **B. Vì HTTP để lộ token trên đường truyền cho kẻ nghe lén**
- C. Vì HTTPS nén token nên payload gửi đi nhẹ hơn đáng kể
- D. Vì máy chủ chỉ chấp nhận Header Authorization qua HTTPS

**Câu 27.** (Vận dụng) Lệnh dotnet dev-certs https sinh ra loại chứng chỉ nào và phục vụ mục đích gì?

- A. Chứng chỉ CA công cộng, dùng thẳng được cho môi trường production
- B. Chứng chỉ wildcard dùng cho mọi tên miền mà công ty đang sở hữu
- **C. Chứng chỉ tự ký cho môi trường local, tránh phải cần CA thật**
- D. Chứng chỉ mTLS bắt buộc client phải xác thực khi gọi vào local

**Câu 28.** (Vận dụng) Khi load balancer chấm dứt TLS (TLS termination) trước khi chuyển tiếp vào container nội bộ, điều gì thường xảy ra?

- A. Container nhận thẳng traffic mã hóa và tự giải mã lại lần nữa
- B. Load balancer từ chối chuyển tiếp mọi traffic đã được mã hóa
- C. Container bắt buộc phải giữ private key giống hệt load balancer
- **D. Traffic nội bộ sau điểm chấm dứt thường đi dưới dạng HTTP thường**

**Câu 29.** (Vận dụng) Vì sao ổ khóa HTTPS trên trình duyệt không đồng nghĩa trang đó an toàn hay đáng tin?

- **A. Vì ổ khóa chỉ xác nhận kênh truyền mã hóa, không xét nội dung**
- B. Vì trình duyệt hiện đại đã bỏ hẳn việc kiểm tra chứng chỉ hợp lệ
- C. Vì ổ khóa chỉ xuất hiện khi trang không yêu cầu đăng nhập gì
- D. Vì chứng chỉ HTTPS chỉ có giá trị trong một vài phút mỗi lần

**Câu 30.** (Vận dụng) Khi chứng chỉ TLS của một trang đã hết hạn, trình duyệt phản ứng ra sao và vì sao?

- A. Tự động gia hạn ngầm rồi tải trang bình thường cho người dùng
- **B. Cảnh báo lỗi kết nối vì không còn xác thực được danh tính máy chủ**
- C. Chuyển hướng người dùng sang bản HTTP không mã hóa để tiếp tục
- D. Bỏ qua cảnh báo nếu người dùng đã từng truy cập trang trước đó

**Câu 31.** (Vận dụng) Vì sao tấn công hạ cấp giao thức (downgrade attack) ép dùng SSL/TLS phiên bản cũ lại nguy hiểm?

- A. Vì phiên bản cũ tải trang chậm hơn gây trải nghiệm kém
- B. Vì phiên bản cũ không tương thích với trình duyệt hiện đại
- **C. Vì phiên bản cũ thường mang theo lỗ hổng mã hóa đã biết**
- D. Vì phiên bản cũ yêu cầu chứng chỉ đắt hơn để vận hành

**Câu 32.** (Vận dụng) Vì sao quản trị nên chủ động tắt các cipher suite yếu như RC4 trên máy chủ?

- A. Vì cipher suite yếu làm tăng đáng kể độ trễ của handshake
- B. Vì cipher suite yếu chiếm nhiều dung lượng lưu trữ chứng chỉ
- C. Vì cipher suite yếu không tương thích với giao thức HTTP/2
- **D. Vì cipher suite yếu có điểm yếu mã hóa cho phép bị phá được**

**Câu 33.** (Vận dụng) Sau khi gia hạn chứng chỉ nhưng quên reload tiến trình máy chủ web, hệ quả thường là gì?

- **A. Máy chủ vẫn phục vụ bằng chứng chỉ cũ cho tới khi được nạp lại**
- B. Máy chủ tự phát hiện và nạp lại chứng chỉ mới ngay lập tức
- C. Máy chủ ngừng phục vụ toàn bộ kết nối HTTPS ngay tức thì
- D. Trình duyệt tự lấy chứng chỉ mới trực tiếp từ CA thay máy chủ

**Câu 34.** (Vận dụng) HSTS preload giúp chống downgrade attack tốt hơn HSTS thường ở điểm nào?

- A. Preload giúp chứng chỉ có thời hạn hiệu lực dài hơn hẳn
- **B. Trình duyệt buộc dùng HTTPS ngay từ lần đầu, không qua HTTP trước**
- C. Preload cho phép bỏ qua hoàn toàn bước xác thực chứng chỉ
- D. Trình duyệt chỉ preload được với chứng chỉ do một CA cấp

**Câu 35.** (Vận dụng) Theo nguyên tắc đồng bộ của hệ thống, vì sao thao tác thanh toán bắt buộc phải qua kênh TLS chứ không thể trì hoãn xác thực?

- A. Vì cổng thanh toán chỉ hỗ trợ giao thức TLS phiên bản mới nhất
- B. Vì TLS giúp nén dữ liệu thanh toán để gửi đi nhanh hơn hẳn
- **C. Vì thanh toán cần nhất quán dữ liệu ngay, không chấp nhận trễ**
- D. Vì thao tác thanh toán luôn được xử lý riêng ở tác vụ nền

**Câu 36.** (Vận dụng) Certificate pinning có thể gây rủi ro vận hành nào khi tổ chức xoay vòng chứng chỉ định kỳ?

- A. Chứng chỉ mới sẽ tự động được ghim thay thế mà không cần cập nhật
- B. Pinning chỉ ảnh hưởng tới tốc độ tải trang, không ảnh hưởng kết nối
- C. Pinning khiến chứng chỉ mới không cần CA nào ký xác nhận nữa
- **D. Ứng dụng đã ghim chứng chỉ cũ có thể từ chối kết nối với bản mới**

**Câu 37.** (Vận dụng) Khi nhiều tên miền cùng chia sẻ một địa chỉ IP trên load balancer, cơ chế nào giúp chọn đúng chứng chỉ cho từng tên miền?

- **A. SNI, dựa trên tên miền client gửi kèm ngay khi bắt tay**
- B. ALPN, dựa trên giao thức ứng dụng mà client đề nghị dùng
- C. OCSP, dựa trên trạng thái thu hồi của từng chứng chỉ
- D. HSTS, dựa trên chính sách buộc dùng HTTPS đã lưu trước

**Câu 38.** (Vận dụng) Vì sao chứng chỉ wildcard *.example.com không bảo vệ được tên miền sub.sub.example.com?

- A. Vì wildcard chỉ có hiệu lực với tên miền cấp cao nhất duy nhất
- **B. Vì wildcard chỉ khớp đúng một cấp subdomain, không khớp nhiều cấp**
- C. Vì wildcard bắt buộc phải đi kèm chứng chỉ mTLS mới hoạt động
- D. Vì wildcard chỉ được các trình duyệt cũ hỗ trợ đầy đủ tính năng

**Câu 39.** (Vận dụng) Vì sao HTTP/2 trong thực tế hầu như luôn chạy kèm TLS dù chuẩn không bắt buộc điều đó?

- A. Vì giao thức HTTP/2 về mặt kỹ thuật không thể hoạt động thiếu TLS
- B. Vì TLS là điều kiện bắt buộc để nén được phần thân dữ liệu
- **C. Vì phần lớn trình duyệt chỉ hỗ trợ HTTP/2 khi có TLS đi kèm**
- D. Vì máy chủ web hiện đại đã ngừng hỗ trợ hoàn toàn HTTP/1.1

**Câu 40.** (Vận dụng) Vì sao việc lộ private key của máy chủ được xem là sự cố nghiêm trọng bậc nhất với TLS?

- A. Vì private key bị lộ khiến băng thông máy chủ giảm rõ rệt
- B. Vì private key là thứ duy nhất trình duyệt dùng để hiển thị ổ khóa
- C. Vì private key bị lộ chỉ ảnh hưởng tới một phiên kết nối duy nhất
- **D. Vì kẻ tấn công có thể giả mạo máy chủ và can thiệp kết nối**

## Phần C — Vận dụng cao (câu 41–50)

**Câu 41.** (Vận dụng cao) Một lỗi tràn bộ đệm kiểu Heartbleed trong thư viện TLS rò rỉ vùng nhớ tiến trình ra ngoài. Hệ quả nguy hiểm nhất là gì?

- **A. Kẻ tấn công có thể đọc được private key hoặc dữ liệu phiên đang xử lý**
- B. Máy chủ chỉ giảm hiệu năng tạm thời rồi tự phục hồi bình thường
- C. Lỗi chỉ ảnh hưởng tới phiên bản HTTP, không liên quan tới HTTPS
- D. Trình duyệt sẽ tự phát hiện và từ chối kết nối máy chủ bị lỗi

**Câu 42.** (Vận dụng cao) Với các bộ trao đổi khóa hỗ trợ forward secrecy như ECDHE, việc private key dài hạn của máy chủ bị lộ sau này có hệ quả gì với traffic đã ghi lại từ trước?

- A. Traffic cũ vẫn giải mã được ngay vì khóa dài hạn kiểm soát mọi phiên
- **B. Traffic cũ vẫn an toàn vì khóa phiên đã được tạo tạm thời và huỷ đi**
- C. Chỉ traffic của phiên gần nhất trước khi lộ khóa mới giải mã được
- D. Toàn bộ traffic cũ tự động bị xóa khỏi máy chủ ngay khi khóa lộ

**Câu 43.** (Vận dụng cao) Kẻ tấn công cài được một root CA giả vào kho tin cậy của máy nạn nhân rồi thực hiện MITM. Vì sao HTTPS thông thường không phát hiện ra, và cơ chế nào có thể giúp chặn được?

- A. Vì trình duyệt không kiểm tra chứng chỉ khi kết nối cùng mạng nội bộ
- B. Vì HTTPS chỉ xác thực một chiều nên máy chủ không biết bị giả mạo
- **C. Vì chuỗi chứng chỉ giả hợp lệ với kho tin cậy bị chèn, pinning giúp lộ**
- D. Vì CA giả luôn bị OCSP đánh dấu thu hồi ngay khi vừa được cài vào

**Câu 44.** (Vận dụng cao) So với tra cứu qua CRL, OCSP stapling cải thiện điều gì trong việc kiểm tra thu hồi chứng chỉ?

- A. OCSP stapling loại bỏ hoàn toàn nhu cầu có Certificate Authority
- B. CRL luôn cập nhật nhanh hơn OCSP nên ít khi cần dùng stapling
- C. Stapling chuyển việc kiểm tra thu hồi sang phía client tự xử lý
- **D. Máy chủ tự đính kèm phản hồi OCSP mới, đỡ trình duyệt tự truy vấn**

**Câu 45.** (Vận dụng cao) Khi OCSP responder không phản hồi được, sự khác biệt giữa chính sách hard-fail và soft-fail là gì?

- **A. Hard-fail chặn kết nối khi không xác minh được; soft-fail vẫn cho qua**
- B. Hard-fail chỉ áp dụng cho mTLS; soft-fail áp dụng cho TLS một chiều
- C. Hard-fail cho qua kết nối; soft-fail chặn hẳn mọi kết nối đến
- D. Cả hai chính sách đều chặn kết nối như nhau, chỉ khác thời gian chờ

**Câu 46.** (Vận dụng cao) TLS 1.3 loại bỏ cơ chế trao đổi khóa RSA tĩnh và renegotiation giữa phiên. Điều này liên hệ thế nào tới forward secrecy?

- A. Việc loại bỏ chỉ nhằm tăng tốc độ, không liên quan gì tới forward secrecy
- **B. Loại bỏ RSA tĩnh buộc mọi phiên phải dùng khóa tạm, đảm bảo forward secrecy**
- C. Renegotiation vốn là cơ chế duy nhất từng cung cấp forward secrecy
- D. RSA tĩnh vẫn đảm bảo forward secrecy tốt hơn các khóa trao đổi tạm

**Câu 47.** (Vận dụng cao) Hai microservice giao tiếp nội bộ trong cùng một VPC riêng có nên vẫn dùng TLS (thậm chí mTLS) hay không, và đánh đổi là gì?

- A. Không cần vì mạng riêng đã đủ an toàn, thêm TLS chỉ gây lãng phí
- B. Chỉ nên dùng khi hai service khác region, cùng region thì bỏ qua
- **C. Nên dùng để phòng thủ theo chiều sâu, đổi lại thêm chi phí vận hành khóa**
- D. Nên dùng nhưng chỉ cần mã hóa một chiều, không cần xác thực lẫn nhau

**Câu 48.** (Vận dụng cao) Chế độ 0-RTT của TLS 1.3 giảm độ trễ bằng cách gửi dữ liệu ngay ở lần bắt tay lại. Rủi ro tiềm ẩn của cơ chế này là gì?

- A. 0-RTT làm tăng đáng kể kích thước của mọi chứng chỉ máy chủ gửi
- B. 0-RTT chỉ hoạt động được khi bỏ hoàn toàn bước xác thực máy chủ
- C. 0-RTT buộc mỗi phiên phải đổi sang thuật toán mã hóa đối xứng khác
- **D. Dữ liệu gửi sớm dễ bị phát lại, nguy hiểm với yêu cầu không idempotent**

**Câu 49.** (Vận dụng cao) Máy chủ cấu hình thiếu chứng chỉ trung gian (intermediate CA) trong chuỗi gửi đi. Vì sao một số trình duyệt vẫn chạy được trong khi số khác báo lỗi chuỗi tin cậy?

- **A. Vì trình duyệt chạy được đã có sẵn bản trung gian trong cache trước đó**
- B. Vì HTTPS không thực sự cần có chứng chỉ trung gian để hoạt động
- C. Vì lỗi chuỗi tin cậy chỉ xảy ra trên trình duyệt di động, không desktop
- D. Vì máy chủ đã tự động gửi bù chứng chỉ trung gian cho các bên còn thiếu

**Câu 50.** (Vận dụng cao) Vì sao thời hạn hiệu lực của chứng chỉ TLS công khai bị rút ngắn dần qua từng năm, và đánh đổi đi kèm là gì?

- A. Rút ngắn giúp CA thu phí nhiều lần hơn từ mỗi khách hàng sử dụng dịch vụ
- **B. Rút ngắn giảm thiệt hại nếu khóa lộ, đổi lại buộc tự động hoá gia hạn**
- C. Rút ngắn chỉ nhằm ép buộc website chuyển sang dùng chứng chỉ wildcard
- D. Rút ngắn không kèm đánh đổi nào vì quy trình gia hạn vẫn luôn thủ công
