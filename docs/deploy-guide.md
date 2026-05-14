# HushStore — Quy trình Deploy & CI/CD

> **Hạ tầng:** EC2 t3.micro (Singapore) + RDS SQL Server Express
> **Domain:** `hushstore.io.vn` (Frontend) · `api.hushstore.io.vn` (API)
> **SSH key:** `~/.ssh/hushstore-key.pem`

---

## Tổng quan luồng deploy

```
Code thay đổi (local)
        │
        ▼
  git push → main
        │
        ├─── API thay đổi? ──► docker compose build + up (trên EC2)
        │
        └─── Client thay đổi? ► dotnet publish (local) → rsync → EC2
```

---

## 1. Update API (Backend)

Khi có thay đổi trong `src/API/`, `src/Service/`, `src/Infrastructure/`, `src/Core/`, `src/Shared/`.

```bash
# Bước 1: Push code lên main
git push origin main

# Bước 2: SSH vào EC2
ssh -i ~/.ssh/hushstore-key.pem ubuntu@47.130.131.199

# Bước 3: Pull + build + restart (chạy trên EC2)
cd /opt/hushstore
git pull origin main
sudo docker compose build api
sudo docker compose up -d api

# Bước 4: Verify
curl -s http://localhost:8080/health
sudo docker compose logs api --tail=20
```

**Thời gian:** ~5-8 phút (build Docker image).

---

## 2. Update Frontend (Blazor WASM)

Khi có thay đổi trong `src/Client/` hoặc `src/Shared/`.

> ⚠️ Build **trên máy local** (không build trên EC2 — t3.micro sẽ OOM).

```bash
# Bước 1: Push code lên main
git push origin main

# Bước 2: Build local
dotnet publish src/Client/Client.csproj \
  -c Release -o /tmp/blazor-publish \
  --nologo -v q

# Bước 3: Rsync lên EC2
rsync -a --delete \
  -e "ssh -i ~/.ssh/hushstore-key.pem" \
  /tmp/blazor-publish/wwwroot/ \
  ubuntu@47.130.131.199:/var/www/hushstore/wwwroot/

# Bước 4: Dọn dẹp local
rm -rf /tmp/blazor-publish
```

**Thời gian:** ~2-3 phút (build) + ~30 giây (rsync).

---

## 3. Update cả API lẫn Frontend

Chạy tuần tự hai lệnh trên, hoặc dùng script tiện lợi:

```bash
#!/usr/bin/env bash
# Chạy từ thư mục gốc project
set -euo pipefail

EC2="ubuntu@47.130.131.199"
KEY="~/.ssh/hushstore-key.pem"

echo "[1/4] Push code..."
git push origin main

echo "[2/4] Build Blazor WASM..."
dotnet publish src/Client/Client.csproj -c Release -o /tmp/blazor-publish --nologo -v q

echo "[3/4] Deploy Frontend..."
rsync -a --delete -e "ssh -i $KEY" /tmp/blazor-publish/wwwroot/ $EC2:/var/www/hushstore/wwwroot/
rm -rf /tmp/blazor-publish

echo "[4/4] Deploy API..."
ssh -i $KEY $EC2 "cd /opt/hushstore && git pull origin main && sudo docker compose build api && sudo docker compose up -d api"

echo "✓ Deploy hoàn thành!"
```

---

## 4. Migration Database

Khi có migration EF Core mới (file trong `src/Infrastructure/Migrations/`):

```bash
# Migration tự động chạy khi API container khởi động
# Kiểm tra trong logs:
ssh -i ~/.ssh/hushstore-key.pem ubuntu@47.130.131.199 \
  "sudo docker compose -f /opt/hushstore/docker-compose.yml logs api | grep -i migration"
```

> Migration chạy tự động lúc startup — không cần làm gì thêm.

---

## 5. Bật / Tắt hệ thống (tiết kiệm chi phí)

```bash
# Tắt EC2 + RDS (khi không dùng)
bash infra/stop.sh

# Bật lại
bash infra/start.sh
```

> ⚠️ Sau khi `start.sh`: IP EC2 có thể thay đổi. Script sẽ thông báo nếu cần update Cloudflare DNS.

---

## 6. Khi IP EC2 thay đổi (sau Start)

`start.sh` tự phát hiện IP mới. Nếu IP đổi, cần:

**a) Update Cloudflare DNS:**
- Vào Cloudflare → `hushstore.io.vn` → DNS
- Sửa record `A @` và `A api` sang IP mới

**b) Update SSH Security Group (nếu IP máy local đổi):**
```bash
OLD_IP="IP_CŨ_CỦA_MÁY_LOCAL"
NEW_IP=$(curl -s https://api64.ipify.org)

aws --profile hushstore --region ap-southeast-1 ec2 revoke-security-group-ingress \
  --group-id sg-04c2ac92924081a5d --protocol tcp --port 22 --cidr "$OLD_IP/32"

aws --profile hushstore --region ap-southeast-1 ec2 authorize-security-group-ingress \
  --group-id sg-04c2ac92924081a5d --protocol tcp --port 22 --cidr "$NEW_IP/32"

echo "SSH rule updated: $NEW_IP"
```

---

## 7. Xem logs & debug

```bash
SSH="ssh -i ~/.ssh/hushstore-key.pem ubuntu@47.130.131.199"

# API logs realtime
$SSH "sudo docker compose -f /opt/hushstore/docker-compose.yml logs api -f"

# API logs gần nhất
$SSH "sudo docker compose -f /opt/hushstore/docker-compose.yml logs api --tail=50"

# Nginx logs
$SSH "sudo tail -f /var/log/nginx/error.log"
$SSH "sudo tail -f /var/log/nginx/access.log"

# Kiểm tra container
$SSH "sudo docker ps"

# Restart API nhanh (không build lại image)
$SSH "sudo docker compose -f /opt/hushstore/docker-compose.yml restart api"
```

---

## 8. Checklist trước khi deploy Production

- [ ] `dotnet build PBL3.sln` không có lỗi
- [ ] Không commit file `.env`, `appsettings.Development.json`, `config.json`
- [ ] `git push origin main` thành công
- [ ] Sau deploy: `curl https://api.hushstore.io.vn/health` trả về `{"status":"healthy"}`
- [ ] Mở `https://hushstore.io.vn` trên browser, login thử

---

## 9. Thông tin hạ tầng

| Tài nguyên | ID / Giá trị |
|---|---|
| EC2 Instance | `i-01fa96072d16e846a` |
| EC2 Public IP | `47.130.131.199` (dynamic — đổi khi stop/start) |
| RDS Endpoint | `hushstore-db.c3oiawo2etcf.ap-southeast-1.rds.amazonaws.com` |
| EC2 Security Group | `sg-04c2ac92924081a5d` |
| VPC | `vpc-018d5e85ad2c84984` |
| Region | `ap-southeast-1` (Singapore) |
| AWS Profile | `hushstore` |

> Chi tiết đầy đủ: `infra/resources.env` (gitignored)
