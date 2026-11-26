# Runbook — bật PostgreSQL role DML-only cho API (pentest F-breach / §2.4#2)

**Vấn đề:** API runtime kết nối RDS bằng **master user** (`data/main.tf`). Nếu container `api` bị RCE,
connection string trong env cho phép đọc/ghi/**DROP** toàn DB (đã chứng minh ở GĐ4.1, `p4-01`).

**Giải pháp:** role `hushstore_app` chỉ có `SELECT/INSERT/UPDATE/DELETE` + dùng sequence. Migrator giữ
master (cần DDL). Thay đổi này **ship ở trạng thái TẮT** (`use_dml_db_role = false`) — apply không đổi
hành vi — và chỉ bật sau khi tạo role + validate ở cửa sổ live.

## Đã có sẵn trong code
- `random_password.app_db` + SSM `/hushstore/prod/app-db-password` (luôn tạo).
- `var.use_dml_db_role` (mặc định `false`): khi `true`, cả hai connection string đổi sang `hushstore_app`.
- SQL idempotent: `Infrastructure/db/grant_app_role.sql`.

## Các bước bật (trong một cửa sổ `up.sh` có người canh)
1. `up.sh` như bình thường (migrator đã tạo schema bằng master).
2. Lấy mật khẩu role và tạo role trong DB (chạy bằng master, từ máy trong VPC hoặc qua ECS Exec):
   ```sh
   APP_PW=$(aws ssm get-parameter --name /hushstore/prod/app-db-password \
     --with-decryption --query Parameter.Value --output text)
   psql "host=<rds> dbname=hushstore user=<master> sslmode=verify-full \
     sslrootcert=<ca>" -v app_password="$APP_PW" -f Infrastructure/db/grant_app_role.sql
   ```
3. Bật cờ: `terraform apply -var='use_dml_db_role=true'` (hoặc đặt trong tfvars). ECS lấy connection
   string mới từ SSM khi task khởi động lại.
4. **VALIDATE bắt buộc** trước khi coi là xong — chạy tay mọi luồng GHI để chắc không thiếu GRANT nào:
   đăng ký/đăng nhập, checkout, upload ảnh, POS, nhập kho, xuất kho, phiếu dịch vụ. Lỗi thiếu quyền
   biểu hiện là 500 ở đúng luồng đó. Nếu thiếu, bổ sung GRANT vào `grant_app_role.sql` rồi chạy lại.
5. Nếu hỏng: `apply -var='use_dml_db_role=false'` để quay về master ngay (role vẫn tồn tại, vô hại).

## Vì sao chưa bật sẵn
Không validate được khi RDS đang tắt. Một GRANT thiếu (ví dụ quyền trên bảng migration thêm sau, hay
sequence mới) chỉ lộ lúc chạy thật → bật mặc định là rủi ro hồi quy API. `ALTER DEFAULT PRIVILEGES`
trong SQL đã xử lý bảng/sequence tạo ở các migration sau, nhưng vẫn cần một lần đo tay xác nhận.
