#!/usr/bin/env bash
# Entry point của image hushstore-seeder. Chạy như một one-off ECS task.
#
# Biến môi trường — task definition cấp:
#   DB_HOST      endpoint RDS (env thường)
#   DB_NAME      tên database (env thường)
#   DB_USER      user (env thường)
#   DB_PASSWORD  mật khẩu — do ECS inject qua `secrets` từ SSM Parameter Store.
#                KHÔNG đọc bằng aws ssm get-parameter: role của container
#                instance bị Deny đọc /hushstore/* một cách tường minh.
#
# Vì sao truyền 4 biến rời chứ không dùng connection string: sqlcmd không nhận
# connection string kiểu .NET, nên nếu inject connection string thì entrypoint
# phải tự parse — thêm một chỗ để sai mà không đổi lại được gì.

set -euo pipefail

: "${DB_HOST:?thiếu DB_HOST}"
: "${DB_NAME:?thiếu DB_NAME}"
: "${DB_USER:?thiếu DB_USER}"
: "${DB_PASSWORD:?thiếu DB_PASSWORD — task definition phải inject qua secrets}"

echo "=================================================="
echo " HushStore — seed dữ liệu"
echo " host     : $DB_HOST"
echo " database : $DB_NAME"
echo " user     : $DB_USER"
echo "=================================================="

# ─── cờ -C và vì sao nó bị khoá ────────────────────────────────────────────
# Mặc định KHÔNG có -C: sqlcmd XÁC THỰC cert của server thay vì tin bừa — giữ
# đúng tính chất Encrypt=True;TrustServerCertificate=False mà API và migrator
# đang dùng. CA của RDS đã nằm trong trust store (xem Dockerfile).
#
# SQL Server chạy local thì dùng cert tự ký nên không xác thực được, tức không
# test được image này trên máy nếu tuyệt đối không cho -C. Nên có
# SEED_TRUST_SERVER_CERT=1 — nhưng nó bị TỪ CHỐI khi phát hiện đang chạy trong
# ECS. Nếu chỉ ghi "biến này chỉ dùng để test" thì sớm muộn có người đặt nó
# trong task definition và âm thầm tắt xác thực cert ở production.
# AWS_CONTAINER_CREDENTIALS_RELATIVE_URI do ECS agent luôn đặt cho task có
# task role, nên nó là dấu hiệu tin được.
TRUST_FLAG=()
if [ "${SEED_TRUST_SERVER_CERT:-0}" = "1" ]; then
    if [ -n "${AWS_CONTAINER_CREDENTIALS_RELATIVE_URI:-}" ]; then
        echo "LỖI: SEED_TRUST_SERVER_CERT=1 không được dùng khi chạy trong ECS." >&2
        echo "      Nó tắt việc xác thực cert của server, mà toàn hệ thống đang giữ" >&2
        echo "      Encrypt=True;TrustServerCertificate=False. Chỉ dùng cờ này khi" >&2
        echo "      test trên máy với SQL Server cert tự ký." >&2
        exit 2
    fi
    echo "CẢNH BÁO: bỏ qua xác thực cert của server (chỉ hợp lệ khi test local)."
    TRUST_FLAG=(-C)
fi
# -b : exit code khác 0 khi có lỗi SQL. Không có nó thì script "thành công" dù
#      mọi statement đều fail, và exit code là thứ duy nhất pipeline đọc được.
# -I : BẮT BUỘC. sqlcmd mặc định đặt QUOTED_IDENTIFIER OFF, còn schema do EF Core
#      sinh có filtered index nên SQL Server từ chối mọi INSERT với
#      "Msg 1934 ... SET options have incorrect settings: 'QUOTED_IDENTIFIER'".
#      Phát hiện bằng test thật trên SQL Server local, không phải suy luận — và
#      nó sẽ fail y hệt trên RDS.
# -l/-t: timeout kết nối và query. RDS vừa start có thể chậm.
run_sql() {
    sqlcmd -S "tcp:${DB_HOST},1433" -d "$DB_NAME" \
           -U "$DB_USER" -P "$DB_PASSWORD" \
           -N -b -I -l 60 -t 300 "${TRUST_FLAG[@]}" "$@"
}

echo
echo "[1/3] Kiểm tra kết nối và schema đã tồn tại chưa ..."
# Seed đòi schema phải có sẵn — migration là việc của task migrator, không phải
# của seeder. Nếu bảng chưa có thì dừng ngay với thông báo rõ, thay vì để hàng
# trăm câu INSERT fail lần lượt.
run_sql -Q "SET NOCOUNT ON; IF OBJECT_ID('dbo.AppRoles','U') IS NULL OR OBJECT_ID('dbo.Categories','U') IS NULL BEGIN RAISERROR('Schema chua ton tai: chay task migrator TRUOC khi seed.',16,1); END ELSE PRINT 'Schema OK';"

echo
echo "[2/3] Chạy các file seed ..."
# Bỏ mọi dòng `USE [...]` và dòng `GO` ngay sau nó: tên database do -d quyết
# định, không để file seed tự chọn. File dùng [HushStoreDb], connection string
# dùng HushStoreDB — SQL Server không phân biệt hoa thường ở tên database nên
# vẫn chạy, nhưng phụ thuộc vào điều đó là mong manh.
for f in /seed/*.sql; do
    echo "  --- $(basename "$f") ---"
    sed -E '/^[[:space:]]*USE[[:space:]]*\[/,+1{/^[[:space:]]*USE[[:space:]]*\[/d; /^[[:space:]]*GO[[:space:]]*$/d}' "$f" > /tmp/clean.sql
    run_sql -i /tmp/clean.sql
done

echo
echo "[3/3] Đếm lại để xác nhận seed có tác dụng thật ..."
# Đếm thay vì tin exit code: một file seed toàn IF NOT EXISTS vẫn exit 0 khi
# không insert được gì.
run_sql -Q "SET NOCOUNT ON;
SELECT 'AppRoles      = ' + CAST(COUNT(*) AS VARCHAR) FROM AppRoles;
SELECT 'AppUsers      = ' + CAST(COUNT(*) AS VARCHAR) FROM AppUsers;
SELECT 'Categories    = ' + CAST(COUNT(*) AS VARCHAR) FROM Categories;
SELECT 'Manufacturers = ' + CAST(COUNT(*) AS VARCHAR) FROM Manufacturers;
SELECT 'Products      = ' + CAST(COUNT(*) AS VARCHAR) FROM Products;
SELECT 'ProductVariants = ' + CAST(COUNT(*) AS VARCHAR) FROM ProductVariants;"

echo
echo "=================================================="
echo " Seed xong. Exit code 0."
echo "=================================================="
