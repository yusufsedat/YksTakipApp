
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY YksTakipApp.sln ./
COPY src/YksTakipApp.Core/YksTakipApp.Core.csproj ./YksTakipApp.Core/
COPY src/YksTakipApp.Infra/YksTakipApp.Infra.csproj ./YksTakipApp.Infra/
COPY src/YksTakipApp.Application/YksTakipApp.Application.csproj ./YksTakipApp.Application/
COPY src/YksTakipApp.Api/YksTakipApp.Api.csproj ./YksTakipApp.Api/

RUN dotnet restore ./YksTakipApp.Api/YksTakipApp.Api.csproj

COPY src/YksTakipApp.Core ./YksTakipApp.Core/
COPY src/YksTakipApp.Infra ./YksTakipApp.Infra/
COPY src/YksTakipApp.Application ./YksTakipApp.Application/
COPY src/YksTakipApp.Api ./YksTakipApp.Api/

COPY src/YksTakipApp.Api/appsettings.json ./YksTakipApp.Api/

RUN dotnet publish ./YksTakipApp.Api/YksTakipApp.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
# Railway (ve benzeri) ortamlar PORT verir; yoksa 8080 kullanılır.
CMD ["/bin/sh", "-c", "exec dotnet YksTakipApp.Api.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]

# Repo kökünden: docker build -t yks-takip-api -f src/YksTakipApp.Api/Dockerfile .
