#!/bin/bash
# Creates the 16 Build PC categories via the HushStore API
# Usage: bash scripts/create-build-pc-categories.sh
# Requires: curl, python3 (both pre-installed on macOS)

set -e

API="https://localhost:7010"

echo ">> Đăng nhập..."
LOGIN_RESP=$(curl -sk -X POST "$API/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@hushstore.com","password":"Admin@123"}')

TOKEN=$(echo "$LOGIN_RESP" | python3 -c "import sys,json; r=json.load(sys.stdin); print(r['data']['accessToken'])")

if [ -z "$TOKEN" ]; then
  echo "Lỗi: Không lấy được access token. Kiểm tra lại thông tin đăng nhập và API đang chạy."
  exit 1
fi

echo ">> Đăng nhập thành công."
echo ""

create_category() {
  local NAME="$1"
  local SLUG="$2"
  local SORT="$3"

  RESP=$(curl -sk -X POST "$API/api/categories" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d "{\"name\":\"$NAME\",\"slug\":\"$SLUG\",\"sortOrder\":$SORT,\"isVisible\":true}")

  SUCCESS=$(echo "$RESP" | python3 -c "import sys,json; r=json.load(sys.stdin); print(r.get('success','false'))" 2>/dev/null)
  if [ "$SUCCESS" = "True" ] || [ "$SUCCESS" = "true" ]; then
    echo "  [OK] $NAME ($SLUG)"
  else
    MSG=$(echo "$RESP" | python3 -c "import sys,json; r=json.load(sys.stdin); print(r.get('message','unknown error'))" 2>/dev/null)
    echo "  [SKIP/ERR] $NAME ($SLUG) — $MSG"
  fi
}

echo ">> Tạo danh mục linh kiện PC..."
create_category "Bộ vi xử lý"        "cpu"                  1
create_category "Bo mạch chủ"         "bo-mach-chu"          2
create_category "RAM"                  "ram"                  3
create_category "HDD"                  "hdd"                  4
create_category "SSD"                  "ssd"                  5
create_category "VGA"                  "vga"                  6
create_category "Nguồn"                "nguon"                7
create_category "Vỏ Case"              "vo-case"              8
create_category "Fan Case"             "fan-case"             9
create_category "Màn hình"             "man-hinh"             10
create_category "Chuột"                "chuot"                11
create_category "Bàn phím"             "ban-phim"             12
create_category "Tản nhiệt khí"        "tan-nhiet-khi"        13
create_category "Tản nhiệt nước AIO"   "tan-nhiet-nuoc-aio"   14
create_category "Tai nghe"             "tai-nghe"             15
create_category "Phần mềm"             "phan-mem"             16

echo ""
echo ">> Hoàn tất!"
