# EC2 Deployment Design — HushStore (PBL3)

**Date:** 2026-05-13  
**Status:** Approved

---

## Context

Dự án PBL3 (HushStore) cần được deploy lên AWS EC2 để demo/vận hành. Hiện tại:
- Không có Dockerfile nào
- `appsettings.json` đang commit thông tin nhạy cảm (DB credentials, JWT secret) lên git
- README gần như rỗng
- CORS chỉ whitelist localhost

Mục tiêu: tạo đầy đủ Docker/nginx config để deploy thủ công lên EC2, đồng thời vá các lỗ hổng bảo mật trong cấu hình hiện tại.

---

## Kiến trúc

```
Internet
    │
    ▼
EC2 Instance (Ubuntu 22.04+)
├── nginx (host-level, quản lý SSL bằng Certbot)
│   ├── Port 80  → 301 redirect → HTTPS
│   ├── yourdomain.com (HTTPS/443)
│   │   └── Serve /var/www/hushstore/wwwroot (Blazor WASM static files)
│   │   └── SPA fallback: tất cả route 404 → index.html
│   └── api.yourdomain.com (HTTPS/443)
│       └── Reverse proxy → localhost:8080 (API Docker container)
│
├── Docker container: hushstore_api
│   ├── Image: built từ Dockerfile tại root repo
│   ├── Port: 8080 (internal) → 8080 (host)
│   ├── Config: đọc secrets từ .env file (không commit)
│   └── Restart: unless-stopped
│
└── AWS RDS SQL Server
    ├── Chỉ accessible trong VPC (Security Group không mở port 1433 ra internet)
    └── Kết nối qua internal DNS của RDS
```

**Lý do không container hóa Blazor WASM client:**  
Blazor WASM sau khi `dotnet publish` chỉ là static files (HTML/CSS/JS). Nginx host-level serve trực tiếp nhanh hơn và đơn giản hơn một nginx container bọc ngoài — không cần thêm 1 container chỉ để serve file tĩnh.

---

## Files cần tạo/sửa

### Tạo mới

| File | Mô tả |
|------|-------|
| `Dockerfile` | Multi-stage build: SDK → publish API → runtime image |
| `docker-compose.yml` | Production compose cho API container |
| `.env.example` | Template biến môi trường, placeholder values, commit lên git |
| `nginx/hushstore.conf` | Virtual host config: SSL, proxy API, serve Blazor, SPA routing |
| `deploy.sh` | Script deploy thủ công 1 lệnh trên EC2 |
| `.dockerignore` | Loại trừ file không cần khi build Docker image |

### Sửa

| File | Thay đổi |
|------|----------|
| `.gitignore` | Thêm: `.env`, `*.env`, `appsettings.Production.json` |
| `src/API/appsettings.json` | Xóa hardcoded credentials, thay bằng placeholder/empty |
| `README.md` | Viết lại hoàn toàn: project overview + hướng dẫn deploy từng bước |

---

## Bảo mật — Vấn đề cần vá

### Vấn đề hiện tại (nghiêm trọng)
1. **`appsettings.json`** commit lên git chứa:
   - DB connection string: `Server=192.168.2.63,1433;User Id=athena232;Password=1111`
   - JWT secret: `DayLaMotChuoiBiMatSieuDaiVaPhucTapCuaHushStore_KhongDuocDeLo_2026!!`
2. **`Infrastructure/db/.env`** chứa: `SA_PASSWORD=HushStore@Secure2026!`

### Giải pháp
ASP.NET Core tự động override appsettings bằng environment variables theo cú pháp:
```
ConnectionStrings__DefaultConnection=Server=rds-endpoint;...
JwtSettings__SecretKey=<secret-mới-trên-server>
AwsSettings__AccessKeyId=<key>
AwsSettings__SecretAccessKey=<secret>
```

Các biến này chỉ tồn tại trong file `.env` trên EC2 — không bao giờ commit lên git.

**`appsettings.json` sau khi sửa** (chỉ giữ non-sensitive defaults):
```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "ConnectionStrings": { "DefaultConnection": "" },
  "JwtSettings": {
    "Issuer": "HushStoreAPI",
    "Audience": "HushStoreBlazorClient",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "AwsSettings": {
    "BucketName": "hushstore-images",
    "Region": "ap-southeast-1"
  }
}
```

---

## Chi tiết các file

### Dockerfile (multi-stage)
```
Stage 1 (build): mcr.microsoft.com/dotnet/sdk:10.0
  - Copy toàn bộ source
  - dotnet publish src/API/API.csproj -c Release -o /app/publish

Stage 2 (runtime): mcr.microsoft.com/dotnet/aspnet:10.0
  - Copy /app/publish từ stage 1
  - EXPOSE 8080
  - ENV ASPNETCORE_URLS=http://+:8080
  - ENTRYPOINT dotnet API.dll
```

### docker-compose.yml
```yaml
services:
  api:
    build: .
    container_name: hushstore_api
    ports: ["8080:8080"]
    env_file: .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    restart: unless-stopped
```

### .env.example
```bash
# Database (AWS RDS)
ConnectionStrings__DefaultConnection=Server=<RDS_ENDPOINT>,1433;Database=HushStoreDB;User Id=<USER>;Password=<PASSWORD>;TrustServerCertificate=True;

# JWT
JwtSettings__SecretKey=<random-256-bit-string>

# AWS S3
AwsSettings__AccessKeyId=<YOUR_ACCESS_KEY>
AwsSettings__SecretAccessKey=<YOUR_SECRET_KEY>
```

### nginx/hushstore.conf
```nginx
# Blazor WASM — static files
server {
    listen 80; server_name yourdomain.com www.yourdomain.com;
    return 301 https://yourdomain.com$request_uri;
}
server {
    listen 443 ssl http2; server_name yourdomain.com www.yourdomain.com;
    ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;
    root /var/www/hushstore/wwwroot;
    index index.html;
    location / { try_files $uri $uri/ /index.html; }  # SPA routing
}

# ASP.NET Core API — reverse proxy
server {
    listen 80; server_name api.yourdomain.com;
    return 301 https://api.yourdomain.com$request_uri;
}
server {
    listen 443 ssl http2; server_name api.yourdomain.com;
    ssl_certificate /etc/letsencrypt/live/api.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.yourdomain.com/privkey.pem;
    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### deploy.sh
Script thực hiện theo thứ tự:
1. `git pull origin main`
2. Build Blazor WASM: `dotnet publish src/Client/Client.csproj -c Release -o /tmp/client_publish`
3. Copy static files: `cp -r /tmp/client_publish/wwwroot/* /var/www/hushstore/wwwroot/`
4. Build & restart API: `docker-compose build api && docker-compose up -d api`

> **Lưu ý:** API Docker image dùng `aspnet` runtime (không có SDK), không thể chạy `dotnet ef` bên trong container. Thay vào đó, thêm `context.Database.MigrateAsync()` vào `Program.cs` để migrations tự động áp dụng khi API khởi động lần đầu.

---

## CORS — cập nhật cho production

Trong `Program.cs`, CORS hiện whitelist `localhost`. Cần thêm production domain:
```csharp
.WithOrigins(
    "http://localhost:5214",
    "https://localhost:7107",
    "https://yourdomain.com"   // thêm dòng này
)
```

Giá trị domain production đọc từ environment variable `AllowedOrigins` để không hardcode trong code.

---

## Verification — Test sau deploy

1. **API health check:** `curl https://api.yourdomain.com/api/health` → HTTP 200
2. **Blazor load:** Mở `https://yourdomain.com` trong browser → trang chủ load được
3. **Login flow:** Thử đăng nhập với tài khoản test → nhận JWT token
4. **SSL:** Check badge khóa xanh trên browser
5. **DB connectivity:** API không trả 500 khi gọi endpoint có DB query
6. **S3 upload:** Thử upload ảnh sản phẩm → URL S3 trả về hợp lệ

---

## EC2 Setup Checklist (tóm tắt trong README)

```
1. Provisioning EC2 (Ubuntu 22.04, t3.small trở lên)
2. Mở Security Groups: port 22, 80, 443
3. Cài Docker, Docker Compose, nginx, certbot
4. Clone repo: git clone <repo>
5. Tạo .env từ .env.example, điền credentials thật
6. Tạo /var/www/hushstore/wwwroot/
7. Cấu hình nginx: copy nginx/hushstore.conf → /etc/nginx/sites-available/
8. Certbot: sudo certbot --nginx -d yourdomain.com -d api.yourdomain.com
9. Chạy deploy.sh lần đầu
10. Verify theo checklist trên
```
