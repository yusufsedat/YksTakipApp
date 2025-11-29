# 🚀 YksTakipApp Deploy Hazırlık Checklist

## ✅ Deploy Öncesi Kontroller

### 1. Kod Hazırlığı

- [x] LambdaEntryPoint.cs oluşturuldu
- [x] Program.cs Lambda için yapılandırıldı
- [x] Güvenlik iyileştirmeleri yapıldı (CORS, Security Headers, JWT validation)
- [x] Secrets Manager entegrasyonu hazır
- [x] Database migration dosyaları mevcut
- [ ] **YAPILACAK**: Projeyi build et ve hata kontrolü yap
  ```powershell
  dotnet build src/YksTakipApp.Api/YksTakipApp.Api.csproj -c Release
  ```

### 2. AWS Hesap Hazırlığı

- [ ] **AWS CLI kurulu ve yapılandırılmış**
  ```powershell
  aws --version
  aws configure
  aws sts get-caller-identity  # Credentials kontrolü
  ```

- [ ] **AWS Region seçildi** (örn: eu-central-1, us-east-1)
  - Region'ı not al: `_________________`

- [ ] **AWS Account ID biliniyor**
  ```powershell
  aws sts get-caller-identity --query Account --output text
  ```
  - Account ID: `_________________`

### 3. AWS Kaynakları Oluşturma (AWS Console)

#### 3.1. RDS MySQL Database

- [ ] **RDS MySQL instance oluştur**
  - AWS Console → RDS → Create database
  - **Engine**: MySQL 8.0
  - **Template**: Free tier (test için) veya Production
  - **DB instance identifier**: `yks-takip-db`
  - **Master username**: `admin` (veya istediğin)
  - **Master password**: `_________________` (GÜVENLİ ŞİFRE - KAYDET!)
  - **DB instance class**: `db.t3.micro` (free tier) veya `db.t3.small`
  - **Storage**: 20 GB (free tier) veya daha fazla
  - **VPC**: Default VPC veya yeni VPC
  - **Public access**: **NO** (Güvenlik için)
  - **VPC security group**: Yeni oluştur (Lambda için erişim izni ver)
  - **Database name**: `yksdb`
  - **Backup retention**: 7 gün (production için)
  - **Enable encryption**: ✅ Evet

- [ ] **RDS Endpoint'i not al**
  - RDS Console → Databases → `yks-takip-db` → Connectivity & security
  - Endpoint: `_________________.rds.amazonaws.com`

- [ ] **Security Group'u yapılandır**
  - RDS Security Group → Inbound rules
  - Type: MySQL/Aurora (3306)
  - Source: Lambda Security Group (sonra oluşturacağız) veya geçici olarak VPC CIDR

#### 3.2. Secrets Manager

- [ ] **Secret oluştur**
  - AWS Console → Secrets Manager → Store a new secret
  - **Secret type**: Other type of secret
  - **Key/value pairs**:
    ```
    ConnectionStrings:DefaultConnection = Server=<RDS_ENDPOINT>;Database=yksdb;User=admin;Password=<ŞİFRE>;Port=3306;SslMode=Required;
    Jwt:Key = <EN_AZ_32_KARAKTER_UZUNLUĞUNDA_GÜVENLİ_JWT_SECRET_KEY>
    Jwt:Issuer = YksTakipApp
    Jwt:Audience = YksTakipAppUsers
    ```
  - **Secret name**: `yks-takip-app-secrets`
  - **Encryption key**: AWS managed key

- [ ] **JWT Secret oluştur** (en az 32 karakter)
  - Güvenli bir secret: `_________________`
  - Örnek: `openssl rand -base64 32` (Linux/Mac) veya PowerShell ile rastgele oluştur

#### 3.3. IAM Role (Lambda için)

- [ ] **IAM Role oluştur**
  - AWS Console → IAM → Roles → Create role
  - **Trusted entity type**: AWS service
  - **Service**: Lambda
  - **Permissions policies**:
    - `AWSLambdaBasicExecutionRole` (CloudWatch Logs için)
    - `SecretsManagerReadWrite` (Secrets Manager erişimi için)
  - **Role name**: `yks-takip-lambda-role`
  - **Role ARN'ı not al**: `arn:aws:iam::<ACCOUNT_ID>:role/yks-takip-lambda-role`

- [ ] **VPC erişimi için policy ekle** (Lambda VPC'de çalışacaksa)
  - Role → Add permissions → Create inline policy
  - JSON:
    ```json
    {
      "Version": "2012-10-17",
      "Statement": [
        {
          "Effect": "Allow",
          "Action": [
            "ec2:CreateNetworkInterface",
            "ec2:DescribeNetworkInterfaces",
            "ec2:DeleteNetworkInterface"
          ],
          "Resource": "*"
        }
      ]
    }
    ```

#### 3.4. VPC ve Networking (Opsiyonel - Eğer Lambda VPC'de çalışacaksa)

- [ ] **VPC seçimi**
  - Default VPC kullanabilirsin veya yeni VPC oluştur
  - Lambda ve RDS aynı VPC'de olmalı

- [ ] **Security Groups**
  - Lambda Security Group oluştur (RDS'e erişim için)
  - RDS Security Group'a Lambda Security Group'u ekle (inbound rule)

### 4. Environment Variables Hazırlığı

- [ ] **CORS Allowed Origins belirle**
  - Frontend URL'leri: `_________________`
  - Örnek: `https://yourapp.com,https://www.yourapp.com`
  - Veya React Native için: `*` (tüm origin'ler - dikkatli kullan!)

- [ ] **Tüm değerleri not al**:
  ```
  RDS Endpoint: _________________
  Database Name: yksdb
  Database User: admin
  Database Password: _________________
  JWT Secret: _________________
  CORS Origins: _________________
  Lambda Role ARN: _________________
  Region: _________________
  ```

### 5. Build ve Test

- [ ] **Local build test**
  ```powershell
  cd C:\Users\yusuf\Desktop\YksTakipApp
  dotnet build src/YksTakipApp.Api/YksTakipApp.Api.csproj -c Release
  ```

- [ ] **Publish test**
  ```powershell
  dotnet publish src/YksTakipApp.Api/YksTakipApp.Api.csproj -c Release -o ./publish
  ```

- [ ] **ZIP oluşturma test**
  ```powershell
  cd publish
  Compress-Archive -Path * -DestinationPath ../yks-takip-api.zip -Force
  ```

### 6. Deploy Komutları (Hazır)

- [ ] **Deploy script'ini hazırla** (yukarıdaki komutları kullan)

---

## 🚀 Deploy Adımları

### Adım 1: Build ve Package

```powershell
# Proje dizinine git
cd C:\Users\yusuf\Desktop\YksTakipApp

# Build
dotnet build src/YksTakipApp.Api/YksTakipApp.Api.csproj -c Release

# Publish
dotnet publish src/YksTakipApp.Api/YksTakipApp.Api.csproj -c Release -o ./publish

# ZIP oluştur
cd publish
Compress-Archive -Path * -DestinationPath ../yks-takip-api.zip -Force
cd ..
```

### Adım 2: Lambda Function Oluştur

```powershell
aws lambda create-function `
    --function-name yks-takip-api `
    --runtime provided.al2023 `
    --role arn:aws:iam::<ACCOUNT_ID>:role/yks-takip-lambda-role `
    --handler YksTakipApp.Api::YksTakipApp.Api.LambdaEntryPoint::FunctionHandlerAsync `
    --zip-file fileb://yks-takip-api.zip `
    --timeout 30 `
    --memory-size 1024 `
    --environment Variables="{ASPNETCORE_ENVIRONMENT=Production,AWS__SECRETS_NAME=yks-takip-app-secrets,CORS__AllowedOrigins=https://yourapp.com}" `
    --region eu-central-1
```

**ÖNEMLİ**: 
- `<ACCOUNT_ID>` yerine gerçek Account ID'yi yaz
- `CORS__AllowedOrigins` değerini kendi domain'lerinle değiştir

### Adım 3: Function URL Oluştur

```powershell
aws lambda create-function-url-config `
    --function-name yks-takip-api `
    --auth-type NONE `
    --cors "AllowOrigins=*,AllowMethods=GET,POST,PUT,DELETE,OPTIONS,AllowHeaders=Content-Type,Authorization" `
    --region eu-central-1
```

### Adım 4: API URL'ini Al

```powershell
aws lambda get-function-url-config `
    --function-name yks-takip-api `
    --region eu-central-1 `
    --query FunctionUrl `
    --output text
```

**API URL**: `_________________`

### Adım 5: Database Migration

```powershell
# RDS endpoint'ini environment variable olarak ayarla
$env:ConnectionStrings__DefaultConnection="Server=<RDS_ENDPOINT>;Database=yksdb;User=admin;Password=<ŞİFRE>;Port=3306;SslMode=Required;"

# Migration çalıştır
cd src\YksTakipApp.Api
dotnet ef database update --project ..\YksTakipApp.Infra
```

---

## ✅ Deploy Sonrası Kontroller

- [ ] **API Health Check**
  ```powershell
  curl https://<LAMBDA_FUNCTION_URL>/
  # Beklenen: "✅ YksTakipApp Lambda API running!"
  ```

- [ ] **Database Connection Test** (Development endpoint varsa)
  ```powershell
  curl https://<LAMBDA_FUNCTION_URL>/dbtest
  # Beklenen: "✅ Connected" veya "❌ Not Connected"
  ```

- [ ] **User Registration Test**
  ```powershell
  curl -X POST https://<LAMBDA_FUNCTION_URL>/users/register `
    -H "Content-Type: application/json" `
    -d '{"name":"Test User","email":"test@example.com","password":"Test123!"}'
  ```

- [ ] **Login Test**
  ```powershell
  curl -X POST https://<LAMBDA_FUNCTION_URL>/users/login `
    -H "Content-Type: application/json" `
    -d '{"email":"test@example.com","password":"Test123!"}'
  ```

- [ ] **CloudWatch Logs kontrolü**
  - AWS Console → CloudWatch → Log groups → `/aws/lambda/yks-takip-api`
  - Hata var mı kontrol et

---

## 🔄 Güncelleme (Sonraki Deploy'lar)

```powershell
# 1. Build ve Publish
dotnet publish src/YksTakipApp.Api/YksTakipApp.Api.csproj -c Release -o ./publish

# 2. ZIP oluştur
cd publish
Compress-Archive -Path * -DestinationPath ../yks-takip-api.zip -Force
cd ..

# 3. Lambda function'ı güncelle
aws lambda update-function-code `
    --function-name yks-takip-api `
    --zip-file fileb://yks-takip-api.zip `
    --region eu-central-1
```

---

## 🗑️ Temizleme (Stack'i Silme)

```powershell
# Lambda function'ı sil
aws lambda delete-function --function-name yks-takip-api --region eu-central-1

# RDS'i sil (AWS Console'dan - dikkatli!)
# Secrets Manager secret'ı sil (AWS Console'dan)
# IAM Role'ü sil (AWS Console'dan)
```

---

## ⚠️ Önemli Notlar

1. **RDS Endpoint**: RDS oluşturulduktan sonra birkaç dakika bekle, endpoint hazır olana kadar
2. **Security Groups**: Lambda ve RDS aynı VPC'de ve Security Group'lar doğru yapılandırılmış olmalı
3. **Secrets Manager**: Secret oluşturulduktan sonra Lambda'nın erişim izni olduğundan emin ol
4. **CORS**: Production'da mutlaka belirli origin'lere sınırla
5. **JWT Secret**: En az 32 karakter, güvenli ve rastgele olmalı
6. **Database Password**: Güçlü bir şifre kullan (min 8 karakter, özel karakterler)

---

## 📝 Checklist Özeti

**AWS Console'da Yapılacaklar:**
- [ ] RDS MySQL oluştur
- [ ] Secrets Manager secret oluştur
- [ ] IAM Role oluştur
- [ ] Security Groups yapılandır

**Local'de Yapılacaklar:**
- [ ] Build test
- [ ] Publish test
- [ ] ZIP oluşturma test

**Deploy:**
- [ ] Lambda function oluştur
- [ ] Function URL oluştur
- [ ] Database migration çalıştır

**Test:**
- [ ] API health check
- [ ] Database connection test
- [ ] User registration test
- [ ] Login test

---

**Hazır mısın?** Yukarıdaki tüm checkbox'ları işaretledikten sonra deploy'a başlayabilirsin! 🚀

