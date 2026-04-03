# 🔐 GitHub Secrets Kurulum Rehberi

## 📋 Workflow'lar Nasıl Dev/Prod Ayırt Ediyor?

### 1. Branch Bazlı Otomatik Ayrım

**Development Workflow (`deploy-dev.yml`):**
- **Tetiklenme:** `develop` branch'ine push edildiğinde
- **Lambda Function Adı:** `yks-takip-api-dev`
- **Environment:** `Development`
- **Secrets Kullanımı:** `*_DEV` ile biten secrets

**Production Workflow (`deploy-prod.yml`):**
- **Tetiklenme:** `main` branch'ine push edildiğinde
- **Lambda Function Adı:** `yks-takip-api`
- **Environment:** `Production`
- **Secrets Kullanımı:** `*_PROD` ile biten secrets

### 2. Workflow Dosyalarındaki Farklar

```yaml
# deploy-dev.yml
on:
  push:
    branches: [ develop ]  # ← develop branch'i için

env:
  FUNCTION_NAME: yks-takip-api-dev  # ← dev suffix

secrets:
  AWS_ROLE_ARN_DEV  # ← DEV secrets
  AWS_SECRETS_NAME_DEV
```

```yaml
# deploy-prod.yml
on:
  push:
    branches: [ main ]  # ← main branch'i için

env:
  FUNCTION_NAME: yks-takip-api  # ← prod (suffix yok)

secrets:
  AWS_ROLE_ARN_PROD  # ← PROD secrets
  AWS_SECRETS_NAME_PROD
```

## 🔑 GitHub Secrets Kurulumu

### Adım 1: GitHub Repository'ye Git

1. GitHub'da repository'ni aç
2. **Settings** → **Secrets and variables** → **Actions**
3. **New repository secret** butonuna tıkla

### Adım 2: Development Secrets Ekle

Aşağıdaki secrets'ları tek tek ekle (her biri için "New repository secret" tıkla):

#### 2.1. AWS_ROLE_ARN_DEV
- **Name:** `AWS_ROLE_ARN_DEV`
- **Secret:** `arn:aws:iam::<ACCOUNT_ID>:role/github-actions-dev-role`
- **Açıklama:** GitHub Actions'un AWS'ye bağlanması için kullanılan IAM Role ARN'ı (Development)

**Nasıl Bulunur:**
```bash
# AWS Console → IAM → Roles → github-actions-dev-role → ARN'ı kopyala
# Veya AWS CLI ile:
aws iam get-role --role-name github-actions-dev-role --query Role.Arn --output text
```

#### 2.2. AWS_LAMBDA_ROLE_ARN_DEV
- **Name:** `AWS_LAMBDA_ROLE_ARN_DEV`
- **Secret:** `arn:aws:iam::<ACCOUNT_ID>:role/yks-takip-lambda-role-dev`
- **Açıklama:** Lambda function'ının çalışması için kullanılan IAM Role ARN'ı (Development)

**Nasıl Bulunur:**
```bash
aws iam get-role --role-name yks-takip-lambda-role-dev --query Role.Arn --output text
```

#### 2.3. AWS_SECRETS_NAME_DEV
- **Name:** `AWS_SECRETS_NAME_DEV`
- **Secret:** `yks-takip-app-secrets-dev`
- **Açıklama:** AWS Secrets Manager'daki secret'ın adı (Development)

**Nasıl Bulunur:**
- AWS Console → Secrets Manager → Secret adını kopyala
- Veya kendin oluşturduysan adını yaz

#### 2.4. CORS_ALLOWED_ORIGINS_DEV
- **Name:** `CORS_ALLOWED_ORIGINS_DEV`
- **Secret:** `*` veya `http://localhost:3000,http://localhost:5173`
- **Açıklama:** Development ortamında izin verilen CORS origin'leri (virgülle ayrılmış)

**Örnekler:**
- Tüm origin'lere izin: `*`
- Belirli origin'ler: `http://localhost:3000,http://localhost:5173`
- Domain: `https://dev.yourapp.com`

### Adım 3: Production Secrets Ekle

Aynı şekilde Production secrets'ları ekle:

#### 3.1. AWS_ROLE_ARN_PROD
- **Name:** `AWS_ROLE_ARN_PROD`
- **Secret:** `arn:aws:iam::<ACCOUNT_ID>:role/github-actions-prod-role`

#### 3.2. AWS_LAMBDA_ROLE_ARN_PROD
- **Name:** `AWS_LAMBDA_ROLE_ARN_PROD`
- **Secret:** `arn:aws:iam::<ACCOUNT_ID>:role/yks-takip-lambda-role`

#### 3.3. AWS_SECRETS_NAME_PROD
- **Name:** `AWS_SECRETS_NAME_PROD`
- **Secret:** `yks-takip-app-secrets`

#### 3.4. CORS_ALLOWED_ORIGINS_PROD
- **Name:** `CORS_ALLOWED_ORIGINS_PROD`
- **Secret:** `https://yourapp.com,https://www.yourapp.com`
- **ÖNEMLİ:** Production'da `*` kullanma! Sadece kendi domain'lerini ekle.

## 📝 Secrets Listesi Özeti

### Development Secrets (4 adet)
```
✅ AWS_ROLE_ARN_DEV
✅ AWS_LAMBDA_ROLE_ARN_DEV
✅ AWS_SECRETS_NAME_DEV
✅ CORS_ALLOWED_ORIGINS_DEV
```

### Production Secrets (4 adet)
```
✅ AWS_ROLE_ARN_PROD
✅ AWS_LAMBDA_ROLE_ARN_PROD
✅ AWS_SECRETS_NAME_PROD
✅ CORS_ALLOWED_ORIGINS_PROD
```

## 🔍 Secrets Kontrol Listesi

GitHub'da secrets'ları ekledikten sonra kontrol et:

1. ✅ **Settings** → **Secrets and variables** → **Actions** sayfasına git
2. ✅ Toplam **8 adet secret** görmelisin (4 DEV + 4 PROD)
3. ✅ Her secret'ın adı doğru mu kontrol et
4. ✅ Secret değerlerinin sonunda yanlışlıkla boşluk var mı kontrol et

## 🚀 İlk Deploy Öncesi Kontrol

### 1. AWS IAM Role'leri Oluşturuldu mu?

**Development için:**
- [ ] `github-actions-dev-role` (GitHub Actions için)
- [ ] `yks-takip-lambda-role-dev` (Lambda için)

**Production için:**
- [ ] `github-actions-prod-role` (GitHub Actions için)
- [ ] `yks-takip-lambda-role` (Lambda için)

### 2. AWS Secrets Manager'da Secret'lar Var mı?

**Development:**
- [ ] `yks-takip-app-secrets-dev` secret'ı oluşturuldu
- [ ] İçinde şunlar var:
  - `ConnectionStrings:DefaultConnection`
  - `Jwt:Key` (min 32 karakter)
  - `Jwt:Issuer`
  - `Jwt:Audience`

**Production:**
- [ ] `yks-takip-app-secrets` secret'ı oluşturuldu
- [ ] İçinde şunlar var:
  - `ConnectionStrings:DefaultConnection`
  - `Jwt:Key` (min 32 karakter)
  - `Jwt:Issuer`
  - `Jwt:Audience`

### 3. GitHub Secrets Eklendi mi?

- [ ] Tüm 8 secret GitHub'a eklendi
- [ ] Secret isimleri tam olarak eşleşiyor (büyük/küçük harf duyarlı!)

## 🎯 Örnek Secret Değerleri

### AWS_ROLE_ARN_DEV
```
arn:aws:iam::123456789012:role/github-actions-dev-role
```

### AWS_LAMBDA_ROLE_ARN_DEV
```
arn:aws:iam::123456789012:role/yks-takip-lambda-role-dev
```

### AWS_SECRETS_NAME_DEV
```
yks-takip-app-secrets-dev
```

### CORS_ALLOWED_ORIGINS_DEV
```
*
```

### AWS_ROLE_ARN_PROD
```
arn:aws:iam::123456789012:role/github-actions-prod-role
```

### AWS_LAMBDA_ROLE_ARN_PROD
```
arn:aws:iam::123456789012:role/yks-takip-lambda-role
```

### AWS_SECRETS_NAME_PROD
```
yks-takip-app-secrets
```

### CORS_ALLOWED_ORIGINS_PROD
```
https://yourapp.com,https://www.yourapp.com
```

## ⚠️ Önemli Notlar

1. **Secret İsimleri Büyük/Küçük Harf Duyarlı!**
   - ✅ `AWS_ROLE_ARN_DEV` (doğru)
   - ❌ `aws_role_arn_dev` (yanlış)

2. **Secret Değerlerinde Boşluk Olmamalı**
   - Secret eklerken başta/sonda boşluk bırakma

3. **ARN Formatı**
   - ARN'lar `arn:aws:iam::<ACCOUNT_ID>:role/<ROLE_NAME>` formatında olmalı
   - `<ACCOUNT_ID>` yerine gerçek AWS Account ID'ni yaz

4. **CORS Origins**
   - Development'ta `*` kullanabilirsin (test için)
   - Production'da **mutlaka** belirli domain'leri yaz
   - Virgülle ayır: `https://domain1.com,https://domain2.com`

5. **Secrets Güncelleme**
   - Secret'ı güncellemek için: Settings → Secrets → Secret'ı seç → Update
   - Eski değer silinir, yeni değer eklenir

## 🔄 Workflow Test Etme

Secrets'ları ekledikten sonra test et:

1. **Development'a test deploy:**
   ```bash
   git checkout develop
   git commit --allow-empty -m "test: trigger dev deploy"
   git push origin develop
   ```
   → GitHub Actions'da `Deploy to Development` workflow'u çalışmalı

2. **Production'a test deploy:**
   ```bash
   git checkout main
   git commit --allow-empty -m "test: trigger prod deploy"
   git push origin main
   ```
   → GitHub Actions'da `Deploy to Production` workflow'u çalışmalı

## 🐛 Troubleshooting

### Secret Bulunamadı Hatası

**Hata:** `Error: Input required and not supplied: AWS_ROLE_ARN_DEV`

**Çözüm:**
- GitHub → Settings → Secrets → Actions sayfasına git
- Secret'ın adının tam olarak eşleştiğinden emin ol
- Secret'ın değerinin boş olmadığından emin ol

### IAM Role Hatası

**Hata:** `AccessDeniedException: User is not authorized to perform: sts:AssumeRole`

**Çözüm:**
- IAM Role'ün Trust Policy'sinde GitHub repository'si doğru mu kontrol et
- Role ARN'ının doğru olduğundan emin ol

### Lambda Deploy Hatası

**Hata:** `ResourceNotFoundException: Function not found`

**Çözüm:**
- İlk deploy'da function yoksa oluşturulur (normal)
- Eğer hata devam ederse Lambda function adını kontrol et

## 📚 İlgili Dosyalar

- `.github/workflows/deploy-dev.yml` - Development workflow
- `.github/workflows/deploy-prod.yml` - Production workflow
- `.github/GITHUB_ACTIONS_SETUP.md` - Genel setup rehberi

---

**Hazır mısın?** Tüm secrets'ları ekledikten sonra ilk deploy'u yapabilirsin! 🚀

