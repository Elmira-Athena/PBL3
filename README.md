# HushStore — IT Hardware E-commerce & Management System

Hệ thống thương mại điện tử và quản lý phần cứng IT, xây dựng trên ASP.NET Core 10 (API) + Blazor WebAssembly (Frontend) + SQL Server 2025.

---

## Kiến trúc

    EC2 Instance
    ├── nginx (SSL termination + reverse proxy)
    │   ├── yourdomain.com      → Blazor WASM static files
    │   └── api.yourdomain.com  → ASP.NET Core API (Docker)
    ├── Docker: hushstore_api (port 8080)
    └── AWS RDS SQL Server (external)

---


## Deploy lên AWS EC2 (lần đầu)

### 1. Chuẩn bị EC2

```bash
# Cài dependencies (Ubuntu 22.04+)
sudo apt update && sudo apt upgrade -y
sudo apt install -y nginx certbot python3-certbot-nginx docker.io rsync

# Cài .NET SDK 10 (để build Blazor WASM)
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 10.0
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc && source ~/.bashrc

# Cài Docker Compose v2
sudo apt install -y docker-compose-plugin
sudo systemctl enable --now docker
sudo usermod -aG docker $USER  # logout & login lại để có hiệu lực
```

### 2. Clone repo & cấu hình secrets

```bash
git clone https://github.com/<your-org>/PBL3.git /opt/hushstore
cd /opt/hushstore

# Tạo file .env từ template
cp .env.example .env
nano .env   # Điền RDS endpoint, JWT secret, AWS credentials
```

### 3. Cấu hình API URL cho Blazor WASM

Sửa `src/Client/wwwroot/appsettings.json`:
```json
{
  "ApiBaseUrl": "https://api.yourdomain.com"
}
```

### 4. Cấu hình nginx

```bash
sudo cp nginx/hushstore.conf /etc/nginx/sites-available/hushstore
# Thay "yourdomain.com" bằng domain thật:
sudo sed -i 's/yourdomain.com/your-actual-domain.com/g' /etc/nginx/sites-available/hushstore

sudo ln -s /etc/nginx/sites-available/hushstore /etc/nginx/sites-enabled/
sudo nginx -t   # Kiểm tra config hợp lệ
sudo nginx -s reload
```

### 5. Lấy SSL certificate (Let's Encrypt)

```bash
sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com -d api.yourdomain.com
```

> Certbot sẽ tự động cập nhật nginx config với SSL. Sau đó: `sudo systemctl reload nginx`

### 6. Deploy lần đầu

```bash
cd /opt/hushstore
bash deploy.sh
```

### 7. Kiểm tra

- **Frontend:** Mở `https://yourdomain.com` → trang chủ load
- **API:** `curl https://api.yourdomain.com/health` → `{"status":"healthy"}`
- **Login:** Đăng nhập với tài khoản test → nhận JWT token thành công

---

## Update sau khi có thay đổi code

```bash
cd /opt/hushstore && bash deploy.sh
```

---

## AWS Security Groups (EC2)

| Port | Protocol | Source | Mục đích |
|------|----------|--------|----------|
| 22   | TCP | Your IP only | SSH |
| 80   | TCP | 0.0.0.0/0 | HTTP (redirect → HTTPS) |
| 443  | TCP | 0.0.0.0/0 | HTTPS |

> Port 8080 (API container) **không mở** ra internet — nginx proxy nội bộ.

---

## Development (local)

```bash
# Khởi động SQL Server
docker compose -f Infrastructure/db/docker-compose.yml up -d

# Chạy API (https://localhost:7010)
dotnet run --project src/API/API.csproj

# Chạy Blazor WASM (https://localhost:7107)
dotnet run --project src/Client/Client.csproj

# Tạo migration mới
dotnet ef migrations add <TênMigration> --project src/Infrastructure --startup-project src/API

# Áp dụng migrations
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

---

## Troubleshooting

**API container không start:**
```bash
docker compose logs api --tail=100
```

**Lỗi migration khi startup:**
```bash
docker compose logs api | grep -i "migration\|error"
```

**nginx 502 Bad Gateway:**
```bash
docker compose ps
docker compose restart api
```

**SSL cert hết hạn:**
```bash
sudo certbot renew --dry-run  # Test trước
sudo certbot renew
```
