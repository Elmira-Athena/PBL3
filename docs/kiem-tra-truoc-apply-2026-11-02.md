# Kiểm tra SG/WAF trước apply — 02/11/2026

**Bản ghi tại điểm dừng trước apply, được giữ nguyên để đối chiếu.** Sau khi có
yêu cầu triển khai tiếp, AWS đã hoàn tất hai mốc; xem
[biên bản apply](bien-ban-apply-sg-2026-11-02.md). WAF vẫn chưa apply.

**Điểm dừng:** đã sửa mã và chuẩn bị plan; chưa apply, kể cả refresh-only.
Chưa init/migrate backend Cloudflare, import ruleset, deploy image hoặc thay đổi DNS.

## Các sửa đổi trong đợt này

- `hs_nacl_migration_check` kiểm tra state trước khi up/down thay đổi AWS hoặc
  tfvars; `hs_apply` kiểm tra lại. Còn bất kỳ custom ACL hoặc đọc state lỗi đều
  làm dừng. Lệnh Terraform gõ tay vẫn phải tuân theo runbook.
- API giữ một hop: direct ALB dùng XFF; origin lockdown dùng một IP hợp lệ trong
  CF-Connecting-IP. Header thiếu/sai/lặp bị 400 ngoài các GET/HEAD health path
  đã định nghĩa. Không tăng hạn mức và không đưa origin secret vào task env.
- Root truyền cờ lockdown hiện có sang ECS. Khi false không thêm env mới, giữ
  nguyên hình dạng task definition trong hai mốc NACL. Khi true chỉ API có env
  `ProxyHeaders__CloudflareOriginLockdown=true`.
- Preflight có kiểm tra CNAME tới ALB dự kiến và kiểm tra transform đã triển khai
  theo secret/scope/scheme/thứ tự; không in secret, không gọi API ghi.
- Backend WAF khai báo S3 mã hóa, locking và key `cloudflare/terraform.tfstate`
  riêng trong bucket state hiện có; không thêm tài nguyên AWS. Người được quyền
  đọc state/listener/transform vẫn có thể đọc secret.
- CI thêm probe middleware độc lập với NuGet package ứng dụng và test guard shell.
  Runbook có bước chuyển API revision rõ ràng vì Service bỏ qua task-definition
  diff của Terraform; rollback app về direct mode trước khi mở lại origin.

## Kiểm tra đã chạy

| Kiểm tra | Kết quả |
|---|---|
| Terraform AWS, tám module, tất cả run dùng `command = plan` | **117 đạt, 0 lỗi** |
| Terraform Cloudflare mock plan | **14 đạt, 0 lỗi** |
| Python migration guard | **20 đạt** |
| Python WAF preflight | **24 đạt** |
| Python shell guard, dùng executable giả | **5 đạt** |
| Middleware probe dùng ASP.NET Core thật, không network | **26 tình huống đạt** |
| Build API Release, restore package hiện có, không đổi version | **0 lỗi, 32 cảnh báo** NuGet/nullable |
| Terraform validate | Prod/dev và root Cloudflare đạt; prod còn cảnh báo hai biến NACL cũ trong tfvars local |
| Provider lock Cloudflare | Registry xác minh macOS ARM64 + Linux AMD64; checksum đã có, không cần đổi lockfile |
| Format | 70 file HCL được quản lý và ba tfvars example đạt; không sửa tfvars local bị ignore |
| Shell / symlink / thông báo lỗi | Cú pháp shell đạt, symlink dev giữ nguyên; cổng rò lỗi 256 file, 0 chỗ |

Không kết luận CI GitHub xanh: workflow chưa chạy trên bản sửa này và các cảnh
báo NuGet không được bỏ qua hoặc vá trong đợt mạng này. Các kết quả trên không
chứng minh WAF đang chặn request thật hoặc SG đã cập nhật.

## Live plan và lý do dừng

Đã xác minh danh tính account đích, đọc state/AWS và tạo plan mốc 1 trong cây
migration riêng. Default NACL thực tế đạt allow-all IPv4 hai chiều. RDS quan sát
đang `stopped`; phiên này không bật hoặc tắt tài nguyên.

Plan đầu có bốn cập nhật: ba ACL tháo association và một Launch Template do
nguồn AMI SSM đã đổi. Có ba drift entry: metadata `status`/`latest_restorable_time`
của RDS và thuộc tính route quan sát của hai app route table.

Đã chuẩn bị override AMI **chỉ trong hai cây tạm**, giữ cả giá trị đang triển khai
và metadata `sensitive` của SSM. Cấu hình AMI của repo không đổi. Plan mới chỉ còn
**ba cập nhật ACL**, không create/delete/replace tài nguyên. Guard vẫn từ chối
vì drift; không xóa drift khỏi JSON, không tắt refresh hoặc nới guard để cho qua.

Đã tạo riêng **refresh-only plan: 0 thay đổi tài nguyên**, để người vận hành review
việc đồng bộ state với ba drift entry. Apply refresh-only vẫn là thao tác ghi
state và **chưa được thực hiện**. Cần duyệt bước này riêng, rồi tạo lại live plan
mốc 1 có refresh và chạy guard. Không dùng lại saved plan cũ sau khi state đổi.

Artifacts chứa state/secret nằm trong thư mục local quyền 0700:
`/private/tmp/hushstore-preapply-20261102-f85_vra5/`. File plan/JSON được tạo dưới
`umask 077`; không đưa vào Git hoặc gửi kèm báo cáo. Thư mục tạm không phải hồ sơ
lưu trữ lâu dài; khi mất thư mục phải chuẩn bị và review lại.

Các file cần review tại máy này:

- `refresh-only.tfplan` và `refresh-only.json`: chỉ đồng bộ state, chưa apply.
- `stage1-frozen.tfplan` và `stage1-frozen.json`: ba cập nhật ACL, **guard đang chặn** drift.
- `stage1/`, `stage2/`: source snapshot và override AMI tạm; không dùng như backend mới.
- `acls.json`, `subnets.json`: snapshot association/default ACL tại lần đọc.

Mốc 2 chưa có live plan vì cần state sau mốc 1 và bằng chứng cả sáu subnet đã
gắn default ACL. Chưa chạy preflight/plan Cloudflare có xác thực; cần chuẩn bị
zone/token/quyền backend ngoài repo trong phiên triển khai.

## Trình tự tiếp theo, chỉ sau khi được cho phép

1. Review refresh-only plan/state drift; nếu được duyệt mới apply và tạo lại
   plan mốc 1 từ nguồn đã review, giữ AMI và trạng thái tài nguyên.
2. Chạy guard, review/apply mốc 1; kiểm tra cả sáu association thực tế.
3. Tạo/review/apply mốc 2; sau đó script up/down mới được phép chạy bình thường.
4. Theo [runbook WAF](waf-cloudflare.md): image API đã sửa, backend bảo vệ,
   preflight và ruleset adoption, edge rules, origin lockdown, API revision.
5. Đo lại public DNS/web/API, PostgreSQL/ECR/SSM/logs, SG negative tests, IP giả
   và bypass WAF; lưu bằng chứng runtime mới.

[Migration hai mốc](nacl-to-sg-migration.md) vẫn là quy trình bắt buộc. Báo cáo và
`docs/evidence/` cũ được giữ nguyên. Dependency ALB, route DB local-only và các
đề xuất HA/RDS trong bản rà soát trước là phạm vi riêng, chưa đổi trong đợt này.
