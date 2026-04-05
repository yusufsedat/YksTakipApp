FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 1. Adım: Solution ve Proje dosyalarını kopyala (Restore için)
COPY YksTakipApp.sln ./
COPY src/YksTakipApp.Core/YksTakipApp.Core.csproj src/YksTakipApp.Core/
COPY src/YksTakipApp.Infra/YksTakipApp.Infra.csproj src/YksTakipApp.Infra/
COPY src/YksTakipApp.Application/YksTakipApp.Application.csproj src/YksTakipApp.Application/
COPY src/YksTakipApp.Api/YksTakipApp.Api.csproj src/YksTakipApp.Api/

RUN dotnet restore src/YksTakipApp.Api/YksTakipApp.Api.csproj

# 2. Adım: Tüm kaynak kodları kopyala
COPY src/YksTakipApp.Core/ src/YksTakipApp.Core/
COPY src/YksTakipApp.Infra/ src/YksTakipApp.Infra/
COPY src/YksTakipApp.Application/ src/YksTakipApp.Application/
COPY src/YksTakipApp.Api/ src/YksTakipApp.Api/

# 3. Adım: Publish
RUN dotnet publish src/YksTakipApp.Api/YksTakipApp.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .


ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "YksTakipApp.Api.dll"]

