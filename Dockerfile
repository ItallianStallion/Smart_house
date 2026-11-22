# syntax=docker/dockerfile:1

# ---------- Етап 1: збирання ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Спочатку лише .csproj: шар із відновленими пакетами кешується, поки не змінились залежності.
COPY SmartHomeSystem.Domain/SmartHomeSystem.Domain.csproj SmartHomeSystem.Domain/
COPY SmartHomeSystem.Infrastructure/SmartHomeSystem.Infrastructure.csproj SmartHomeSystem.Infrastructure/
COPY SmartHomeSystem.Services/SmartHomeSystem.Services.csproj SmartHomeSystem.Services/
COPY SmartHomeSystem.API/SmartHomeSystem.API.csproj SmartHomeSystem.API/
RUN dotnet restore SmartHomeSystem.API/SmartHomeSystem.API.csproj

COPY . .
RUN dotnet publish SmartHomeSystem.API/SmartHomeSystem.API.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Етап 2: запуск ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Не root: образи .NET 8 містять користувача "app" (UID 1654, змінна $APP_UID).
USER $APP_UID

# Типово образ слухає порт 8080 (ASPNETCORE_HTTP_PORTS у базовому образі .NET 8).
# На Render порт задається змінною середовища ASPNETCORE_URLS=http://0.0.0.0:10000 (див. RENDER_DEPLOY.md).
EXPOSE 8080

# УВАГА: рядок підключення до PostgreSQL, секрети та прапорці (Swagger, SeedDemoData) в образ НЕ вшиваються —
# вони передаються під час запуску змінними середовища (Render -> Environment, локально — docker-compose.yml).
ENTRYPOINT ["dotnet", "SmartHomeSystem.API.dll"]
