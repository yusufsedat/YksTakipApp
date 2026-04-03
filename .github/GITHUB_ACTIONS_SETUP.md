# 🚀 GitHub Actions CI/CD Setup Rehberi

## 📋 Genel Bakış

Bu proje için 4 adet GitHub Actions workflow'u hazırlandı:

1. **CI (ci.yml)** - Build ve test (PR ve push'larda çalışır)
2. **Deploy Dev (deploy-dev.yml)** - Development ortamına deploy (develop branch)
3. **Deploy Prod (deploy-prod.yml)** - Production ortamına deploy (main branch)
4. **Code Quality (code-quality.yml)** - Kod kalitesi kontrolleri

## 🔐 GitHub Secrets Yapılandırması

### AWS IAM Role Oluşturma (OIDC için - Önerilen)

GitHub Actions'un AWS'ye erişmesi için OIDC (OpenID Connect) kullanman önerilir. Bu, access key'leri GitHub'da saklamaktan daha güvenlidir.

#### 1. AWS IAM Identity Provider Oluştur

```bash
aws iam create-open-id-connect-provider \
  --url https://token.actions.githubusercontent.com \
  --client-id-list sts.amazonaws.com \
  --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1
```

#### 2. IAM Role Oluştur (GitHub Actions için)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::<ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com"
        },
        "StringLike": {
          "token.actions.githubusercontent.com:sub": "repo:<GITHUB_USER>/<REPO_NAME>:*"
        }
      }
    }
  ]
}
```

**Role Permissions:**
- `AWSLambda_FullAccess` (Lambda function oluşturma/güncelleme için)
- `SecretsManagerReadWrite` (Secrets Manager erişimi için)
- `IAMPassRole` (Lambda role'ü atamak için)

#### 3. GitHub Secrets Ekle

GitHub Repository → Settings → Secrets and variables → Actions → New repository secret

**Development Secrets:**
- `AWS_ROLE_ARN_DEV` - `arn:aws:iam::<ACCOUNT_ID>:role/github-actions-dev-role`
- `AWS_LAMBDA_ROLE_ARN_DEV` - `arn:aws:iam::<ACCOUNT_ID>:role/yks-takip-lambda-role-dev`
- `AWS_SECRETS_NAME_DEV` - `yks-takip-app-secrets-dev`
- `CORS_ALLOWED_ORIGINS_DEV` - `*` veya development domain'leri

**Production Secrets:**
- `AWS_ROLE_ARN_PROD` - `arn:aws:iam::<ACCOUNT_ID>:role/github-actions-prod-role`
- `AWS_LAMBDA_ROLE_ARN_PROD` - `arn:aws:iam::<ACCOUNT_ID>:role/yks-takip-lambda-role`
- `AWS_SECRETS_NAME_PROD` - `yks-takip-app-secrets`
- `CORS_ALLOWED_ORIGINS_PROD` - Production domain'leri (örn: `https://yourapp.com,https://www.yourapp.com`)

**Opsiyonel (Migration için):**
- `RDS_CONNECTION_STRING_PROD` - RDS connection string (migration için)

## 🔄 Workflow Akışı

### CI Workflow (ci.yml)
```
PR/Push → Checkout → Setup .NET → Restore → Build → Test → Publish → Upload Artifacts
```

### Deploy Dev Workflow (deploy-dev.yml)
```
Push to develop → Build → Package → Deploy Lambda → Create/Update Function URL → Health Check
```

### Deploy Prod Workflow (deploy-prod.yml)
```
Push to main → Build → Package → Deploy Lambda → Create/Update Function URL → Health Check → (Migration)
```

## 🛠️ Kullanım

### Otomatik Deploy

1. **Development'a deploy:**
   ```bash
   git checkout develop
   git commit -m "feat: new feature"
   git push origin develop
   ```
   → Otomatik olarak `yks-takip-api-dev` Lambda function'ına deploy edilir

2. **Production'a deploy:**
   ```bash
   git checkout main
   git merge develop
   git push origin main
   ```
   → Otomatik olarak `yks-takip-api` Lambda function'ına deploy edilir

### Manuel Deploy

GitHub Actions → Deploy to Production → Run workflow → Run workflow

## 📝 Özelleştirme

### Environment Variables

Workflow dosyalarında environment variables'ı değiştirebilirsin:

```yaml
env:
  AWS_REGION: eu-central-1  # Kendi region'ını kullan
  FUNCTION_NAME: yks-takip-api
  RUNTIME: provided.al2023
```

### Lambda Configuration

Memory ve timeout değerlerini ihtiyacına göre ayarla:

```yaml
--memory-size 1024  # 128-10240 MB arası
--timeout 30        # 1-900 saniye arası
```

### Database Migration

Production deploy'da otomatik migration çalıştırmak istersen:

1. `RDS_CONNECTION_STRING_PROD` secret'ını ekle
2. `deploy-prod.yml` dosyasındaki migration adımının comment'ini kaldır
3. Migration komutunu düzenle

## 🔒 Güvenlik Best Practices

1. ✅ **OIDC kullan** - Access key'ler yerine IAM Role
2. ✅ **Secrets Manager** - Hassas bilgileri GitHub Secrets'da saklama
3. ✅ **Least Privilege** - IAM Role'lere sadece gerekli izinleri ver
4. ✅ **Branch Protection** - Main branch için approval gerektir
5. ✅ **Environment Separation** - Dev ve Prod için ayrı secrets

## 🚨 Troubleshooting

### Lambda Deploy Hatası

**Hata:** `AccessDeniedException`
**Çözüm:** IAM Role'ün Lambda ve Secrets Manager izinleri olduğundan emin ol

**Hata:** `ResourceNotFoundException`
**Çözüm:** Lambda function'ı ilk kez oluştururken tüm parametreleri doğru ver

### Health Check Hatası

**Hata:** `curl: (7) Failed to connect`
**Çözüm:** 
- Lambda cold start için bekleme süresini artır
- Function URL'in doğru oluşturulduğunu kontrol et
- CORS ayarlarını kontrol et

### Build Hatası

**Hata:** `dotnet restore` başarısız
**Çözüm:**
- NuGet source'larını kontrol et
- Internet bağlantısını kontrol et
- .NET SDK versiyonunu kontrol et

## 📊 Monitoring

GitHub Actions → Actions sekmesinden tüm workflow run'larını görebilirsin:
- ✅ Başarılı deploy'lar
- ❌ Başarısız deploy'lar
- ⏱️ Deploy süreleri
- 📝 Log'lar

## 🔄 Rollback

Bir deploy başarısız olursa veya geri almak istersen:

```bash
# Önceki versiyona geri dön
git revert HEAD
git push origin main
```

Veya AWS Console'dan Lambda function'ın önceki versiyonunu deploy edebilirsin.

## 📚 Kaynaklar

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [AWS Lambda Deployment](https://docs.aws.amazon.com/lambda/latest/dg/gettingstarted-deploy.html)
- [OIDC with GitHub Actions](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-amazon-web-services)

