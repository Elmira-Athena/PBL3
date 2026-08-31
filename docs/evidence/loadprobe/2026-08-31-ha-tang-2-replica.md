# Bằng chứng hạ tầng đa-instance — 2 replica API + nginx round-robin ($0)

**Ngày đo:** 2026-08-31 · **Hạ tầng:** `devops/docker/docker-compose.multi.yml`
· **Chi phí:** $0, chạy hoàn toàn trên máy local.

```bash
export SA_PASSWORD=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
export JWT_SECRET="$(openssl rand -base64 48 | tr -d '\n')"
docker compose -f devops/docker/docker-compose.multi.yml up --build -d
```

Ba cổng: `8088` qua nginx (round-robin), `8081` thẳng vào replica A, `8082` thẳng vào
replica B. Hai cổng sau **bắt buộc phải có** — không có chúng thì không gửi được lệnh
vào đúng container A rồi đọc kết quả ở đúng container B, và mọi phép kiểm "xuyên
instance" mất nghĩa.

---

## 1. Round-robin có thật, không phải giả định

```
$ for i in $(seq 1 10); do curl -sD- -o /dev/null localhost:8088/health/live | grep -i x-upstream; done | sort | uniq -c
   7 X-Upstream: 172.21.0.2:8080
   3 X-Upstream: 172.21.0.3:8080
```

**Hai địa chỉ phân biệt.** Đây là lý do `nginx.multi.conf` liệt kê tường minh
`api-1:8080` và `api-2:8080` thay vì dùng `docker compose --scale api=2`: nginx phân
giải tên trong khối `upstream` **đúng một lần lúc khởi động**, nên với một service được
scale, nó gửi 100% traffic vào đúng một container suốt đời. Vòng lặp kiểm tra vẫn xanh,
log vẫn đẹp, và bài kiểm "đa instance" âm thầm trở thành bài kiểm một instance.

## 2. Cả hai container sống sót lúc khởi động ⭐

Cả `hushstore_api_1` và `hushstore_api_2` đều lên `Up` và trả `200` ở `/health/live`.
Trước đợt 1, khối seed role `Technician` ném lúc boot và **giết task**; nay khối đó đã
được bọc `try/catch` nên khởi động không còn phụ thuộc DB đang ở trạng thái nào.

## 3. Khoá tài khoản KHÔNG bị kẹt trong RAM của một task ⭐⭐

Bằng chứng đắt giá nhất trong bộ này, và nó tốn $0.

| Bước | Gửi vào | Kết quả |
|---|---|---|
| Đăng ký + đăng nhập khách | replica **A** (`:8081`) | 200, có token |
| Gọi `/api/orders/my` **trước khi khoá** | replica **B** (`:8082`) | `200 OK` — phiên sống xuyên instance |
| `DELETE /api/customers/{id}` (khoá) | replica **A** (`:8081`) | `{"success":true,"message":"Khóa tài khoản thành công."}` |
| Gọi lại `/api/orders/my` **ngay lập tức** | replica **B** (`:8082`) | **`403 Forbidden`** |

```http
HTTP/1.1 403 Forbidden
X-Account-Status: locked

{"success":false,"message":"Tài khoản của bạn đã bị khóa. Lý do: Kiem thu da instance","data":null}
```

Không có độ trễ, không phụ thuộc ALB định tuyến vào đâu. Đây chính là thứ mà việc bỏ
`MemoryCache` ở đợt 1 mua được: **hướng nguy hiểm là hướng mở khoá** — cache nói tài
khoản còn hoạt động trong khi DB đã khoá. Với 1 task đó là "chậm 30 giây"; với 2 task
đó là "lúc được lúc không tuỳ ALB", loại lỗi không tái hiện được.

> **Nửa "TRƯỚC" của cặp before/after chưa đo.** Muốn có nó phải tạm khôi phục
> `MemoryCache` rồi chạy lại đúng bốn bước trên — kỳ vọng: bước cuối trả `200`. Việc này
> thuộc đợt 6 (đo tải + báo cáo), không phải đợt 0.

## 4. RAM thật của container API

| | api-1 | api-2 |
|---|---|---|
| Nghỉ | 103,4 MiB | 87,4 MiB |
| Đỉnh khi chạy LoadProbe | **197,9 MiB** | **198,0 MiB** |

Đo với `mem_limit: 512m` / `mem_reservation: 384m`, cố ý đặt khớp taskdef ECS
(`api_memory_hard = 512`, `api_memory_reservation = 384`).

### Cảnh báo RAM migrator: con số cũ tính bằng SAI đại lượng

Ghi chú cũ nói *"api 512 + web 192 + migrator 512 = **1216 MiB** vs ~950–985 MiB khả dụng
trên t3.micro"*. Ba số đó là **`memory` (giới hạn cứng)**. ECS xếp task theo
**`memoryReservation`** khi trường này được khai — và `infra/tf/modules/ecs/taskdef.tf`
khai đủ cho cả bốn container:

| Container | `memory` | `memoryReservation` | Dòng |
|---|---|---|---|
| api | 512 | **384** | taskdef.tf:94-95 |
| web | 192 | **96** | taskdef.tf:152-153 |
| migrator | 512 | **256** | taskdef.tf:186-187 |
| seeder | 256 | **128** | taskdef.tf:235-236 |

Nhu cầu xếp chỗ thật:

- thường trực: 384 + 96 = **480 MiB**
- lúc deploy (thêm migrator): **736 MiB**
- nếu seeder chạy cùng: **864 MiB**

So với ~950–985 MiB thì **vừa**, biên khoảng 90–120 MiB ở ca xấu nhất. Nghĩa là việc
"hạ `memory` của migrator 512 → 256" **không phải điều kiện cần** để chạy 2 task.

⚠️ Vẫn còn một nửa chưa xác minh: con số **~950–985 MiB khả dụng** lấy từ ghi chú cũ,
chưa đọc `remainingResources` thật của container instance. Cần cụm ECS đang chạy:

```bash
aws ecs describe-container-instances --cluster <tên> --container-instances <arn> \
  --query 'containerInstances[0].remainingResources'
```

---

## Cách dựng lại toàn bộ

```bash
# 1. DB (đã có sẵn nếu môi trường dev đang chạy)
cd Infrastructure/db && docker-compose up -d && cd ../..

# 2. Hai replica + nginx
export SA_PASSWORD=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
export JWT_SECRET="$(openssl rand -base64 48 | tr -d '\n')"
docker compose -f devops/docker/docker-compose.multi.yml up --build -d

# 3. Đo qua nginx
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HushStoreDb;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;MultipleActiveResultSets=True"
export JwtSettings__SecretKey="$JWT_SECRET"
dotnet run --project tools/LoadProbe -- --api http://localhost:8088 --out docs/evidence/loadprobe

# 4. Dọn
docker compose -f devops/docker/docker-compose.multi.yml down
```
