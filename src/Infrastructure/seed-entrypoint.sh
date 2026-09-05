#!/usr/bin/env bash
# Entry point của image hushstore-seeder. Chạy như một one-off ECS task.
#
# Biến môi trường — task definition cấp ĐÚNG BA biến thường:
#   DB_HOST      endpoint RDS
#   DB_NAME      tên database
#   DB_USER      user
#   DB_PASSWORD  mật khẩu — do ECS inject qua `secrets` từ SSM Parameter Store.
#                KHÔNG đọc bằng aws ssm get-parameter: role của container
#                instance bị Deny đọc /hushstore/* một cách tường minh.
#
# ⚠️ ĐÚNG BA, không phải "ít nhất ba": ecs/tests/taskdef.tftest.hcl assert
# `toset([...]) == toset(["DB_HOST","DB_NAME","DB_USER"])`, và lý do nêu ngay
# trong error_message — thêm biến thứ tư vào `environment` là mở một lối cho mật
# khẩu lọt ra ngoài khối `secrets`. Vì vậy mọi cấu hình PostgreSQL (cổng, sslmode,
# đường dẫn CA) đặt Ở ĐÂY và trong Dockerfile, KHÔNG đẩy ngược lên task definition.
#
# Vì sao truyền 4 biến rời chứ không dùng connection string: psql không nhận
# connection string kiểu .NET, nên nếu inject connection string thì entrypoint
# phải tự parse — thêm một chỗ để sai mà không đổi lại được gì.

set -euo pipefail

: "${DB_HOST:?thiếu DB_HOST}"
: "${DB_NAME:?thiếu DB_NAME}"
: "${DB_USER:?thiếu DB_USER}"
: "${DB_PASSWORD:?thiếu DB_PASSWORD — task definition phải inject qua secrets}"

DB_PORT="${DB_PORT:-5432}"

echo "=================================================="
echo " HushStore — seed dữ liệu (PostgreSQL)"
echo " host     : $DB_HOST:$DB_PORT"
echo " database : $DB_NAME"
echo " user     : $DB_USER"
echo "=================================================="

# ─── sslmode và vì sao đường hạ nó bị khoá ─────────────────────────────────
# Mặc định verify-full: psql vừa mã hoá, vừa XÁC THỰC cert của server, vừa kiểm
# hostname — tương đương `SSL Mode=VerifyFull` mà chuỗi kết nối của API và
# migrator đang giữ. CA của RDS được nạp trong Dockerfile.
#
# 🚨 Đừng hạ xuống `require` để "cho nhanh": `require` MÃ HOÁ NHƯNG KHÔNG XÁC
# THỰC gì cả — nó không phòng được man-in-the-middle, tức mất đúng thuộc tính
# đang được bảo vệ, và mất IM LẶNG (kết nối vẫn xanh, log vẫn sạch).
#
# PostgreSQL chạy trong docker-compose local KHÔNG bật TLS, nên không test được
# image này trên máy nếu tuyệt đối không cho hạ. Vì vậy có SEED_TRUST_SERVER_CERT=1
# — nhưng nó bị TỪ CHỐI khi phát hiện đang chạy trong ECS. Nếu chỉ ghi "biến này
# chỉ dùng để test" thì sớm muộn có người đặt nó trong task definition và âm thầm
# tắt xác thực ở production. AWS_CONTAINER_CREDENTIALS_RELATIVE_URI do ECS agent
# luôn đặt cho task có task role, nên nó là dấu hiệu tin được.
# (Giữ nguyên tên biến của bản SQL Server để khỏi phải sửa tài liệu vận hành.)
export PGSSLMODE="${PGSSLMODE:-verify-full}"
export PGSSLROOTCERT="${PGSSLROOTCERT:-/usr/local/share/ca-certificates/rds-ap-southeast-1.crt}"

if [ "${SEED_TRUST_SERVER_CERT:-0}" = "1" ]; then
    if [ -n "${AWS_CONTAINER_CREDENTIALS_RELATIVE_URI:-}" ]; then
        echo "LỖI: SEED_TRUST_SERVER_CERT=1 không được dùng khi chạy trong ECS." >&2
        echo "      Nó tắt việc xác thực cert của server, mà toàn hệ thống đang giữ" >&2
        echo "      SSL Mode=VerifyFull. Chỉ dùng cờ này khi test trên máy với" >&2
        echo "      PostgreSQL local không bật TLS." >&2
        exit 2
    fi
    echo "CẢNH BÁO: bỏ qua xác thực cert của server (chỉ hợp lệ khi test local)."
    export PGSSLMODE=prefer
    unset PGSSLROOTCERT
fi

export PGPASSWORD="$DB_PASSWORD"
export PGCONNECT_TIMEOUT="${PGCONNECT_TIMEOUT:-60}"
# Trần thời gian cho MỘT câu lệnh. RDS vừa start có thể chậm; 300s là trần cũ của
# sqlcmd -t. Đi qua PGOPTIONS nên không phải sửa từng file seed.
export PGOPTIONS="${PGOPTIONS:--c statement_timeout=300000}"

# -v ON_ERROR_STOP=1 : BẮT BUỘC — vai trò y hệt `sqlcmd -b`. Không có nó thì psql
#      chạy tiếp sau mỗi lỗi và exit 0, tức script "thành công" dù mọi statement
#      đều fail, mà exit code là thứ duy nhất pipeline đọc được.
#      🚨 Đây KHÔNG phải cảnh báo lý thuyết: đúng lúc chuyển seed_product_data.sql
#      sang PL/pgSQL, một câu PRINT chưa dịch đứng TRƯỚC 4 lệnh setval. Có cờ này
#      thì script dừng ồn ào; không có nó thì seed báo thành công trong khi
#      sequence vẫn đứng ở 1 — và lỗi chỉ lộ ra ở lần đầu admin tạo sản phẩm mới.
# -X : không đọc ~/.psqlrc, để hành vi không phụ thuộc môi trường build.
# KHÔNG còn tương đương của `sqlcmd -I` (QUOTED_IDENTIFIER): đó là khái niệm riêng
# SQL Server. PostgreSQL luôn coi "..." là định danh — chính vì vậy các file seed
# phải nháy kép mọi tên bảng/cột, nếu không PG hạ chữ thường và không thấy bảng.
run_sql() {
    psql -h "$DB_HOST" -p "$DB_PORT" -d "$DB_NAME" -U "$DB_USER" \
         -X -v ON_ERROR_STOP=1 "$@"
}

echo
echo "[1/3] Kiểm tra kết nối và schema đã tồn tại chưa ..."
# Seed đòi schema phải có sẵn — migration là việc của task migrator, không phải
# của seeder. Nếu bảng chưa có thì dừng ngay với thông báo rõ, thay vì để hàng
# trăm câu INSERT fail lần lượt.
# to_regclass trả NULL khi không có bảng (thay vì ném), nên kiểm được bằng IF.
run_sql -c "DO \$\$
BEGIN
    IF to_regclass('\"AppRoles\"') IS NULL OR to_regclass('\"Categories\"') IS NULL THEN
        RAISE EXCEPTION 'Schema chua ton tai: chay task migrator TRUOC khi seed.';
    END IF;
    RAISE NOTICE 'Schema OK';
END \$\$;"

echo
echo "[2/3] Chạy các file seed ..."
# Không còn khâu `sed` bỏ `USE [...]`/`GO`: psql không có batch separator, và tên
# database do -d quyết định. Các file seed đã được viết lại dạng PL/pgSQL.
for f in /seed/*.sql; do
    echo "  --- $(basename "$f") ---"
    run_sql -f "$f"
done

echo
echo "[3/3] Đếm lại để xác nhận seed có tác dụng thật ..."
# Đếm thay vì tin exit code: một file seed toàn ON CONFLICT DO NOTHING vẫn exit 0
# khi không insert được gì.
run_sql -c "SELECT 'AppRoles        = ' || COUNT(*) FROM \"AppRoles\"
  UNION ALL SELECT 'AppUsers        = ' || COUNT(*) FROM \"AppUsers\"
  UNION ALL SELECT 'Categories      = ' || COUNT(*) FROM \"Categories\"
  UNION ALL SELECT 'Manufacturers   = ' || COUNT(*) FROM \"Manufacturers\"
  UNION ALL SELECT 'Products        = ' || COUNT(*) FROM \"Products\"
  UNION ALL SELECT 'ProductVariants = ' || COUNT(*) FROM \"ProductVariants\";"

# Sequence của 4 bảng identity phải ĐI QUA số dòng đã seed. Seed chèn Id tường
# minh nên sequence không tự nhích — thiếu `setval` thì mọi thứ vẫn xanh và chỉ
# đâm khoá chính ở LẦN ĐẦU ADMIN TẠO BẢN GHI MỚI, tức là rất muộn.
#
# ⚠️ Chốt này bắt được cái gì và KHÔNG bắt được cái gì — đã đo, đừng hiểu nhầm
# phạm vi của nó:
#   · BẮT ĐƯỢC: ai đó xoá/làm hỏng 4 lệnh setval ở cuối seed_product_data.sql.
#     Đo bằng cách ép sequence về 1 rồi chạy riêng khối này -> exit 3, thông báo
#     nêu đúng tên bảng và hai con số.
#   · KHÔNG BẮT ĐƯỢC: một DB có sequence lệch TỪ TRƯỚC. Vì bước [2/3] chạy seed,
#     mà seed có setval, nên tới đây sequence đã được sửa rồi. (Đã thử: ép về 1
#     rồi chạy CẢ seeder -> exit 0, vì seed tự vá.) Đó là hành vi đúng, không
#     phải lỗ hổng — nhưng nếu tưởng chốt này canh chuyện đó thì sẽ tin nhầm.
echo
echo "[3b/3] Kiểm sequence đã vượt qua dữ liệu seed chưa ..."
run_sql -c "DO \$\$
DECLARE t text; seq text; last_id bigint; next_id bigint;
BEGIN
    FOREACH t IN ARRAY ARRAY['Manufacturers','Categories','Products','ProductVariants'] LOOP
        seq := pg_get_serial_sequence(format('%I', t), 'Id');
        IF seq IS NULL THEN
            RAISE EXCEPTION 'Khong tim thay sequence cua bang %', t;
        END IF;
        EXECUTE format('SELECT coalesce(max(\"Id\"), 0) FROM %I', t) INTO last_id;
        -- seq do pg_get_serial_sequence tra ve da duoc quote san, doc thang tu no.
        EXECUTE format('SELECT last_value + CASE WHEN is_called THEN 1 ELSE 0 END FROM %s', seq)
            INTO next_id;
        IF next_id <= last_id THEN
            RAISE EXCEPTION 'SEED THAT BAI: sequence cua % dang o % nhung max(Id) = % — thieu setval.', t, next_id, last_id;
        END IF;
        RAISE NOTICE '% : max(Id)=%, next=% OK', t, last_id, next_id;
    END LOOP;
END \$\$;"

echo
echo "=================================================="
echo " Seed xong. Exit code 0."
echo "=================================================="
