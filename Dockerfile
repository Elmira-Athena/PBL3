# syntax=docker/dockerfile:1.7
# Parser directive BẮT BUỘC phải là dòng đầu tiên của file. Nó ghim frontend
# Dockerfile ở 1.7 để cờ `ADD --checksum` bên dưới chắc chắn được hỗ trợ: cờ đó
# xuất hiện từ frontend 1.6, và nếu build bằng frontend cũ hơn thì lỗi báo ra là
# "unknown flag" — nghe như lỗi cú pháp chứ không như lỗi phiên bản.
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore (leverage Docker layer cache)
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Core/Core.csproj src/Core/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Service/Service.csproj src/Service/
COPY src/API/API.csproj src/API/
RUN dotnet restore src/API/API.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/API/API.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Amazon RDS CA cho region ap-southeast-1 — cần để client xác thực cert của RDS
# khi connection string dùng Encrypt=True;TrustServerCertificate=False.
# Phải làm TRƯỚC khi đổi sang user không phải root, vì update-ca-certificates
# ghi vào /etc/ssl/certs.
# --checksum ghim nội dung file tải về. Không có nó, `ADD` từ URL là một điểm
# tin cậy mù: mỗi lần build lại có thể nhận nội dung khác mà build vẫn xanh, và
# thứ đang được nạp ở đây là TRUST STORE — cái quyết định container tin cert nào.
# Ai kiểm soát được đường tải là ghi thêm được CA vào trust store của container.
# SHA-256 đo ngày 2026-08-22 bằng:
#   curl -fsSL https://truststore.pki.rds.amazonaws.com/ap-southeast-1/ap-southeast-1-bundle.pem | shasum -a 256
# AWS luân phiên CA bundle theo lịch nhiều năm; khi build fail vì checksum lệch
# thì ĐỌC changelog của AWS trước rồi mới cập nhật giá trị này — đừng xoá cờ.
ADD --checksum=sha256:3c696020a3b7c6721085d182211c28024ab01873ade35dcc7eeebb89c20ee979 \
    https://truststore.pki.rds.amazonaws.com/ap-southeast-1/ap-southeast-1-bundle.pem \
    /usr/local/share/ca-certificates/rds-ap-southeast-1.crt
RUN chmod 644 /usr/local/share/ca-certificates/rds-ap-southeast-1.crt \
    && update-ca-certificates

# Don't run as root
RUN useradd -m appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "API.dll"]
