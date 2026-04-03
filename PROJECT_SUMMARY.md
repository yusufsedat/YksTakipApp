# YksTakipApp — Proje Özeti

## PROJE HAKKINDA

**YksTakipApp**, YKS öğrencileri için geliştirilmiş bir ders ve sınav takip sistemidir. Çalışma süreleri, konu ilerlemesi, deneme sonuçları, haftalık/aylık program ve notlar tek bir API ve mobil istemci üzerinden yönetilir.

### Projenin amacı

- Günlük çalışma sürelerini kaydetmek ve konuya bağlamak
- Konu listesi ve durum takibi (TYT/AYT, katalog + kullanıcı listesi)
- Deneme sonuçlarını (TYT/AYT/YDT/branş) kaydetmek ve istatistiklerle analiz etmek
- Haftalık/aylık ders programı (schedule) ve hatırlatıcılar
- Küçük zaferler, grafikler ve hedef net çizgileriyle motivasyon ve geri bildirim

### Teknik mimari

| Katman | Teknoloji |
|--------|-----------|
| Backend | .NET 8 Minimal API (C#), Clean Architecture |
| Veritabanı | MySQL 8 (EF Core migrations) |
| Kimlik doğrulama | JWT, BCrypt, Admin/User rolleri |
| Mobil | Expo / React Native (TypeScript), Expo Router |
| Dağıtım (hedef) | AWS Lambda + RDS (hazırlık mevcut; prod opsiyonel) |

### Mevcut durum (özet)

- **Backend API**: Özellik seti tamam; production ortamına deploy kullanıcı sürecine bağlı.
- **Mobil uygulama**: Ana akışlar (auth, konular, çalışma, denemeler, program, istatistikler, not defteri, ayarlar) uygulanmış; uygulama tarafı şimdilik “tamam” kabul edilebilir.
- **Testler**: Birim ve entegrasyon testleri kısmen mevcut; kapsam genişletilebilir.
- **Genel ilerleme**: Ürün özellikleri açısından yüksek; operasyonel (deploy, mağaza, izleme) adımlar sırada.

---

## YAPILANLAR

### Backend (.NET 8)

- **Katmanlar**: Core, Application, Infrastructure, API; Repository, DI, FluentValidation.
- **Varlıklar (özet)**: User, Topic, UserTopic, StudyTime, ExamResult + ExamDetail, ScheduleEntry, ProblemNote; ilişkiler ve migrations.
- **Kimlik**: `POST /users/register`, `POST /users/login`, `GET /users/me`; rate limit, JWT.
- **Konular**: Katalog, kullanıcı konuları, admin konu ekleme, durum güncelleme.
- **Çalışma**: `POST /studytime/add`, `GET /studytime/list` (sayfalama); isteğe bağlı `TopicId` (kullanıcı listesi doğrulaması).
- **Denemeler**: Ekleme, listeleme, silme; sınav türü ve detay satırları.
- **İstatistikler**: Özet, haftalık dk (tam 7 gün), haftalık karşılaştırma, küçük zaferler (`completedTopicNames`), deneme istatistikleri (TYT/AYT/branş), `netTrend` (son 10).
- **Program**: `GET/POST/PUT/DELETE /schedule/*`; isteğe bağlı `TopicId`.
- **Problem notları**: CRUD benzeri uçlar (proje yapısına göre).
- **Güvenlik**: CORS, rate limiting, güvenlik başlıkları, Secrets Manager entegrasyonu (AWS), Lambda entry point ve Dockerfile.

### Mobil (Expo / React Native)

- **Navigasyon**: Auth + (app) sekmeler; güvenli alan ve tema.
- **Ekranlar**: Konular, çalışmalar, denemeler, istatistikler, program, not defteri, araçlar, ayarlar.
- **Ortak**: API istemcisi, JWT saklama, `TopicPickerModal` ve `userTopicRows` (Konular listesi ile uyumlu konu seçimi).
- **İstatistikler**: Haftalık sütun grafik, net eğilimi, hedef net çizgisi (AsyncStorage), deneme listesi filtreleri, branş ders chip’leri.
- **Diğer**: Deneme formu, tarih/saat doğrulamaları, program slotu + konu bağlantısı vb.

---

## SONRAKI ADIMLAR (ÖNCELİK)

### Operasyon ve yayın

1. **AWS (veya seçilen host)**: RDS, Secrets Manager, Lambda deploy, migration, CORS ve URL testi.
2. **Mobil dağıtım**: EAS Build, ortam değişkenleri (`API_BASE_URL`), mağaza listeleri ve gizlilik metni.
3. **İzleme**: CloudWatch / basit hata günlüğü; kritik API hataları için alarm (isteğe bağlı).

### Kalite

1. **Test**: Servis ve API entegrasyon testlerinin kapsamını artırma.
2. **Yedekleme / dışa aktarma**: İleride kullanıcı verisini JSON/CSV dışa aktarma (ürün kararı).

### Dokümantasyon

1. Swagger/Postman kullanımı (iç ekip).
2. Kısa “kurulum ve çalıştırma” README (repo kökünde, mevcutsa güncelleme).

---

## OPSİYONEL ÖZELLİK FİKİRLERİ (İLERİDE)

Bunlar zorunlu değil; ürün olgunlaştıkça değerlendirilebilir:

- **Bildirimler**: Günlük çalışma veya program slotu hatırlatıcısı (push).
- **Çevrimdışı**: Son verileri önbelleğe alıp bağlantı gelince senkron (karmaşıklık yüksek).
- **Widget**: Ana ekranda bugünkü program veya çalışma özeti (iOS/Android).
- **Hedefler**: Sadece net değil; haftalık dk hedefi ve basit rozetler.
- **Yönetici paneli (web)**: Sadece admin için konu kataloğu ve kullanıcı raporu (ayrı küçük proje).

---

## ÖNEMLİ NOTLAR

1. Production’da `CORS__AllowedOrigins` ve güçlü `Jwt:Key` (en az 32 karakter).
2. Hassas bilgiler için Secrets Manager veya güvenli env; repoda sırlar tutulmamalı.
3. Deploy sonrası `dotnet ef database update` ile şema güncel tutulmalı.
4. Mobil tarafta API tabanı URL’si ortam dosyası veya build-time config ile verilmeli.

---

## PROJE DURUMU ÖZETİ

| Bileşen | Durum |
|---------|--------|
| Backend API | Özellik olarak hazır; deploy ortamına bağlı |
| Mobil uygulama | Ana kapsam tamamlandı (şimdilik “bitti” kabulü) |
| AWS / prod | İsteğe bağlı / sıradaki büyük adım |
| Test & izleme | Kısmen; güçlendirilebilir |

*Son güncelleme: Nisan 2026*
