#!/usr/bin/env bash
# =============================================================
# HushStore — cổng chặn lỗ hổng NuGet
#
# Chạy:  bash devops/scripts/check-vulnerable-packages.sh [đường/dẫn.sln]
# Mã thoát:  0 sạch · 1 có lỗ hổng High/Critical · 2 KHÔNG KẾT LUẬN
#
# ─── VÌ SAO KHÔNG VIẾT THẲNG `dotnet list package` VÀO ci.yml ────────────────
# Vì lệnh đó TRẢ VỀ 0 KỂ CẢ KHI TÌM THẤY LỖ HỔNG. Đã đo trên chính repo này
# lúc còn 10 advisory High đang mở:
#
#     dotnet list package --vulnerable --include-transitive ; echo $?
#     → in ra đủ 10 advisory, rồi in ra: 0
#
# Nghĩa là một bước CI viết dạng `run: dotnet list package --vulnerable` sẽ
# LUÔN XANH, vĩnh viễn, bất kể có bao nhiêu lỗ hổng. Đó đúng là loại "bằng
# chứng an toàn giả" mà §5 của docs/bat-dau-phien-moi.md xếp vào bẫy im lặng:
# không lỗi, không cảnh báo, và chỉ lộ ra khi có người đi đo.
#
# Nên cổng này PHẢI đọc nội dung báo cáo, không được tin mã thoát.
#
# ─── VÌ SAO CÓ HẠNG "KHÔNG KẾT LUẬN" (mã 2) ─────────────────────────────────
# Cùng một lý lẽ với hạng KHÔNG KẾT LUẬN của tools/LoadProbe: một phép đo
# RỖNG thoả mọi điều kiện. Nếu `restore` hỏng vì mất mạng, hoặc NuGet đổi
# schema JSON, thì "không tìm thấy lỗ hổng nào" là kết luận SAI chứ không
# phải kết quả tốt — code cần quét chưa từng được quét.
#
# Vì thế script fail-closed: JSON không phân giải được, không có project nào,
# hay không có nguồn NuGet nào → thoát 2, KHÔNG thoát 0.
#
# ─── NGƯỠNG ─────────────────────────────────────────────────────────────────
# Chặn High + Critical. Low/Moderate chỉ in ra để biết, không chặn — dựng
# ngưỡng quá thấp thì mọi PR đều đỏ vì thứ không liên quan tới nó, và một cửa
# kiểm lúc nào cũng đỏ thì người ta học cách bỏ qua nó.
# =============================================================
set -uo pipefail

SLN="${1:-PBL3.sln}"
REPO_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$REPO_DIR"

if [[ ! -f "$SLN" ]]; then
  echo "::error::Không tìm thấy '$SLN' trong $REPO_DIR."
  exit 2
fi

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Quét lỗ hổng NuGet — $SLN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# `restore` phải chạy trước và phải được kiểm riêng. `dotnet list package` trên
# một solution chưa restore vẫn in ra JSON hợp lệ với danh sách project RỖNG
# phần frameworks — trông hệt như "sạch".
if ! dotnet restore "$SLN" --nologo; then
  echo "::error::restore thất bại — không quét được. Đây là KHÔNG KẾT LUẬN, không phải sạch."
  exit 2
fi

REPORT="$(dotnet list "$SLN" package --vulnerable --include-transitive --format json 2>/dev/null)"

python3 - "$REPORT" <<'PY_EOF'
import json, sys

BLOCKING = {"high", "critical"}

try:
    data = json.loads(sys.argv[1])
except Exception as e:
    print(f"::error::Không phân giải được JSON của dotnet list package ({e}).")
    print("Đây là KHÔNG KẾT LUẬN, không phải sạch.")
    sys.exit(2)

projects = data.get("projects")
if not projects:
    print("::error::Báo cáo không có project nào. Sai đường dẫn solution, hay restore rỗng?")
    print("Đây là KHÔNG KẾT LUẬN, không phải sạch.")
    sys.exit(2)

if not data.get("sources"):
    print("::error::Báo cáo không liệt kê nguồn NuGet nào — không có gì để đối chiếu advisory.")
    print("Đây là KHÔNG KẾT LUẬN, không phải sạch.")
    sys.exit(2)

blocking, informational = [], []

for proj in projects:
    name = proj.get("path", "?").split("/")[-1]
    for fw in proj.get("frameworks") or []:
        # Gói transitive cũng phải quét: 2 trong 3 lỗ hổng High của đợt vá đầu
        # tiên (Microsoft.OpenApi, System.Security.Cryptography.Xml) KHÔNG nằm
        # ở topLevelPackages. Bỏ transitivePackages là bỏ sót đa số.
        for kind in ("topLevelPackages", "transitivePackages"):
            for pkg in fw.get(kind) or []:
                for v in pkg.get("vulnerabilities") or []:
                    sev = (v.get("severity") or "?").strip()
                    row = (sev, name, pkg.get("id"), pkg.get("resolvedVersion"),
                           v.get("advisoryurl"), "transitive" if kind.startswith("trans") else "trực tiếp")
                    (blocking if sev.lower() in BLOCKING else informational).append(row)

def show(rows, title):
    print(f"\n{title} ({len(rows)}):")
    for sev, proj, pid, ver, url, kind in sorted(rows):
        print(f"  [{sev:<8}] {proj:<22} {pid} {ver}  ({kind})")
        print(f"             {url}")

if informational:
    show(informational, "Mức thấp — chỉ để biết, KHÔNG chặn")

if blocking:
    show(blocking, "CHẶN — mức High/Critical")
    print("\n::error::Có %d lỗ hổng mức High/Critical. Cách vá:" % len(blocking))
    print("  • gói TRỰC TIẾP  → nâng Version trong .csproj")
    print("  • gói TRANSITIVE → thêm PackageReference GHIM thẳng vào .csproj của")
    print("    project đó, kèm comment nói rõ vì sao có nó và khi nào xoá được")
    print("    (xem src/Service/Service.csproj để lấy mẫu).")
    print("  • Chọn bản vá NHỎ NHẤT đóng được HẾT advisory của gói đó — một gói có")
    print("    thể dính nhiều advisory với ngưỡng vá KHÁC NHAU; lấy ngưỡng muộn nhất.")
    sys.exit(1)

print(f"\nSạch: {len(projects)} project, 0 lỗ hổng High/Critical.")
sys.exit(0)
PY_EOF
