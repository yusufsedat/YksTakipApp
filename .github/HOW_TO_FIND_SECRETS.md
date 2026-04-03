# 🔍 GitHub Secrets Değerlerini Nereden Bulabilirim?

## 📋 Genel Bakış

Bu 4 secret'ın değerlerini bulmak için AWS Console veya AWS CLI kullanabilirsin. Her ikisini de göstereceğim.

---

## 1️⃣ AWS_ROLE_ARN_PROD

**Bu ne?** GitHub Actions'un AWS'ye bağlanması için kullanılan IAM Role'ün ARN'ı.

### AWS Console'dan Bulma:

1. **AWS Console'a git:** https://console.aws.amazon.com/iam/
2. **Sol menüden "Roles" seç**
3. **Role adını ara:** `github-actions-prod-role` (veya oluşturduğun role adı)
4. **Role'ü tıkla**
5. **"Summary" sekmesinde ARN'ı gör:**
   ```
   ARN: arn:aws:iam::123456789012:role/github-actions-prod-role
   ```
6. **ARN'ı kopyala** → GitHub Secret olarak ekle

### AWS CLI'den Bulma:

```bash
aws iam get-role --role-name github-actions-prod-role --query Role.Arn --output text
```

**Çıktı:**
```
arn:aws:iam::123456789012:role/github-actions-prod-role
```

### ⚠️ Eğer Role Yoksa:

Role'ü oluşturman gerekiyor. `.github/GITHUB_ACTIONS_SETUP.md` dosyasındaki "AWS IAM Role Oluşturma" bölümüne bak.

---

## 2️⃣ AWS_LAMBDA_ROLE_ARN_PROD

**Bu ne?** Lambda function'ının çalışması için kullanılan IAM Role'ün ARN'ı.

### AWS Console'dan Bulma:

1. **AWS Console'a git:** https://console.aws.amazon.com/iam/
2. **Sol menüden "Roles" seç**
3. **Role adını ara:** `yks-takip-lambda-role` (veya oluşturduğun role adı)
4. **Role'ü tıkla**
5. **"Summary" sekmesinde ARN'ı gör:**
   ```
   ARN: arn:aws:iam::123456789012:role/yks-takip-lambda-role
   ```
6. **ARN'ı kopyala** → GitHub Secret olarak ekle

### AWS CLI'den Bulma:

```bash
aws iam get-role --role-name yks-takip-lambda-role --query Role.Arn --output text
```

**Çıktı:**
```
arn:aws:iam::123456789012:role/yks-takip-lambda-role
```

### ⚠️ Eğer Role Yoksa:

Role'ü oluşturman gerekiyor. Bu role şu izinlere sahip olmalı:
- Secrets Manager read access
- CloudWatch Logs write access
- VPC access (eğer RDS kullanıyorsan)

**Oluşturma komutu:**
```bash
aws iam create-role \
  --role-name yks-takip-lambda-role \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Principal": {"Service": "lambda.amazonaws.com"},
      "Action": "sts:AssumeRole"
    }]
  }'

# Permissions ekle
aws iam attach-role-policy \
  --role-name yks-takip-lambda-role \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole

aws iam attach-role-policy \
  --role-name yks-takip-lambda-role \
  --policy-arn arn:aws:iam::aws:policy/SecretsManagerReadWrite
```

---

## 3️⃣ AWS_SECRETS_NAME_PROD

**Bu ne?** AWS Secrets Manager'da sakladığın secret'ın adı (Database connection string ve JWT bilgileri için).

### AWS Console'dan Bulma:

1. **AWS Console'a git:** https://console.aws.amazon.com/secretsmanager/
2. **Sol menüden "Secrets" seç**
3. **Secret listesinde ara:** `yks-takip-app-secrets` (veya oluşturduğun secret adı)
4. **Secret adını kopyala** → GitHub Secret olarak ekle

**Örnek:**
```
yks-takip-app-secrets
```

### AWS CLI'den Bulma:

```bash
aws secretsmanager list-secrets --query "SecretList[?contains(Name, 'yks-takip')].Name" --output text
```

**Çıktı:**
```
yks-takip-app-secrets
```

### ⚠️ Eğer Secret Yoksa:

Secret'ı oluşturman gerekiyor. İçinde şunlar olmalı:

**AWS Console'dan Oluşturma:**

1. **Secrets Manager → Store a new secret**
2. **Secret type:** "Other type of secret"
3. **Key/value pairs ekle:**
   ```
   ConnectionStrings:DefaultConnection = Server=<RDS_ENDPOINT>;Database=yksdb;User=admin;Password=<ŞİFRE>;Port=3306;SslMode=Required;
   Jwt:Key = <EN_AZ_32_KARAKTER_UZUNLUĞUNDA_GÜVENLİ_JWT_SECRET_KEY>
   Jwt:Issuer = YksTakipApp
   Jwt:Audience = YksTakipAppUsers
   ```
4. **Secret name:** `yks-takip-app-secrets`
5. **Encryption:** AWS managed key
6. **Create secret**

**AWS CLI'den Oluşturma:**

```bash
aws secretsmanager create-secret \
  --name yks-takip-app-secrets \
  --secret-string '{
    "ConnectionStrings:DefaultConnection": "Server=<RDS_ENDPOINT>;Database=yksdb;User=admin;Password=<ŞİFRE>;Port=3306;SslMode=Required;",
    "Jwt:Key": "<EN_AZ_32_KARAKTER_UZUNLUĞUNDA_GÜVENLİ_JWT_SECRET_KEY>",
    "Jwt:Issuer": "YksTakipApp",
    "Jwt:Audience": "YksTakipAppUsers"
  }'
```

**JWT Key Oluşturma (32 karakter):**

**PowerShell:**
```powershell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
```

**Linux/Mac:**
```bash
openssl rand -base64 32
```

---

## 4️⃣ CORS_ALLOWED_ORIGINS_PROD

**Bu ne?** Production ortamında API'ye erişmesine izin verilen frontend domain'leri.

### Bu Değeri Sen Belirliyorsun!

Bu secret'ın değeri senin frontend uygulamanın çalışacağı domain'lere bağlı.

### Örnekler:

**Tek domain:**
```
https://yourapp.com
```

**Birden fazla domain (virgülle ayır):**
```
https://yourapp.com,https://www.yourapp.com
```

**Subdomain:**
```
https://app.yourapp.com
```

**Localhost (sadece test için):**
```
http://localhost:3000,http://localhost:5173
```

### ⚠️ ÖNEMLİ:

- **Production'da `*` kullanma!** (Güvenlik riski)
- Sadece kendi domain'lerini ekle
- Virgülle ayırırken boşluk bırakma
- `http://` ve `https://` protokolünü belirt

### Henüz Domain Yoksa:

Eğer henüz domain'in yoksa, geçici olarak şunları kullanabilirsin:

**Test için:**
```
*
```

**Veya boş bırak:**
```
(boş bırakma, en azından * kullan)
```

**Not:** Domain'in hazır olduğunda GitHub Secret'ı güncelle ve Lambda function'ın environment variable'ını da güncelle.

---

## 📝 Hızlı Kontrol Listesi

### AWS Console'dan Kontrol:

1. ✅ **IAM Roles:** https://console.aws.amazon.com/iam/ → Roles
   - `github-actions-prod-role` var mı?
   - `yks-takip-lambda-role` var mı?

2. ✅ **Secrets Manager:** https://console.aws.amazon.com/secretsmanager/
   - `yks-takip-app-secrets` var mı?
   - İçinde gerekli key/value'lar var mı?

3. ✅ **CORS Origins:** Kendi domain'lerini belirle

### AWS CLI'den Kontrol:

```bash
# IAM Roles kontrolü
aws iam list-roles --query "Roles[?contains(RoleName, 'yks-takip') || contains(RoleName, 'github-actions')].RoleName" --output table

# Secrets kontrolü
aws secretsmanager list-secrets --query "SecretList[?contains(Name, 'yks-takip')].Name" --output table

# Role ARN'ları al
aws iam get-role --role-name github-actions-prod-role --query Role.Arn --output text
aws iam get-role --role-name yks-takip-lambda-role --query Role.Arn --output text
```

---

## 🎯 Örnek Değerler

Senin AWS Account ID'n `123456789012` olsun:

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

---

## 🚀 Sonraki Adımlar

1. ✅ AWS'de gerekli kaynakları oluştur (Roles, Secrets)
2. ✅ Değerleri bul/kopyala
3. ✅ GitHub → Settings → Secrets → Actions → New repository secret
4. ✅ Her birini ekle
5. ✅ `main` branch'ine push et → Deploy başlasın!

---

## 🐛 Sorun mu Yaşıyorsun?

### Role Bulunamadı:
- Role'ü oluşturman gerekiyor
- `.github/GITHUB_ACTIONS_SETUP.md` dosyasına bak

### Secret Bulunamadı:
- Secret'ı oluşturman gerekiyor
- Yukarıdaki "Eğer Secret Yoksa" bölümüne bak

### ARN Formatı Yanlış:
- ARN şu formatta olmalı: `arn:aws:iam::<ACCOUNT_ID>:role/<ROLE_NAME>`
- Başta/sonda boşluk olmamalı

---

**Hazır mısın?** Değerleri bulduktan sonra GitHub Secrets'a ekleyebilirsin! 🎉

