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
ADD https://truststore.pki.rds.amazonaws.com/ap-southeast-1/ap-southeast-1-bundle.pem \
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
