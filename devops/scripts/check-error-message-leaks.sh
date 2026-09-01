#!/usr/bin/env bash
# Chốt chống hồi quy cho mục 🅴 / 🅷 / 🅸: không thông báo lỗi nào chở `ex.Message`
# của HẠ TẦNG ra cho người dùng.
#
# VÌ SAO KHÔNG DÙNG `grep 'ex.Message'`: grep không biết dòng đó nằm trong khối
# `catch` NÀO. Relay `ex.Message` từ `catch (BusinessRuleException)` hay
# `catch (ConcurrentModificationException)` là ĐÚNG — thông báo đã soạn cho người
# dùng. Relay từ `catch (Exception)` là RÒ RỈ. Bản trước của mục 🅷 đếm bằng grep
# và phóng đại 7 chỗ thành... thật ra chỉ 2. Script này phân loại theo ngữ cảnh.
#
# Dùng:
#   check-error-message-leaks.sh              # quét cả 3 tầng
#   check-error-message-leaks.sh server       # chỉ Service + API  (mục 🅴/🅷 — kỳ vọng SẠCH)
#   check-error-message-leaks.sh client       # chỉ Client         (mục 🅸 — CHƯA sửa, còn 124)
#
# Mã thoát:  0 sạch · 1 có rò rỉ · 2 KHÔNG KẾT LUẬN (không quét được file nào)
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 2

python3 - "$@" <<'PY'
import re, sys, glob

BUSINESS = {'ConcurrentModificationException', 'BusinessRuleException',
            'UnauthorizedAccessException', 'ArgumentException', 'KeyNotFoundException'}

ALL = [('Service', 'src/Service/**/*.cs'),
       ('API',     'src/API/**/*.cs'),
       ('Client',  'src/Client/**/*.cs')]

scope = (sys.argv[1] if len(sys.argv) > 1 else 'all').lower()
if scope == 'server':
    LAYERS = [l for l in ALL if l[0] != 'Client']
elif scope == 'client':
    LAYERS = [l for l in ALL if l[0] == 'Client']
elif scope == 'all':
    LAYERS = ALL
else:
    print(f"Tham số không hợp lệ: {scope!r}. Dùng: server | client | all")
    sys.exit(2)

total_files = 0
leaks = []
for label, pat in LAYERS:
    for f in sorted(glob.glob(pat, recursive=True)):
        if '/bin/' in f or '/obj/' in f:
            continue
        total_files += 1
        cur = None
        for i, ln in enumerate(open(f, encoding='utf-8').read().split('\n'), 1):
            m = re.search(r'catch \((\w+)', ln)
            if m:
                cur = m.group(1)
            if 'ex.Message' not in ln:
                continue
            st = ln.strip()
            if st.startswith('//') or st.startswith('*') or '_logger' in ln:
                continue          # comment, hoặc đi vào log — đều được phép

            # NGỮ CẢNH THỨ HAI: nhánh switch-expression khớp KIỂU, không phải khối catch.
            #   ConcurrentModificationException ex => ex.Message,
            # Chốt này vốn chỉ biết phân loại theo `catch (...)` bọc ngoài, nên nó đọc
            # nhánh trên thành "catch(None)" và báo rò rỉ — SAI, vì kiểu ở đây nằm ngay
            # trong BUSINESS và message đã là câu tiếng Việt soạn cho người dùng.
            # Phát hiện khi thêm ConflictExceptionHandler (mục 🅶): ánh xạ exception sang
            # 409 tự nhiên viết bằng switch expression chứ không bằng try/catch.
            #
            # ⚠️ Không nới rộng thành "bỏ qua mọi nhánh `=>`": kiểu nghiệp vụ BẮT BUỘC
            # phải xuất hiện TRÊN CÙNG MỘT DÒNG với ex.Message. Nhờ vậy không mở được
            # lỗ hổng — muốn qua chốt thì phải viết tường minh kiểu mình đang relay.
            arm = re.search(r'(\w+Exception)\s+\w+\s*=>', ln)
            if arm and arm.group(1) in BUSINESS:
                continue

            if cur not in BUSINESS:
                leaks.append((label, f, i, cur, st[:90]))

# FAIL-CLOSED: quét rỗng nghĩa là chốt hỏng, không phải "sạch".
# Cùng lý lẽ với hạng KHÔNG KẾT LUẬN của LoadProbe và bẫy #11.
if total_files == 0:
    print('KHÔNG KẾT LUẬN: không quét được file .cs nào — chốt này đang hỏng, KHÔNG phải sạch.')
    sys.exit(2)

if not leaks:
    print(f'Sạch [{scope}]: {total_files} file, 0 chỗ chở ex.Message của hạ tầng ra cho người dùng.')
    sys.exit(0)

print(f'RÒ RỈ [{scope}]: {len(leaks)} chỗ đưa ex.Message của HẠ TẦNG cho người dùng\n')
for label, f, i, cur, st in leaks:
    print(f'  [{label}] {f}:{i}  trong catch({cur})')
    print(f'      {st}')
print('\nSửa theo khuôn ở mục 🅴/🅷 của docs/bat-dau-phien-moi.md:')
print('  catch (BusinessRuleException ex) { return Fail(ex.Message); }   // nghiệp vụ: nguyên văn')
print('  catch (Exception ex) { _logger.LogError(ex, ...); return Fail("<câu tiếng Việt cố định>"); }')
sys.exit(1)
PY
