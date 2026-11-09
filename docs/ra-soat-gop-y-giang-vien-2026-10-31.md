# Rà soát dự án HushStore theo góp ý của giảng viên

> **Lịch sử — cập nhật 08/11/2026:** phương án Cloudflare WAF/origin lockdown đã được gỡ khỏi mã nguồn. Không thực hiện các bước triển khai Cloudflare trong tài liệu này; các số đo và quyết định bên dưới được giữ theo mốc cũ. Xem [rollback hiện tại](rollback-cloudflare-waf-2026-11-08.md).

**Ngày rà soát:** 31/10/2026.

**Phạm vi:** mã Terraform hiện tại, cấu hình mẫu, script vận hành, tài liệu và hai sơ đồ draw.io; bao gồm thay đổi SG/WAF đang có trong working tree.

**Trạng thái:** đây là kết quả phân tích và danh sách đề xuất. Phiên rà soát không sửa cấu hình hạ tầng, không gọi API xác thực AWS/Cloudflare, không apply và không kiểm thử runtime.

**Theo dõi ngày 02/11:** bản sửa IP proxy, guard script, kiểm tra origin/header và
backend WAF đã được chuẩn bị sau bản rà soát này; xem [runbook WAF](waf-cloudflare.md).
Các nhận xét dưới đây giữ ngữ cảnh ngày rà soát; phần dependency ALB, route DB và
các đề xuất HA/RDS khác chưa được sửa trong đợt chuẩn bị này.

## 1. Kết luận chính

Đa số cấu trúc giảng viên đề nghị **đã có trong mã nguồn**. Không cần xây lại ECS, bổ sung subnet app thứ hai, thêm ALB access log hoặc đổi sang Route 53 chỉ vì bản nhận xét còn mô tả thiết kế cũ.

Những việc cần tập trung là:

1. Sửa các điểm tài liệu/sơ đồ còn sai, đặc biệt luồng WAF và mô tả cách subnet bảo vệ database.
2. Làm khớp yêu cầu DB “chỉ có local route” với association S3 endpoint hiện tại.
3. Khép phụ thuộc tạo tài nguyên giữa listener ALB, ECS Service và bucket policy ghi log.
4. Xử lý nhận diện IP người dùng qua Cloudflare trước khi nghiệm thu rate limit theo IP.
5. Hoàn tất migration NACL và triển khai WAF theo runbook đã chuẩn bị, rồi thu bằng chứng mới.
6. Ghi rõ chế độ PBL tiết kiệm có một EC2; chỉ triển khai hai EC2 hoặc thêm interface endpoint khi có mục tiêu kiểm chứng và ngân sách tương ứng.

Các kết luận “đã có” dưới đây nói về **cấu hình trong repo**, không tự chứng minh trạng thái đang chạy trên AWS.

## 2. Đối chiếu từng nhóm nhận xét

| Nhận xét | Kết quả đối chiếu | Việc còn cần làm |
|---|---|---|
| Thể hiện ECS Cluster / Service / Task | Có cluster, hai Service và hai task definition riêng cho web/API. Sơ đồ hiện tại cũng có các thành phần này. | Làm rõ quan hệ quản lý và luồng traffic trên bản trình chiếu; không cần chuyển đổi nền tảng một lần nữa. |
| NAT riêng theo AZ | Mã hỗ trợ cả một NAT dùng chung và NAT riêng từng AZ. Root prod mặc định `nat_gateway_count = 2`; dev mặc định `1`. `enable_nat` mặc định tắt. | Chú thích rõ chế độ prod/dev và trạng thái tắt; không nói “đang có hai NAT” chỉ từ default. |
| DB không có route ngoài local | Không có default route đi NAT/IGW, nhưng DB route table đang được gắn S3 Gateway Endpoint. | Nếu giữ tiêu chí local-only, bỏ association endpoint với DB, giữ endpoint cho app. |
| Một private app subnet mỗi AZ | Đã có `10.20.10.0/24` và `10.20.11.0/24`, mỗi subnet ở một AZ. | Không cần tạo thêm subnet để đáp ứng nhận xét này. |
| Tên subnet theo tier/AZ | Có convention `hushstore-public-a`, `hushstore-app-a`, `hushstore-db-a`, tương tự AZ-b. | Có thể ghi đủ `1a/1b` trên hình cho dễ đọc; đổi Name tag là cải thiện trình bày, không phải yêu cầu đổi CIDR hay tạo lại subnet. |
| RDS Multi-AZ, backup, encryption | `multi_az` nhận từ input; root prod mặc định true, dev false. Backup mặc định 7 ngày; storage encryption bật; database private. | Đo lại cấu hình thực tế trong cửa sổ triển khai. Cân nhắc bảo vệ xóa/final snapshot riêng ở mục 4. |
| SG database cổng 1433 | Nhận xét này thuộc thiết kế SQL Server cũ. Mã hiện tại dùng PostgreSQL, cổng **5432**. | Giữ 5432; không mở thêm 1433. |
| Web outbound 80/443 | Đã có TCP 80/443 ra Internet và TCP 5432 tới SG RDS. | Ghi nhận outbound còn rộng; thêm endpoint không tự động thu hẹp các rule Internet này. |
| Rule health check từ ALB | Đã cho phép ALB SG tới web 80 và API 8080. Health check dùng `/healthz` và `/health/ready` trên các cổng đó. | Không cần rule health check trùng hoặc mở thêm dải cổng. |
| Bỏ custom NACL | Custom ACL đã được loại khỏi cấu hình đích; có công cụ/runbook migration hai mốc. | Chưa xác nhận migration thực tế; phải chuyển association trước khi xóa ACL/rule. |
| Target group do ECS Service quản lý | Hai Service có `load_balancer` gắn đúng container/cổng; không có attachment thủ công tới EC2 trong cấu hình hiện tại. | Bổ sung phụ thuộc listener để tránh lỗi thứ tự tạo, xem mục 3.3. |
| ALB access log vào S3 | Đã bật trong module ALB, có bucket, lifecycle và delivery policy. | Kiểm tra thứ tự cấp policy và bằng chứng log được ghi thực tế. |
| Bổ sung WAF | Có root Cloudflare riêng, rule chặn đường dẫn nhạy cảm, chặn CIDR tùy chọn, rate limit login và header bảo vệ origin. | Chưa apply/đo runtime; kiểm tra managed rules trên zone và bật origin lockdown đúng trình tự. |
| Task definition riêng cho frontend/backend | Đã có web và API riêng; mỗi host vẫn có thể chạy một task của mỗi Service. | Khả năng scale độc lập bị giới hạn bởi static host port và cách root buộc task count bằng instance count. |
| IAM roles, không hard-code AWS credential | Có instance role, execution role, application role và các role riêng cho tác vụ hỗ trợ; ứng dụng dùng credential tạm thời của role. | Chưa cần thay mô hình IAM; có thể thu hẹp execution role dùng chung ở đợt hardening sau. |
| SSM Parameter Store / Secrets Manager | Đang dùng SSM `SecureString`, inject vào task qua `secrets/valueFrom`. | Đã đáp ứng lựa chọn SSM; không cần thêm Secrets Manager chỉ để trùng tên dịch vụ trong gợi ý. |
| CloudWatch Logs driver | Cả API/web/migrator/seeder có `awslogs`. | Thu log sau triển khai; cấu hình driver không thay cho bằng chứng delivery. |
| Terraform module và môi trường | Đã chia tám module và hai folder prod/dev, backend key riêng. Dev giữ symlink main/outputs sang prod. | Giữ cấu trúc hiện tại; không cần thêm workspace để tách lại cùng hai môi trường. |
| Output ALB DNS / thông tin DB qua SSM | Có `alb_dns_name`, `rds_endpoint`; connection string lưu trong SSM và ARN được truyền sang ECS. | Có thể xuất thêm tên/ARN parameter ở root để tiện thao tác; không xuất giá trị connection string. |
| ALB + ACM + Route 53 | Đã có ALB + ACM; DNS/proxy hiện dùng Cloudflare. | Route 53 là phương án thay thế DNS, không phải thành phần bắt buộc phải thêm bên cạnh Cloudflare. |

Bằng chứng chính: [root prod](../infra/tf/envs/prod/main.tf), [biến prod](../infra/tf/envs/prod/variables.tf), [biến dev](../infra/tf/envs/dev/variables.tf), [network](../infra/tf/modules/network/vpc.tf), [SG](../infra/tf/modules/security/main.tf), [ECS Service](../infra/tf/modules/ecs/service.tf), [task definition](../infra/tf/modules/ecs/taskdef.tf), [IAM](../infra/tf/modules/ecs/iam.tf), [ALB](../infra/tf/modules/alb/alb.tf), [RDS/SSM](../infra/tf/modules/data/main.tf).

## 3. Những việc nên sửa trước lần nghiệm thu tiếp theo

### 3.1. Khôi phục đúng IP người dùng khi đi qua Cloudflare

**Mức ưu tiên: cao trước khi nghiệm thu rate limit/log theo IP.**

Ứng dụng đọc `RemoteIpAddress` để phân vùng rate limit, trong khi forwarded headers chỉ xử lý một hop:

- [Program.cs:509–566](../src/API/Program.cs): `ForwardLimit = 1`, `KnownNetworks/KnownProxies` đang được clear.
- [DbRateLimitAttribute.cs:137–138](../src/API/Filters/DbRateLimitAttribute.cs): rate limiter dùng PostgreSQL cũng lấy `RemoteIpAddress`.
- [alb.tf:26–34](../infra/tf/modules/alb/alb.tf): đã ghi nhận ALB append IP peer trực tiếp, tức IP edge khi đi qua Cloudflare.

**Suy luận từ chuỗi proxy và cấu hình:** với luồng người dùng → Cloudflare → ALB → API, app lấy IP Cloudflare ở hop cuối, nhiều người dùng có thể chung phân vùng đếm. Rate limit Cloudflare có sẵn không sửa danh tính IP ở lớp ứng dụng, và rule login tại edge không thay cho mọi policy ứng dụng.

Cloudflare cung cấp `CF-Connecting-IP` cho IP người truy cập, nhưng chỉ nên dùng sau khi xác thực đường tới origin và xác định rõ các proxy/Worker được tin cậy. Không chỉ tăng `ForwardLimit` lên 2 khi vẫn tin mọi proxy, cũng không tin header do request bất kỳ tự khai. [Cloudflare HTTP headers](https://developers.cloudflare.com/fundamentals/reference/http-headers/)

**Đề xuất:** thiết kế cách lấy IP theo chế độ direct-ALB/proxied, gắn với origin lockdown và ranh giới proxy đã xác thực. Kiểm thử hai client khác IP qua cùng edge không chia sẻ quota; thử giả mạo XFF/CF header không đổi được danh tính; health check vẫn hoạt động. Đây là khoảng trống đã nhận diện trong thiết kế WAF trước, nay cần xử lý trước khi tuyên bố bảo vệ theo IP đã hoàn thiện.

### 3.2. DB route table chưa thật sự local-only

**Mức ưu tiên: chỉnh cấu hình để khớp yêu cầu, không phải phát hiện database đang mở Internet.**

[vpc.tf:220–223](../infra/tf/modules/network/vpc.tf) đưa cả route table DB vào `aws_vpc_endpoint.s3.route_table_ids`. AWS tự thêm route tới prefix list S3 vào các route table được chọn. [AWS Gateway endpoints — Routing](https://docs.aws.amazon.com/vpc/latest/privatelink/gateway-endpoints.html)

SG RDS hiện không có outbound và DB không có default route ra Internet. Vì vậy không được diễn giải association này thành “RDS đang truy cập Internet”. Tuy nhiên, câu “DB chỉ có local route” chưa đúng cấu hình đích.

**Đề xuất:** chỉ associate S3 endpoint với các app route table, bỏ DB association sau khi xác nhận không có workload DB-tier cần truy cập S3. Không xóa toàn bộ S3 endpoint. Thêm test kiểm tra tập route table nguồn/đích chính xác; kiểm tra route thực tế sau apply, vì mock test không phát hiện route ngoài Terraform do thao tác tay.

### 3.3. ECS Service có thể được tạo trước khi target group gắn ALB

**Mức ưu tiên: cao cho khả năng dựng lại serving stack. Đây là thiếu phụ thuộc trong mã, chưa tái hiện lỗi runtime ở phiên này.**

[alb/outputs.tf:11–18](../infra/tf/modules/alb/outputs.tf) xuất trực tiếp ARN target group. Hai ECS Service có dependency tường minh tới capacity provider, nhưng không có dependency tới forwarding listener rule tại [service.tf:92 và 173](../infra/tf/modules/ecs/service.tf). Cùng bật bằng một cờ không tạo quan hệ thứ tự trong Terraform.

Do đó, lần tạo mới có thể gọi `CreateService` khi target group đã tồn tại nhưng chưa được listener sử dụng. AWS cũng chỉ rõ dependency listener/listener rule trong cấu hình ECS Service. [AWS ECS Service reference](https://docs.aws.amazon.com/AWSCloudFormation/latest/TemplateReference/aws-resource-ecs-service.html)

**Đề xuất:** cho output ARN của từng target group phụ thuộc forwarding rule tương ứng, để consumer ECS chỉ chạy sau khi ALB association hoàn tất. Giữ tham chiếu hẹp, tránh tạo dependency vòng bằng cách buộc cả module ALB phụ thuộc cả module ECS. Terraform hỗ trợ `depends_on` trên output để biểu diễn phụ thuộc không suy ra được từ riêng giá trị trả về. [HashiCorp output reference](https://developer.hashicorp.com/terraform/language/block/output)

**Nghiệm thu:** kiểm tra dependency graph, validate hai môi trường và kiểm thử lại tình huống bật serving stack từ trạng thái ALB/Service chưa tồn tại trong cửa sổ được phép triển khai.

### 3.4. Kiểm tra thứ tự cấp quyền ALB ghi access log

[storage/outputs.tf:61–64](../infra/tf/modules/storage/outputs.tf) trả tên bucket, nhưng không phụ thuộc bucket policy tại [s3.tf:216](../infra/tf/modules/storage/s3.tf). ALB có thể bật access log trước khi policy được cấp trong lần dựng mới. Bucket tồn tại từ trước có thể che khuất vấn đề này; chưa tái hiện lỗi tại phiên rà soát.

**Đề xuất:** cho output bucket log phụ thuộc policy liên quan. AWS yêu cầu policy cho phép ELB ghi trước khi cấu hình log. Sau triển khai, kiểm tra cả file kiểm tra quyền và log request thực tế. [AWS Enable ALB access logs](https://docs.aws.amazon.com/elasticloadbalancing/latest/application/enable-access-logging.html)

### 3.5. Sửa các nội dung gây hiểu sai trong tài liệu và hình

| Vị trí | Hiện trạng | Nội dung cần chỉnh |
|---|---|---|
| `hushstore-aws-don-gian.drawio`, trang 1, `e31/e32/e34` | User → Cloudflare chỉ ghi DNS; HTTPS đi User → IGW → ALB, không có cạnh Cloudflare → origin. | Vẽ User → Cloudflare WAF → ALB; có thể đi qua biểu tượng IGW. Phân biệt DNS lookup với luồng HTTP được proxy. |
| `thiet-ke-he-thong-aws.md:469–472` | Nói tách subnet mới chặn được app→DB và tạo lớp lọc ở subnet. | Giải thích route table tạo phân tách đường đi; SG tham chiếu nguồn thực thi quyền vào DB, kể cả khi tài nguyên cùng subnet. Default NACL allow-all không cung cấp lớp lọc tùy chỉnh. |
| `prod/terraform.tfvars.example:21–25` | Hướng dẫn tra version `sqlserver-ex` nhưng dùng version PostgreSQL `17`. | Đổi lệnh tra engine sang `postgres`. |
| `prod/terraform.tfvars.example:15,34` | Nói max host cố định 1 và có ba image. | Nêu giới hạn cấu hình 1/2 host có guard; cập nhật bốn image. |
| Sơ đồ đơn giản trang 1, `n27` | ECR ba repo. | Bốn repo: web, API, migrator, seeder. |
| Sơ đồ đầy đủ trang 3, `ci2` | Ghim 93 Terraform tests. | Dẫn nguồn kết quả có ngày đo hoặc bỏ số đếm dễ lỗi thời; kết quả trước đây không là kết quả chạy lại hôm nay. |
| `thiet-ke-he-thong-aws.md:1077–1088`; sơ đồ đầy đủ trang 5, `ra1` | Thiếu migrator role trong danh sách compute; nói host không có quyền S3. | Đối chiếu năm role compute và quyền host `s3:GetObject` trên artifacts, tại `ecs/iam.tf:46–54`. |
| `docs/README.md:165–175` | Chỉ mô tả evidence hiện hành là SQL Server, bỏ qua lần đo PostgreSQL tháng 9. | Bổ sung liên kết/bối cảnh báo cáo ngày 24/09; tiếp tục ghi rõ báo cáo đó chưa chứng minh SG/WAF tháng 10. |

Không sửa số liệu trong báo cáo cũ hoặc `docs/evidence/`. [Báo cáo tháng 9](security-validation-report-2026-09-24.md) đã ghi PostgreSQL 5432 và Multi-AZ, nhưng vẫn có NACL/demo DENY. Cần cập nhật mục lục và bản mô tả hiện tại, đồng thời giữ nguyên bản ghi lịch sử.

## 4. Những đánh đổi cần quyết định theo mục tiêu PBL

### 4.1. Hai AZ trong mạng chưa đồng nghĩa ứng dụng có HA

Root prod mặc định `max_instance_count = 1`; script `up.sh` lấy số host từ giới hạn này. Root buộc `service_desired_count = instance_count` tại [main.tf:90](../infra/tf/envs/prod/main.tf). Khi bật theo cấu hình đó chỉ có một EC2, một task web và một task API. Hai app subnet và ALB phủ hai AZ không loại bỏ điểm lỗi của host duy nhất.

[service.tf:45–46,125–126](../infra/tf/modules/ecs/service.tf) đặt min healthy 0 với một host và max 100, tương ứng deploy dừng task cũ trước. Đây là downtime được chấp nhận để giữ bridge/static host port 80/8080 và chi phí thấp. ECS managed scaling cũng đang tắt tại [cluster.tf:193](../infra/tf/modules/ecs/cluster.tf).

**Khuyến nghị:** giữ chế độ một host nếu mục tiêu là PBL có giới hạn chi phí, nhưng ghi đúng giới hạn. Nếu cần chứng minh chịu lỗi một AZ, mở cửa sổ hai host/hai task mỗi Service, kiểm tra placement, năng lực máy và hành vi failover; không kết luận chỉ từ `spread` hay hai ô EC2 trên hình. Không bắt buộc đổi sang Fargate hoặc dynamic host port để trả lời góp ý này.

Ứng dụng đã có bộ đếm xác thực dùng PostgreSQL, nhưng cờ `rate_limiter_is_distributed` chỉ là lời khai cấu hình, không thay bằng chứng kiểm thử. WAF login rule không đủ để tự bật cờ đó.

DataProtection có code SSM có điều kiện tại [Program.cs:161–174](../src/API/Program.cs), nhưng chưa inject `DataProtection__SsmPrefix` và chưa có quyền key-ring trong task app IAM. Cần kiểm tra feature nào thực sự dùng key ring trước khi mở hai task, rồi nối cấu hình/quyền prefix hẹp nếu cần. Không coi đây là lỗi JWT đã đo được: JWT đang dùng secret chung qua SSM.

### 4.2. Interface endpoint không tự động tiết kiệm hơn NAT

Hiện có S3 gateway endpoint và ECR API/DKR endpoint tùy chọn; chưa có endpoint cho SSM, Logs, Secrets Manager hoặc ECS control plane.

Với NAT hoạt động và outbound HTTPS được chấp nhận, việc chưa có những interface endpoint này không tự làm hệ thống thiếu chức năng. Interface endpoint tính tiền theo thời gian tồn tại ở từng AZ, cộng xử lý dữ liệu; endpoint vẫn có thể tính tiền khi EC2 đã tắt. S3 gateway endpoint không có phí sử dụng endpoint bổ sung. [AWS PrivateLink pricing](https://aws.amazon.com/privatelink/pricing/), [AWS Gateway endpoints](https://docs.aws.amazon.com/vpc/latest/privatelink/gateway-endpoints.html)

**Khuyến nghị:** giữ NAT + S3 cho cấu hình PBL hiện tại. Nếu cần demo private service access, chọn endpoint theo dependency thực sự và đo chi phí. Không giữ nhận định “hai ECR endpoint chắc chắn có lãi” trong comment khi chưa có số đo lưu lượng và thời gian chạy.

Nếu mục tiêu chuyển sang **ECS EC2 vận hành không NAT**, cần thiết kế đầy đủ đường tới ECS (`ecs`, `ecs-agent`, `ecs-telemetry`), ECR API/DKR + S3, CloudWatch Logs, SSM cho secret/Session Manager và các API mà ứng dụng hoặc bootstrap sử dụng. `ec2messages` phụ thuộc phiên bản agent/region, không phải endpoint luôn phải thêm; Secrets Manager chỉ cần khi thực sự chuyển sang dùng dịch vụ đó. Chỉ bật ECR endpoint chưa tạo thành serving stack không NAT. [AWS ECS endpoints](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/vpc-endpoints.html), [AWS SSM endpoints](https://docs.aws.amazon.com/systems-manager/latest/userguide/setup-create-vpc.html)

### 4.3. Bảo vệ dữ liệu RDS khi xóa

Multi-AZ, encryption và backup 7 ngày đã có. Tuy nhiên [data/main.tf:127–129](../infra/tf/modules/data/main.tf) cố định `deletion_protection = false`; `skip_final_snapshot` mặc định true tại [data/variables.tf:51–55](../infra/tf/modules/data/variables.tf). Root chưa truyền lựa chọn cho hai hành vi này.

Đây là đánh đổi tháo dỡ nhanh của lab, không phù hợp để mô tả là cấu hình production bảo vệ dữ liệu đầy đủ. Nếu giữ dữ liệu quan trọng, bổ sung input phân biệt prod/dev, bật bảo vệ xóa cho prod và cấu hình final snapshot identifier khi cần snapshot. Phải cập nhật `nuke.sh`/runbook và kiểm tra restore trong đợt riêng; không lẫn thay đổi này vào migration ACL đang có phạm vi chặt.

### 4.4. Giữ Cloudflare cho DNS và WAF

Lựa chọn Cloudflare trước đây phù hợp yêu cầu WAF không bắt buộc của AWS. Không cần thêm AWS WAF hoặc chuyển DNS sang Route 53 chỉ để làm giống danh sách gợi ý. Cloudflare Free có managed rules subset tự triển khai; phải kiểm tra trên zone, không được mô tả tương đương bộ rule OWASP đầy đủ hoặc dùng mock test để chứng minh đang chặn thật. [Cloudflare WAF — Get started](https://developers.cloudflare.com/waf/get-started/)

Các việc còn lại là vận hành: bảo vệ state chứa origin secret, kiểm tra ruleset đang có trước import, xác nhận DNS trỏ đúng ALB sau mỗi lần dựng lại, và triển khai edge trước origin lockdown. Hướng dẫn chi tiết ở [waf-cloudflare.md](waf-cloudflare.md). Lỗi DNS và CI/NuGet được ghi trong hồ sơ trước là việc cần xác minh riêng, chưa được kiểm tra lại trong phiên này.

## 5. Thứ tự thực hiện đề xuất

| Đợt | Công việc | Tiêu chí hoàn tất |
|---|---|---|
| 1 — sửa mô tả | Chỉnh các điểm mục 3.5; bổ sung bảng prod/dev và giới hạn một host. | Hai sơ đồ thống nhất, đúng 5432, đúng luồng WAF, không nhận kết quả lịch sử thành kết quả mới. |
| 2 — sửa khả năng dựng lại | Thêm dependency listener→Service và bucket-policy→ALB; làm DB local-only nếu giữ tiêu chí này. | Có test nhắm đúng bất biến; fmt/validate/test và plan cho thấy thay đổi đúng phạm vi, không replacement/bật tài nguyên ngoài ý muốn. |
| 3 — sửa tích hợp proxy | Chốt trust boundary và IP resolver; kiểm thử giả mạo header, hai client, health check. | Rate limit và audit dùng đúng danh tính client trên cả chế độ được hỗ trợ. |
| 4 — cửa sổ triển khai | Hai mốc migration ACL, Cloudflare rules, origin lockdown, DNS và kiểm thử runtime. | Sáu subnet về default ACL; serving hoạt động; có bằng chứng SG/WAF mới và rollback. |
| 5 — tùy đầu ra đề tài | Hai host/HA, endpoint private, bảo vệ xóa RDS, restore test. | Có mục tiêu, ngân sách, phép đo và cơ chế tắt tiền rõ ràng. |

**Lưu ý tách phạm vi:** hai plan migration NACL hiện có guard chỉ cho phép những thay đổi đã định nghĩa. Không ghép thay đổi route DB, DNS hoặc tài nguyên mới vào hai plan đó. Chuẩn bị revision/plan riêng, xác định thứ tự và giữ nguyên trạng thái bật/tắt khi chuyển association ACL. [Runbook migration](nacl-to-sg-migration.md)

## 6. Bằng chứng cần bổ sung trước khi báo cáo “đã hoàn thành”

- Public DNS và TLS: người dùng truy cập web/API bằng tên miền thật, không chỉ `curl --resolve`.
- ECS: hai Service trỏ đúng task definition/target group; health check web/API đạt, API kết nối PostgreSQL được.
- Network: sáu subnet association đúng default ACL allow-all; route table DB đúng tiêu chí đã chốt; NAT route theo đúng chế độ một/hai NAT.
- SG: nguồn hợp lệ kết nối được; **kết nối mới** từ nguồn không được cho phép bị chặn; không SSH/public DB.
- Vận hành: pull ECR, SSM Session Manager, CloudWatch Logs và ALB log vào S3 hoạt động.
- WAF: đường dẫn nhạy cảm/CIDR demo bị chặn; phân biệt 403 từ edge/ALB và 429 từ từng lớp; truy cập ALB trực tiếp không bypass được WAF khi lockdown bật.
- IP: hai client khác nhau không dùng chung danh tính edge; header giả không đổi quota/audit identity.
- Nếu demo HA: một host/AZ không phục vụ thì host còn lại vẫn đáp ứng; ghi riêng downtime và hành vi kết nối DB sau failover.

Phiên này rà soát tĩnh và đối chiếu tài liệu chính thức, không chạy lại toàn bộ Terraform suite hoặc build ứng dụng. Subagent đã chạy lại 20 unit test công cụ migration, tất cả đạt; kết quả này chỉ kiểm tra công cụ local. Kết quả Terraform/Python trước đây được ghi trong [báo cáo SG/WAF](bao-cao-thay-doi-sg-va-waf.md), không phải kết quả chạy ngày 31/10. Các mục runtime phía trên vẫn cần thực hiện sau apply được cho phép.
