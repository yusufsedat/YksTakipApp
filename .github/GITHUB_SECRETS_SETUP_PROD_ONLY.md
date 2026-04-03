# 🔐 GitHub Secrets Kurulum Rehberi (Sadece Production)

## 📋 Production Workflow

**Workflow Dosyası:** `.github/workflows/deploy-prod.yml`

**Tetiklenme:**
- `main` branch'ine push edildiğinde otomatik çalışır
- Veya GitHub Actions → "Deploy to Production" → "Run workflow" ile manuel çalıştırılabilir

**Lambda Function Adı:** `yks-takip-api`

## 🔑 GitHub Secrets Kurulumu (Sadece Production)

### Adım 1: GitHub Repository'ye Git

1. GitHub'da repository'ni aç
2. **Settings** → **Secrets and variables** → **Actions**
3. **New repository secret** butonuna tıkla

### Adım 2: Production Secrets Ekle (4 adet)

Aşağıdaki secrets'ları tek tek ekle (her biri için "New repository secret" tıkla):

#### 1. AWS_ROLE_ARN_PROD
- **Name:** `AWS_ROLE_ARN_PROD`
- **Secret:** `arn:aws:iam::<ACCOUNT_ID>:role/github-actions-prod-role`
- **Açıklama:** GitHub Actions'un AWS'ye bağlanması için kullanılan IAM Role ARN'ı

**Nasıl Bulunur:**
```bash
# AWS Console → IAM → Roles → github-actions-prod-role → ARN'ı kopyala
# Veya AWS CLI ile:
aws iam get-role --role-name github-actions-prod-role --query Role.Arn --output text
```

**Örnek:**
```
arn:aws:iam::123456789012:role/github-actions-prod-role
```

#### 2. AWS_LAMBDA_ROLE_ARN_PROD
- **Name:** `AWS_LAMBDA_ROLE_ARN_PROD`
- **Secret:** `arn:aws:iam::<ACCOUNT_ID>:role/yks-takip-lambda-role`
- **Açıklama:** Lambda function'ının çalışması için kullanılan IAM Role ARN'ı

**Nasıl Bulunur:**
```bash
aws iam get-role --role-name yks-takip-lambda-role --query Role.Arn --output text
```

**Örnek:**
```
arn:aws:iam::123456789012:role/yks-takip-lambda-role
```

#### 3. AWS_SECRETS_NAME_PROD
- **Name:** `AWS_SECRETS_NAME_PROD`
- **Secret:** `yks-takip-app-secrets`
- **Açıklama:** AWS Secrets Manager'daki secret'ın adı

**Nasıl Bulunur:**
- AWS Console → Secrets Manager → Secret adını kopyala
- Veya kendin oluşturduysan adını yaz

**Örnek:**
```
yks-takip-app-secrets
```

#### 4. CORS_ALLOWED_ORIGINS_PROD
- **Name:** `CORS_ALLOWED_ORIGINS_PROD`
- **Secret:** `https://yourapp.com,https://www.yourapp.com`
- **Açıklama:** Production ortamında izin verilen CORS origin'leri (virgülle ayrılmış)

**ÖNEMLİ:** Production'da `*` kullanma! Sadece kendi domain'lerini ekle.

**Örnekler:**
```
https://yourapp.com,https://www.yourapp.com
https://api.yourapp.com
https://yourapp.com
```

## 📝 Secrets Listesi Özeti

### Production Secrets (4 adet - Hepsini ekle!)

```
✅ AWS_ROLE_ARN_PROD
✅ AWS_LAMBDA_ROLE_ARN_PROD
✅ AWS_SECRETS_NAME_PROD
✅ CORS_ALLOWED_ORIGINS_PROD
```

## 🔍 Secrets Kontrol Listesi

GitHub'da secrets'ları ekledikten sonra kontrol et:

1. ✅ **Settings** → **Secrets and variables** → **Actions** sayfasına git
2. ✅ Toplam **4 adet secret** görmelisin (sadece PROD)
3. ✅ Her secret'ın adı doğru mu kontrol et:
   - `AWS_ROLE_ARN_PROD` (büyük harf, alt çizgi)
   - `AWS_LAMBDA_ROLE_ARN_PROD`
   - `AWS_SECRETS_NAME_PROD`
   - `CORS_ALLOWED_ORIGINS_PROD`
4. ✅ Secret değerlerinin sonunda yanlışlıkla boşluk var mı kontrol et

## 🚀 İlk Deploy Öncesi Kontrol

### 1. AWS IAM Role'leri Oluşturuldu mu?

**Production için:**
- [ ] `github-actions-prod-role` (GitHub Actions için)
  - Trust Policy: GitHub OIDC provider
  - Permissions: Lambda, Secrets Manager erişimi
- [ ] `yks-takip-lambda-role` (Lambda için)
  - Permissions: Secrets Manager read, CloudWatch Logs, VPC (eğer RDS kullanıyorsan)

### 2. AWS Secrets Manager'da Secret Var mı?

**Production:**
- [ ] `yks-takip-app-secrets` secret'ı oluşturuldu
- [ ] İçinde şunlar var:
  ```
  ConnectionStrings:DefaultConnection = Server=<RDS_ENDPOINT>;Database=yksdb;User=admin;Password=<ŞİFRE>;Port=3306;SslMode=Required;
  Jwt:Key = <EN_AZ_32_KARAKTER_UZUNLUĞUNDA_GÜVENLİ_JWT_SECRET_KEY>
  Jwt:Issuer = YksTakipApp
  Jwt:Audience = YksTakipAppUsers
  ```

### 3. GitHub Secrets Eklendi mi?

- [ ] Tüm 4 secret GitHub'a eklendi
- [ ] Secret isimleri tam olarak eşleşiyor (büyük/küçük harf duyarlı!)

## 🎯 Örnek Secret Değerleri

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
   - ✅ `AWS_ROLE_ARN_PROD` (doğru)
   - ❌ `aws_role_arn_prod` (yanlış)
   - ❌ `AWS_ROLE_ARN_PROD ` (sonunda boşluk - yanlış)

2. **Secret Değerlerinde Boşluk Olmamalı**
   - Secret eklerken başta/sonda boşluk bırakma
   - ARN'ların sonunda boşluk olmamalı

3. **ARN Formatı**
   - ARN'lar `arn:aws:iam::<ACCOUNT_ID>:role/<ROLE_NAME>` formatında olmalı
   - `<ACCOUNT_ID>` yerine gerçek AWS Account ID'ni yaz
   - Örnek: `arn:aws:iam::123456789012:role/github-actions-prod-role`

4. **CORS Origins**
   - Production'da **mutlaka** belirli domain'leri yaz
   - `*` kullanma (güvenlik riski!)
   - Virgülle ayır: `https://domain1.com,https://domain2.com`
   - Boşluk olmamalı: `https://domain1.com, https://domain2.com` ❌

5. **Secrets Güncelleme**
   - Secret'ı güncellemek için: Settings → Secrets → Secret'ı seç → Update
   - Eski değer silinir, yeni değer eklenir

## 🔄 Workflow Test Etme

Secrets'ları ekledikten sonra test et:

1. **Production'a test deploy:**
   ```bash
   git checkout main
   git commit --allow-empty -m "test: trigger prod deploy"
   git push origin main
   ```
   → GitHub Actions'da `Deploy to Production` workflow'u çalışmalı

2. **Manuel tetikleme:**
   - GitHub → Actions → "Deploy to Production" → "Run workflow" → "Run workflow"

## 🐛 Troubleshooting

### Secret Bulunamadı Hatası

**Hata:** `Error: Input required and not supplied: AWS_ROLE_ARN_PROD`

**Çözüm:**
- GitHub → Settings → Secrets → Actions sayfasına git
- Secret'ın adının tam olarak `AWS_ROLE_ARN_PROD` olduğundan emin ol
- Secret'ın değerinin boş olmadığından emin ol
- Büyük/küçük harf kontrolü yap

### IAM Role Hatası

**Hata:** `AccessDeniedException: User is not authorized to perform: sts:AssumeRole`

**Çözüm:**
- IAM Role'ün (`github-actions-prod-role`) Trust Policy'sinde GitHub repository'si doğru mu kontrol et
- Role ARN'ının doğru olduğundan emin ol
- Role'ün gerekli permissions'ları olduğundan emin ol

### Lambda Deploy Hatası

**Hata:** `ResourceNotFoundException: Function not found`

**Çözüm:**
- İlk deploy'da function yoksa otomatik oluşturulur (normal)
- Eğer hata devam ederse Lambda function adını kontrol et (`yks-takip-api`)
- IAM Role'ün Lambda oluşturma izni olduğundan emin ol

### Secrets Manager Hatası

**Hata:** `AccessDeniedException: Secrets Manager`

**Çözüm:**
- Lambda Role'ün (`yks-takip-lambda-role`) Secrets Manager read izni olduğundan emin ol
- Secret adının doğru olduğundan emin ol (`yks-takip-app-secrets`)

## 📊 Deploy Sonrası Kontrol

Deploy başarılı olduktan sonra:

1. **Lambda Function URL'ini al:**
   ```bash
   aws lambda get-function-url-config \
     --function-name yks-takip-api \
     --region eu-central-1 \
     --query FunctionUrl \
     --output text
   ```

2. **Health Check:**
   ```bash
   curl https://<FUNCTION_URL>/
   # Beklenen: "✅ YksTakipApp Lambda API running!"
   ```

3. **CloudWatch Logs kontrolü:**
   - AWS Console → CloudWatch → Log groups → `/aws/lambda/yks-takip-api`
   - Hata var mı kontrol et

## 📚 İlgili Dosyalar

- `.github/workflows/deploy-prod.yml` - Production workflow
- `.github/GITHUB_ACTIONS_SETUP.md` - Genel setup rehberi (OIDC kurulumu için)

---

**Hazır mısın?** 4 secret'ı ekledikten sonra `main` branch'ine push ederek deploy'u başlatabilirsin! 🚀

