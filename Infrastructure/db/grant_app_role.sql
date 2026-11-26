-- Tạo PostgreSQL role DML-only `hushstore_app` cho API runtime, thay cho master user.
-- Pentest 2026-11-23 §2.4#2 / F-breach: nếu container api bị RCE, connection string trong env
-- hiện là master → đọc/ghi/DROP toàn DB. Role này chỉ có SELECT/INSERT/UPDATE/DELETE, không DDL/DROP.
--
-- CHẠY BẰNG MASTER USER, SAU KHI migrator đã tạo xong schema. Idempotent — chạy lại không sao.
-- Thay :'app_password' bằng giá trị SSM /hushstore/prod/app-db-password, ví dụ:
--   PGPASSWORD=<master> psql "host=... sslmode=verify-full ..." \
--     -v app_password="$(aws ssm get-parameter --name /hushstore/prod/app-db-password \
--        --with-decryption --query Parameter.Value --output text)" -f grant_app_role.sql
--
-- Sau khi chạy xong và API đã được chuyển sang role này (use_dml_db_role=true), VALIDATE mọi luồng
-- ghi (checkout, upload, POS, nhập/xuất kho, phiếu dịch vụ) ở cửa sổ live trước khi coi là xong.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hushstore_app') THEN
    CREATE ROLE hushstore_app LOGIN PASSWORD :'app_password';
  ELSE
    ALTER ROLE hushstore_app LOGIN PASSWORD :'app_password';
  END IF;
END
$$;

-- KHÔNG cấp CREATE/DROP/TRUNCATE/DDL. Chỉ DML + quyền dùng sequence (IDocumentSequence nextval).
GRANT CONNECT ON DATABASE hushstore TO hushstore_app;
GRANT USAGE ON SCHEMA public TO hushstore_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO hushstore_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO hushstore_app;

-- Bảng/sequence do migrator (master) tạo Ở CÁC LẦN SAU cũng tự được cấp — nếu không có dòng này,
-- mỗi migration thêm bảng mới sẽ làm API mất quyền trên bảng đó (lỗi chỉ lộ khi chạy).
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO hushstore_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO hushstore_app;
