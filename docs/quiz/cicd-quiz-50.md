# Đề trắc nghiệm CI/CD — Dự án HushStore (PBL3)

50 câu, mỗi câu 1 điểm. Phân bố: **25** nhận biết/thông hiểu · **15** vận dụng · **10** vận dụng cao.

Quy tắc ra đề: phương án là mệnh đề ngắn, không chứa lời giải thích; bốn phương án mỗi câu có độ dài tương đương (chênh lệch trung bình 4.3 ký tự, lớn nhất 8); vị trí đáp án đúng chia đều A 13 · B 13 · C 12 · D 12.

Đáp án đúng được **in đậm**. Bản Apps Script để dựng Google Form: [`tao-form-cicd-quiz.gs`](tao-form-cicd-quiz.gs).

## Phần A — Nhận biết & Thông hiểu (câu 1–25)

**Câu 1.** (Nhận biết) Continuous Integration (CI) là gì?

- A. Tự động đưa mọi thay đổi lên production ngay sau khi merge code
- **B. Tích hợp code vào nhánh chung thường xuyên, có kiểm tra tự động**
- C. Dùng máy chủ riêng để build lại toàn bộ dự án vào mỗi buổi đêm
- D. Đánh giá code thủ công theo quy trình trước khi cho phép merge

**Câu 2.** (Nhận biết) Khác biệt cốt lõi giữa Continuous Delivery và Continuous Deployment là gì?

- A. Delivery áp dụng cho backend, Deployment áp dụng cho frontend
- B. Delivery cần hạ tầng cloud, Deployment chạy trên máy vật lý
- **C. Delivery dừng ở artifact sẵn sàng, Deployment tự lên production**
- D. Delivery chạy test tự động, Deployment chạy test thủ công

**Câu 3.** (Nhận biết) Mục tiêu quan trọng nhất của một pipeline CI là gì?

- A. Thay thế hoàn toàn khâu review code thủ công của con người
- **B. Phát hiện lỗi sớm, khi phạm vi thay đổi còn nhỏ và dễ sửa**
- C. Giảm chi phí hạ tầng bằng cách gom các bước vào một máy chủ
- D. Tự động sinh tài liệu kỹ thuật từ mã nguồn của dự án

**Câu 4.** (Nhận biết) Nguyên tắc build once, deploy many nghĩa là gì?

- A. Mỗi môi trường có một pipeline build riêng để tối ưu cấu hình
- B. Artifact được build lại mỗi lần deploy để bảo đảm luôn mới
- **C. Một artifact được build một lần rồi dùng lại cho mọi môi trường**
- D. Pipeline chỉ được phép chạy một lần trong mỗi ngày làm việc

**Câu 5.** (Nhận biết) Trong ngữ cảnh CI/CD, artifact là gì?

- **A. Sản phẩm đóng gói từ mã nguồn để đem triển khai, ví dụ image**
- B. Tập tin cấu hình mô tả các bước và điều kiện của pipeline
- C. Báo cáo kết quả test hiển thị trên giao diện của hệ thống CI
- D. Nhật ký do pipeline sinh ra trong lúc chạy, dùng để gỡ lỗi

**Câu 6.** (Thông hiểu) Lợi ích chính của việc gắn tag container image bằng commit SHA thay vì latest là gì?

- A. Commit SHA ngắn hơn nên tiết kiệm dung lượng registry
- B. Tag latest không dùng được với các registry riêng tư
- C. Tag latest bị registry giới hạn số lần ghi mỗi ngày
- **D. Truy vết được image đang chạy ứng với mã nguồn nào**

**Câu 7.** (Thông hiểu) Đặt registry ở chế độ immutable tag mang lại tính chất gì?

- **A. Một tag luôn trỏ tới đúng một nội dung image duy nhất**
- B. Nhiều pipeline có thể push song song mà không xung đột
- C. Registry chỉ giữ một bản sao mỗi tag nên tiết kiệm chỗ
- D. Image được bảo đảm không chứa lỗ hổng bảo mật đã biết

**Câu 8.** (Nhận biết) Multi-stage build trong Dockerfile phục vụ mục đích chính nào?

- A. Cho phép build song song nhiều image trong cùng một lần chạy
- **B. Tách môi trường build khỏi môi trường chạy, image cuối nhỏ hơn**
- C. Cho phép một image chạy được trên nhiều kiến trúc CPU khác nhau
- D. Là điều kiện bắt buộc để dùng được build cache của BuildKit

**Câu 9.** (Thông hiểu) Vì sao container nên chạy bằng một user thường thay vì root?

- A. Nhiều registry từ chối nhận image khai báo USER là root
- B. Tránh làm container khởi động chậm do phải nạp thêm quyền
- **C. Hạn chế quyền của kẻ tấn công nếu tiến trình bị chiếm quyền**
- D. Bảo đảm ứng dụng ghi được vào thư mục dữ liệu của mình

**Câu 10.** (Thông hiểu) Vì sao pipeline nên ghim phiên bản cụ thể của công cụ và base image?

- A. Vì công cụ chỉ tương thích ngược khi được ghim phiên bản
- B. Vì bản mới nhất thường chứa nhiều lỗ hổng đã biết hơn
- **C. Để cùng một commit luôn cho ra cùng một kết quả build**
- D. Vì registry tính phí cao hơn với các tag thay đổi liên tục

**Câu 11.** (Nhận biết) Build cache trong pipeline CI có tác dụng gì?

- A. Lưu kết quả test để lần chạy sau khỏi phải chạy lại test
- B. Lưu artifact cuối cùng để bước deploy lấy ra nhanh hơn
- C. Giữ bản sao mã nguồn để khỏi phải checkout lại từ đầu
- **D. Tái dùng kết quả của các bước không đổi để rút ngắn build**

**Câu 12.** (Nhận biết) Matrix build trong CI dùng để làm gì?

- A. Khai báo thứ tự phụ thuộc giữa các job trong workflow
- B. Phân bổ tài nguyên runner theo mức ưu tiên của từng job
- C. Chạy một pipeline trên nhiều repository trong cùng tổ chức
- **D. Chạy cùng một tập bước với nhiều tổ hợp tham số khác nhau**

**Câu 13.** (Thông hiểu) Trong matrix build đặt fail-fast, khi một nhánh thất bại thì điều gì xảy ra?

- A. Nhánh đó được tự động chạy lại một lần trước khi báo đỏ
- B. Toàn bộ workflow bị hủy, kể cả job nằm ngoài matrix
- **C. Các nhánh còn lại bị hủy để khỏi tốn tài nguyên vô ích**
- D. Các nhánh còn lại vẫn chạy hết để thu đủ thông tin lỗi

**Câu 14.** (Thông hiểu) Cơ chế concurrency group trong workflow giải quyết vấn đề gì?

- **A. Ngăn hai lần chạy cùng nhóm giẫm lên nhau trên cùng tài nguyên**
- B. Đồng bộ build cache giữa các job chạy trong cùng workflow
- C. Giới hạn số phút runner mà một repository được dùng mỗi tháng
- D. Tăng số job chạy song song để rút ngắn thời gian pipeline

**Câu 15.** (Thông hiểu) Vì sao pipeline nên kiểm tra cả ở pull request lẫn ở lần push vào nhánh chính?

- **A. Vì hai nhánh xanh riêng lẻ vẫn có thể hỏng sau khi gộp**
- B. Vì mỗi loại trigger chạy một bộ kiểm tra khác nhau hoàn toàn
- C. Vì nền tảng chỉ hiện trạng thái kiểm tra khi có đủ hai trigger
- D. Vì kết quả ở PR không đáng tin, người mở PR sửa được workflow

**Câu 16.** (Nhận biết) Required status check kết hợp với branch protection có tác dụng gì?

- A. Giới hạn số người được phép tạo pull request vào nhánh
- B. Tự động sửa lỗi định dạng code trước khi cho phép merge
- C. Buộc mọi commit đẩy lên nhánh đó phải được ký số hợp lệ
- **D. Chặn merge vào nhánh được bảo vệ khi kiểm tra chưa xanh**

**Câu 17.** (Nhận biết) Nguyên tắc đúng khi xử lý thông tin bí mật trong pipeline là gì?

- A. Đặt repository ở chế độ private là đủ để bảo vệ secret
- **B. Lấy từ kho bí mật lúc chạy, không hardcode, không in ra log**
- C. Mã hoá rồi commit vào repo kèm khoá giải mã ở nơi khác
- D. Lưu trong biến môi trường của repo và in ra log để gỡ lỗi

**Câu 18.** (Thông hiểu) So với khoá truy cập dài hạn lưu trong CI secret, cơ chế OIDC có lợi ích cốt lõi nào?

- **A. Không còn bí mật dài hạn, mỗi lần chạy nhận credential tạm**
- B. Cho phép pipeline truy cập nhiều tài khoản cloud cùng lúc
- C. Miễn cho pipeline khỏi phải cấu hình phân quyền chi tiết
- D. Giúp pipeline chạy nhanh hơn do bỏ được bước xác thực

**Câu 19.** (Nhận biết) Áp dụng least privilege cho quyền của pipeline nghĩa là gì?

- A. Chỉ một vài người trong nhóm được phép kích hoạt pipeline
- **B. Cấp đúng hành động và đúng tài nguyên pipeline cần, không hơn**
- C. Mọi thao tác ghi của pipeline đều phải có người phê duyệt
- D. Pipeline chỉ được phép chạy trên nhánh chính của repository

**Câu 20.** (Nhận biết) Infrastructure as Code mang lại lợi ích chính nào cho CI/CD?

- A. Cho phép triển khai mà không cần quyền trên tài khoản cloud
- B. Loại bỏ nhu cầu giám sát hệ thống sau khi đã triển khai
- C. Giảm chi phí cloud vì tài nguyên được tạo ra nhanh hơn
- **D. Hạ tầng nằm trong version control nên review và tái lập được**

**Câu 21.** (Nhận biết) Rolling update, blue-green và canary khác nhau chủ yếu ở điểm nào?

- A. Ở việc hệ thống dùng container hay dùng máy ảo truyền thống
- B. Ở số lượng môi trường mà tổ chức phải duy trì song song
- C. Ở việc migration cơ sở dữ liệu chạy trước hay sau khi deploy
- **D. Ở cách phiên bản mới thay thế bản cũ và nhận lưu lượng**

**Câu 22.** (Nhận biết) Trong hầu hết hệ thống CI, điều gì quyết định một bước thành công hay thất bại?

- A. Nội dung dòng cuối cùng mà lệnh in ra ở luồng stdout
- **B. Mã thoát của lệnh: 0 là thành công, khác 0 là thất bại**
- C. Việc lệnh có ghi nội dung nào vào luồng stderr hay không
- D. Thời gian chạy của bước có vượt ngưỡng cấu hình hay không

**Câu 23.** (Nhận biết) Bước quét lỗ hổng thư viện phụ thuộc trong pipeline nhằm mục đích gì?

- A. Kiểm tra giấy phép sử dụng của các thư viện bên thứ ba
- B. Đo độ phủ của bộ test tự động trên toàn bộ mã nguồn
- C. Tìm lỗi logic trong mã nguồn do lập trình viên tự viết ra
- **D. Tìm thư viện đang dùng có lỗ hổng bảo mật đã được công bố**

**Câu 24.** (Thông hiểu) Rollback và fix-forward khác nhau thế nào khi xử lý một bản phát hành lỗi?

- **A. Rollback về bản tốt đã biết, fix-forward vá rồi phát hành**
- B. Fix-forward chỉ dùng được khi đã có blue-green deployment
- C. Rollback áp dụng cho hạ tầng, fix-forward cho phần ứng dụng
- D. Rollback luôn an toàn hơn fix-forward trong mọi tình huống

**Câu 25.** (Thông hiểu) Kiểm tra readiness và liveness khác nhau ở chỗ nào?

- A. Readiness chạy một lần lúc khởi động; liveness chạy định kỳ
- B. Readiness kiểm tra tầng mạng; liveness kiểm tra tầng ứng dụng
- C. Readiness do bộ cân bằng tải gọi; liveness do người vận hành gọi
- **D. Readiness: sẵn sàng nhận lưu lượng; liveness: tiến trình còn sống**

## Phần B — Vận dụng (câu 26–40)

**Câu 26.** (Vận dụng) Một công cụ quét in ra danh sách lỗ hổng nhưng vẫn trả mã thoát 0. Cách xử lý đúng trong CI là gì?

- A. Thêm continue-on-error: false để bước đỏ khi có lỗ hổng
- **B. Đọc nội dung báo cáo rồi tự quyết định mã thoát của bước**
- C. Giữ nguyên, vì cảnh báo đã hiển thị trên giao diện của CI
- D. Tách bước đó ra job riêng cho dễ nhìn thấy phần cảnh báo

**Câu 27.** (Vận dụng) Một cổng kiểm tra chạy xong nhưng không quét được file nào. Kết quả đó nên xếp loại thế nào?

- A. Là cảnh báo, nên cho qua nhưng ghi chú lại để xử lý sau
- B. Là thành công, vì phép quét không tìm thấy vấn đề nào
- **C. Là không kết luận, phải coi như thất bại chứ không phải sạch**
- D. Là lỗi hạ tầng CI, nên bỏ qua khi tính trạng thái pipeline

**Câu 28.** (Vận dụng) Vì sao không nên cấp credential có quyền ghi cho lần chạy phát sinh từ PR của fork?

- **A. Vì code trong PR đó do người ngoài viết và vẫn được chạy**
- B. Vì lần chạy từ fork luôn bị nền tảng giới hạn thời gian
- C. Vì credential không truyền qua ranh giới repository được
- D. Vì fork không có quyền đọc mã nguồn của repository gốc

**Câu 29.** (Vận dụng) File state của công cụ IaC chứa mật khẩu database ở dạng rõ. Hệ quả đúng với phân quyền là gì?

- A. Chỉ cần bật mã hoá phía lưu trữ là mọi vai trò đọc được
- B. Không sao, vì công cụ IaC luôn che giá trị nhạy cảm đi
- C. Nên commit state vào repo để có lịch sử thay đổi rõ ràng
- **D. Quyền đọc state phải siết như quyền đọc chính bí mật đó**

**Câu 30.** (Vận dụng) Trust policy cấp quyền cho pipeline được viết bằng so khớp wildcard cho cả repository. Rủi ro là gì?

- A. Chỉ hoạt động với nhánh chính, các nhánh khác sẽ hỏng
- **B. Khớp cả nhánh, tag và pull request ngoài ý định trao quyền**
- C. Không có rủi ro vì repository vốn đã là ranh giới tin cậy
- D. Làm chậm quá trình xác thực nên pipeline hay bị timeout

**Câu 31.** (Vận dụng) Vì sao nên chạy migration như bước riêng có cổng, thay vì để ứng dụng tự chạy lúc khởi động?

- A. Vì chạy lúc khởi động làm ứng dụng khởi động chậm hơn nhiều
- B. Vì migration chạy lúc khởi động không tương thích container
- **C. Vì migration hỏng sẽ bị chặn lại, bản đang chạy vẫn phục vụ**
- D. Vì chỉ bước riêng mới ghi được log đầy đủ của migration

**Câu 32.** (Vận dụng) Pipeline tạo snapshot cơ sở dữ liệu ngay trước bước migration. Vai trò đúng của snapshot này là gì?

- A. Là cách để pipeline tự rollback khi migration thất bại
- **B. Là điểm quay về cho dữ liệu khi migration làm hỏng nó**
- C. Là bản sao cấp cho môi trường phát triển sau mỗi deploy
- D. Thay cho việc kiểm thử migration ở môi trường staging

**Câu 33.** (Vận dụng) Chính sách migration forward-only áp ràng buộc gì lên cách viết migration?

- A. Chỉ được thêm bảng mới, không được sửa bảng đang có dữ liệu
- B. Mỗi migration phải chạy trọn trong một transaction duy nhất
- C. Phải tắt toàn bộ ứng dụng trong lúc migration đang chạy
- **D. Lược đồ mới phải tương thích ngược với phiên bản ứng dụng cũ**

**Câu 34.** (Vận dụng) Vì sao artifact đem deploy phải đúng là bản đã đi qua các bước kiểm tra?

- A. Vì registry từ chối nhận hai image có nội dung giống nhau
- B. Vì các bước kiểm tra chỉ chạy được một lần cho mỗi commit
- **C. Vì bản build lại có thể khác, kết quả kiểm tra hết giá trị**
- D. Vì build lại tốn thêm thời gian và phút runner của tổ chức

**Câu 35.** (Vận dụng) Bước deploy chọn phiên bản theo kiểu ngầm định lấy bản mới nhất, thay vì ghim bản vừa tạo. Vì sao nguy hiểm?

- A. Vì nền tảng cần vài phút mới cập nhật danh sách bản mới nhất
- B. Vì các bản cũ sẽ bị tự động xoá đi khi có bản mới hơn
- **C. Vì cái được kiểm có thể không phải cái được chạy, mà vẫn xanh**
- D. Vì cách đó cần thêm quyền đọc nên vi phạm least privilege

**Câu 36.** (Vận dụng) Vì sao bước gọi thử URL công khai, đi qua DNS và CDN, không nên dùng làm cổng chặn deploy?

- **A. Vì nó có thể đỏ cả khi ứng dụng khoẻ, gây rollback nhầm**
- B. Vì bước đó tốn thời gian nên không nên đặt vào đường chặn
- C. Vì mã HTTP công khai luôn kém tin cậy hơn log của container
- D. Vì tường lửa của runner thường chặn lưu lượng ra bên ngoài

**Câu 37.** (Vận dụng) Pipeline được thiết kế để không tự bật hạ tầng đang tắt. Cách canh ràng buộc này bền vững nhất là gì?

- **A. Bỏ quyền tương ứng khỏi vai trò mà pipeline dùng để chạy**
- B. Yêu cầu mọi thay đổi file workflow phải có hai người review
- C. Ghi chú rõ ở đầu file workflow để người sau không sửa nhầm
- D. Đặt một biến cấu hình bật/tắt trong repository của dự án

**Câu 38.** (Vận dụng) Một ứng dụng chạy hoàn toàn trong trình duyệt cần biết địa chỉ API. Vì sao giá trị đó phải đưa vào lúc build?

- A. Vì cấu hình lúc chạy sẽ làm lộ địa chỉ API cho người dùng
- **B. Vì mã đã tải về trình duyệt không đọc được biến môi trường**
- C. Vì máy chủ web tĩnh không hỗ trợ đọc biến môi trường
- D. Vì biến môi trường của container được mã hoá khi truyền đi

**Câu 39.** (Vận dụng) Một giá trị cấu hình buộc phải lặp ở hai nơi do hạn chế của nền tảng. Cách xử lý có trách nhiệm nhất là gì?

- A. Chuyển cả hai giá trị vào secret để tránh bị sửa nhầm
- B. Xoá một chỗ và để nền tảng dùng giá trị mặc định của nó
- **C. Ghi chú ở cả hai nơi và tìm cách kiểm tra tự động sự khớp**
- D. Chấp nhận bỏ qua, vì giá trị này rất hiếm khi thay đổi

**Câu 40.** (Vận dụng) Hệ thống chỉ có một instance và cổng host cố định nên mỗi lần deploy đều gián đoạn. Cách giảm đúng nguyên lý là gì?

- A. Dời lịch deploy sang giờ thấp điểm và thông báo trước
- B. Bỏ bước kiểm tra kết nối database để container lên nhanh hơn
- C. Tăng thời gian chờ health check của bộ cân bằng tải lên
- **D. Chạy nhiều instance và dùng cổng động để chồng lấn phiên bản**

## Phần C — Vận dụng cao (câu 41–50)

**Câu 41.** (Vận dụng cao) Cổng kiểm tra migration vô tình chạy công cụ của bản cũ thay vì bản của commit vừa push. Hậu quả đúng nhất là gì?

- A. Nền tảng tự chọn lại bản mới nhất nên không có hậu quả gì
- B. Công cụ cũ báo lỗi lược đồ, pipeline đỏ và dừng lại an toàn
- **C. Cổng báo PASS, code mới chạy trên lược đồ còn thiếu migration**
- D. Migration chạy hai lần, ràng buộc duy nhất sẽ chặn lần sau

**Câu 42.** (Vận dụng cao) Bộ test IaC đã đầy đủ, nhưng vẫn cần khẳng định lúc chạy rằng đúng artifact của commit này sắp được thực thi. Vì sao?

- A. Vì test hạ tầng chỉ chạy ở pull request, không chạy khi push
- **B. Vì lỗi này nằm trong logic pipeline, ngoài phạm vi bộ test đó**
- C. Vì khẳng định lúc chạy thay thế được nhu cầu viết test IaC
- D. Vì bộ test hạ tầng không truy cập được môi trường production

**Câu 43.** (Vận dụng cao) Nguyên tắc đúng khi sắp thứ tự các bước trong một job deploy là gì?

- **A. Chuẩn bị xong hết trước, chạm vào bản đang phục vụ sau cùng**
- B. Đặt bước rủi ro cao lên trước để phát hiện lỗi sớm nhất
- C. Gộp bước chuẩn bị và bước chuyển lưu lượng làm một cho gọn
- D. Đặt bước dọn dẹp ngay sau kiểm tra để giải phóng tài nguyên

**Câu 44.** (Vận dụng cao) Pipeline quay ứng dụng về bản trước nhưng không quay lược đồ database. Điều kiện nào khiến thiết kế này an toàn?

- A. Phải tắt ứng dụng trong suốt quá trình rollback đang diễn ra
- B. Mỗi lần deploy chỉ được chứa tối đa một migration lược đồ
- **C. Mọi thay đổi lược đồ phải tương thích ngược với bản ứng dụng cũ**
- D. Phải khôi phục snapshot dữ liệu cùng lúc với rollback ứng dụng

**Câu 45.** (Vận dụng cao) Bước dọn dẹp tài nguyên ở cuối job deploy thỉnh thoảng lỗi vì sự cố tạm của nhà cung cấp. Xử lý đúng là gì?

- **A. Không để lỗi bước phụ đổi kết luận về lần deploy đã xong**
- B. Bỏ hẳn bước dọn dẹp và chuyển sang làm thủ công định kỳ
- C. Chuyển bước dọn dẹp lên trước bước chuyển lưu lượng sang
- D. Để bước đó làm job đỏ, vì mọi thất bại đều phải hiện rõ

**Câu 46.** (Vận dụng cao) Bước tiền kiểm trạng thái hạ tầng bị lỗi nên không cho ra kết luận nào. Báo cáo tổng kết nên nói gì?

- **A. Nói rõ là chưa biết trạng thái, và chỉ chỗ cần xem tiếp**
- B. Không in gì thêm để tránh gây nhiễu cho người đọc báo cáo
- C. Coi như hạ tầng đang tắt, vì đó là mặc định an toàn hơn
- D. Coi như hạ tầng đang bật và cảnh báo người vận hành kiểm tra

**Câu 47.** (Vận dụng cao) Khi cần bảo đảm một vai trò không bao giờ đọc được một loại tài nguyên, vì sao Deny tường minh bền hơn thu hẹp Allow?

- A. Vì Allow không chỉ định được tài nguyên ở mức chi tiết
- B. Vì Deny được hệ thống phân quyền đánh giá nhanh hơn Allow
- C. Vì Deny áp dụng cho mọi vai trò khác trong cùng tài khoản
- **D. Vì Deny thắng mọi Allow, kể cả Allow được thêm vào sau này**

**Câu 48.** (Vận dụng cao) Một ràng buộc được canh bằng cả logic pipeline lẫn việc không cấp quyền cho vai trò. Vì sao lớp thứ hai có giá trị riêng?

- A. Vì nền tảng CI không bảo đảm thứ tự thực thi giữa các bước
- **B. Vì logic trong file workflow có thể bị sửa hoặc viết nhầm**
- C. Vì lớp phân quyền giúp pipeline chạy nhanh hơn do bớt lệnh
- D. Vì chỉ lớp phân quyền mới ghi được nhật ký kiểm toán

**Câu 49.** (Vận dụng cao) Một kiểm tra chỉ hỏng khi nhiều tiến trình chạy đồng thời, và nó vừa cho kết quả xanh ở một lần chạy. Diễn giải đúng là gì?

- **A. Một lần đỏ là bằng chứng có lỗi, một lần xanh thì không**
- B. Xanh và đỏ giá trị ngang nhau, nên lấy lần chạy gần nhất
- C. Nên bỏ loại kiểm tra này vì kết quả của nó không ổn định
- D. Một lần xanh là đủ kết luận đã sửa nếu mã nguồn không đổi

**Câu 50.** (Vận dụng cao) Một lớp lỗi về đúng đắn dữ liệu, như vượt hạn mức hay ghi trùng, vẫn trả HTTP 200. Hệ quả với thiết kế cổng kiểm tra là gì?

- A. Cần tăng số lần thử lại của kiểm tra để lỗi lộ ra rõ hơn
- **B. Cổng phải khẳng định bất biến trên dữ liệu, không dựa mã HTTP**
- C. Cần đo ở tầng hạ tầng: tỉ lệ lỗi và độ trễ của cân bằng tải
- D. Chỉ cần ghi log chi tiết hơn ở tầng ứng dụng là phát hiện được

## Bảng đáp án

| Câu | ĐA | Câu | ĐA | Câu | ĐA | Câu | ĐA | Câu | ĐA |
|---|---|---|---|---|---|---|---|---|---|
| 1 | B | 11 | D | 21 | D | 31 | C | 41 | C |
| 2 | C | 12 | D | 22 | B | 32 | B | 42 | B |
| 3 | B | 13 | C | 23 | D | 33 | D | 43 | A |
| 4 | C | 14 | A | 24 | A | 34 | C | 44 | C |
| 5 | A | 15 | A | 25 | D | 35 | C | 45 | A |
| 6 | D | 16 | D | 26 | B | 36 | A | 46 | A |
| 7 | A | 17 | B | 27 | C | 37 | A | 47 | D |
| 8 | B | 18 | A | 28 | A | 38 | B | 48 | B |
| 9 | C | 19 | B | 29 | D | 39 | C | 49 | A |
| 10 | C | 20 | D | 30 | B | 40 | D | 50 | B |
