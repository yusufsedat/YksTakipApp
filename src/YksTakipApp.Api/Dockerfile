FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 1. Solution ve proje dosyaları (restore)
COPY YksTakipApp.sln ./
COPY src/YksTakipApp.Core/YksTakipApp.Core.csproj src/YksTakipApp.Core/
COPY src/YksTakipApp.Infra/YksTakipApp.Infra.csproj src/YksTakipApp.Infra/
COPY src/YksTakipApp.Application/YksTakipApp.Application.csproj src/YksTakipApp.Application/
COPY src/YksTakipApp.Api/YksTakipApp.Api.csproj src/YksTakipApp.Api/

RUN dotnet restore src/YksTakipApp.Api/YksTakipApp.Api.csproj

# 2. Kaynak kod
COPY src/YksTakipApp.Core/ src/YksTakipApp.Core/
COPY src/YksTakipApp.Infra/ src/YksTakipApp.Infra/
COPY src/YksTakipApp.Application/ src/YksTakipApp.Application/
COPY src/YksTakipApp.Api/ src/YksTakipApp.Api/

# 3. EF migration bundle (linux-x64, self-contained)
# NOT: dotnet ef için -c = --context değil; yapılandırma için --configuration kullan.
RUN dotnet tool install --global dotnet-ef --version 8.0.20
ENV PATH="${PATH}:/root/.dotnet/tools"
# Bundle sırasında Program.cs host oluşturur: gerçek DB/JWT yok. Sadece bu RUN için geçerli.
RUN ASPNETCORE_ENVIRONMENT=Testing \
    Jwt__Key=test-secret-key-min-32-characters-long-for-testing-purposes-only \
    Jwt__Issuer=YksTakipApp \
    Jwt__Audience=YksTakipAppUsers \
    ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;Database=efbundle_build;User=x;Password=x;" \
    dotnet ef migrations bundle \
    --project src/YksTakipApp.Infra/YksTakipApp.Infra.csproj \
    --startup-project src/YksTakipApp.Api/YksTakipApp.Api.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained \
    --output /app/artifacts/efbundle \
    --force

# 4. API publish
RUN dotnet publish src/YksTakipApp.Api/YksTakipApp.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
COPY --from=build /app/artifacts/efbundle ./efbundle
RUN chmod +x ./efbundle

# Railway: PORT değişkeni; Pre-Deploy migration örneği aşağıda.
CMD ["/bin/sh", "-c", "exec dotnet YksTakipApp.Api.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]

# -----------------------------------------------------------------------------
# Railway — Settings → Deploy → Pre-Deploy Command (örnek):
#   ./efbundle --connection "$ConnectionStrings__DefaultConnection"
#
# Bağlantı cümlesi Railway değişkeninde tanımlı olmalı (MySQL plugin reference veya elle).
# Pre-Deploy, yeni imaj ayağa kalkmadan önce çalışır; şema güncel olmadan API başlamaz.
# -----------------------------------------------------------------------------
