
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
# Solution ve proje dosyalarını kopyala
COPY YksTakipApp.sln ./
COPY src/YksTakipApp.Core/YksTakipApp.Core.csproj ./YksTakipApp.Core/
COPY src/YksTakipApp.Infra/YksTakipApp.Infra.csproj ./YksTakipApp.Infra/
COPY src/YksTakipApp.Application/YksTakipApp.Application.csproj ./YksTakipApp.Application/
COPY src/YksTakipApp.Api/YksTakipApp.Api.csproj ./YksTakipApp.Api/

# Restore
RUN dotnet restore ./YksTakipApp.Api/YksTakipApp.Api.csproj

# Copy all source code
COPY src/YksTakipApp.Core ./YksTakipApp.Core/
COPY src/YksTakipApp.Infra ./YksTakipApp.Infra/
COPY src/YksTakipApp.Application ./YksTakipApp.Application/
COPY src/YksTakipApp.Api ./YksTakipApp.Api/

COPY src/YksTakipApp.Api/appsettings.json ./YksTakipApp.Api/

# Build + Publish
RUN dotnet publish ./YksTakipApp.Api/YksTakipApp.Api.csproj -c Release -o /app/publish

FROM public.ecr.aws/lambda/dotnet:8 AS runtime
WORKDIR /var/task

# Copy built files
COPY --from=build /app/publish .

# Lambda entrypoint
CMD ["YksTakipApp.Api"]


#docker buildx build --platform linux/amd64 -t yks-takip-api -f src/YksTakipApp.Api/Dockerfile . --load
