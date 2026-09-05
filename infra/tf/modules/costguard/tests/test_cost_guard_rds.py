"""Đo nhánh `_stop_rds` của cost guard. KHÔNG gọi AWS — thay `_rds` bằng stub.

Chạy:
    python3 -m venv /tmp/cgvenv && /tmp/cgvenv/bin/pip install boto3
    /tmp/cgvenv/bin/python infra/tf/modules/costguard/tests/test_cost_guard_rds.py

🎯 VÌ SAO CÓ FILE NÀY, TRONG KHI ĐÃ CÓ costguard.tftest.hcl.
`terraform test` kiểm được HÌNH DẠNG hạ tầng (policy ghim đúng ARN nào, biến
mặc định là gì) nhưng KHÔNG chạm được vào Python. Mà lỗi đắt nhất của module này
không nằm ở IAM — nó nằm ở một dòng `notes.append` nói sai sự thật.

🔴 LỖI ĐÓ ĐÃ CÓ THẬT, và chạy chính bộ test này trên bản trước khi vá cho:
    3/6 đạt. CA 2, CA 4, CA 5 đỏ.
Cả ba đỏ theo cùng một hướng: guard ghi `notes` "trạng thái đích vẫn đạt được"
trong khi RDS vẫn tính $0,098/giờ. `notes` chỉ vào CloudWatch, không vào email
⇒ KHÔNG AI BIẾT. Nguyên nhân: AWS dùng chung mã `InvalidDBInstanceState` cho
những tình huống trái ngược nhau — đã stopped (đích đã đạt) và đang
backing-up / modifying / có read replica (vẫn tính đủ tiền) — còn bản cũ diễn
giải nó theo đúng MỘT nghĩa, và chọn nghĩa im lặng.

Giữ file này chạy được. Sửa `_stop_rds` mà không chạy lại nó thì đang sửa mù
đúng chỗ mà "sai" và "im lặng" trùng nhau.
"""
import os, sys, importlib.util

os.environ.update(PROJECT="hushstore", CLUSTER_NAME="c", ASG_NAME="a",
                  RDS_IDENTIFIER="hushstore-db-tf", SERVICE_NAMES="api,web",
                  SNS_TOPIC_ARN="arn:aws:sns:x:1:t")

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "cost_guard.py")

def load():
    import boto3
    boto3.client = lambda *a, **k: object()
    spec = importlib.util.spec_from_file_location("cost_guard", SRC)
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
    return m

cg = load()
from botocore.exceptions import ClientError

def mk_client_error(code):
    return ClientError({"Error": {"Code": code, "Message": f"stub {code}"}}, "StopDBInstance")

class Rds:
    def __init__(self, describes, stop_exc=None):
        self._describes = list(describes); self.stop_exc = stop_exc; self.stop_calls = 0
    def describe_db_instances(self, **kw):
        d = self._describes.pop(0)
        if isinstance(d, Exception): raise d
        return {"DBInstances": [d]}
    def stop_db_instance(self, **kw):
        self.stop_calls += 1
        if self.stop_exc: raise self.stop_exc

def run(rds):
    cg._rds = rds
    a, n, f, e = [], [], [], []
    cg._stop_rds(a, n, f, e)
    return a, n, f, e

def show(ten, rds, ky_vong):
    a, n, f, e = run(rds)
    dat = ky_vong(a, n, f, e, rds)
    print(f"{'✅' if dat else '🔴'} {ten}")
    for nhan, ds in (("actions", a), ("notes", n), ("findings", f), ("errors", e)):
        for x in ds: print(f"      {nhan}: {x[:150]}")
    print(f"      stop_db_instance được gọi: {rds.stop_calls} lần")
    return dat

ket = []

ket.append(show(
    "CA 1 — available, KHÔNG replica ⇒ phải gọi stop, vào actions",
    Rds([{"DBInstanceStatus": "available", "ReadReplicaDBInstanceIdentifiers": []}]),
    lambda a, n, f, e, r: len(a) == 1 and not f and not e and r.stop_calls == 1))

ket.append(show(
    "CA 2 — available, CÓ replica ⇒ KHÔNG gọi stop, vào findings (email)",
    Rds([{"DBInstanceStatus": "available",
          "ReadReplicaDBInstanceIdentifiers": ["hushstore-db-tf-replica"]}]),
    lambda a, n, f, e, r: len(f) == 1 and "replica" in f[0] and not a and not n
                          and not e and r.stop_calls == 0))

ket.append(show(
    "CA 3 — stop bị từ chối, đọc lại thấy 'stopped' ⇒ notes (im lặng, đúng)",
    Rds([{"DBInstanceStatus": "available", "ReadReplicaDBInstanceIdentifiers": []},
         {"DBInstanceStatus": "stopped"}], stop_exc=mk_client_error("InvalidDBInstanceState")),
    lambda a, n, f, e, r: len(n) == 1 and not f and not e))

ket.append(show(
    "CA 4 — stop bị từ chối, đọc lại thấy 'backing-up' ⇒ findings, KHÔNG im lặng",
    Rds([{"DBInstanceStatus": "available", "ReadReplicaDBInstanceIdentifiers": []},
         {"DBInstanceStatus": "backing-up"}], stop_exc=mk_client_error("InvalidDBInstanceState")),
    lambda a, n, f, e, r: len(f) == 1 and "backing-up" in f[0] and not n))

ket.append(show(
    "CA 5 — stop bị từ chối, describe lần 2 cũng lỗi ⇒ errors, KHÔNG mặc định là ổn",
    Rds([{"DBInstanceStatus": "available", "ReadReplicaDBInstanceIdentifiers": []},
         mk_client_error("Throttling")], stop_exc=mk_client_error("InvalidDBInstanceState")),
    lambda a, n, f, e, r: len(e) == 1 and not n and not f))

ket.append(show(
    "CA 6 — DBInstanceNotFound ⇒ notes (thật sự không còn tính tiền)",
    Rds([{"DBInstanceStatus": "available", "ReadReplicaDBInstanceIdentifiers": []}],
        stop_exc=mk_client_error("DBInstanceNotFound")),
    lambda a, n, f, e, r: len(n) == 1 and not f and not e))

print()
print(f"Tổng kết: {sum(ket)}/{len(ket)} đạt")
sys.exit(0 if all(ket) else 1)
