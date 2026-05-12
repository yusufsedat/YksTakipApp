# YksTakipApp — Proje Özeti

Bu belge, repodaki **gerçek yapı ve özellikler**e göre güncellenir: backend minimal API, MySQL + EF Core, Expo (React Native) istemci.

---

## 1. Ürün ne işe yarıyor?

**YksTakipApp**, YKS hazırlığında öğrencinin **konu takibi**, **çalışma süreleri**, **deneme sonuçları**, **haftalık/aylık program**, **istatistik ve motivasyon geri bildirimi** ve **çözemediği sorular için not defteri (Kumbara)** işlerini tek uygulamada toplar.

### Özellikler (işlevsel)

| Alan | Kullanıcıya sunduğu değer |
|------|---------------------------|
| **Kimlik** | Kayıt, giriş, JWT + refresh token; profil (`/users/me` ile konu listesi + özet istatistik). |
| **Konular** | Sistem kataloğundan (sayfalı) konu görüntüleme; kullanıcıya özel takip listesi; durum güncelleme; admin için kataloga konu ekleme. |
| **Çalışma süresi** | Tarih/dakika kaydı; isteğe bağlı konu bağlantısı; liste (sayfalama); toplu oluşturma ve uyumluluk uçları; çevrimdışı kuyruk + Android kronometre foreground bildirimi (Duraklat/Bitir). |
| **Denemeler** | TYT / AYT / YDT / branş türleri; net ve ders bazlı detay satırları; listeleme, ekleme, silme. |
| **Program** | Haftalık veya aylık tekrar; gün + başlangıç/bitiş dakikası; başlık; isteğe bağlı konu; CRUD. Ayrıca hedef bazlı haftalık görev üretimi, görev tamamlama/atlama ve dinamik plan güncelleme akışları. Churn kontrolü haftalık plan sorgusunda otomatik tetiklenir. |
| **İstatistikler** | Son 7 gün özeti; günlük dakika serisi (7 gün); haftalık ilerleme karşılaştırması; “zaferler” (deneme serisi + konu tamamlama branş/ders bazında); TYT/AYT/branş deneme istatistikleri (ortalama net, son denemeler, net eğilimi, ders ortalamaları, branşta zorluk dağılımı vb.). |
| **Hedefler ve öneriler** | Hedef oluşturma/güncelleme (günlük kapasite min 30 dk doğrulaması), onboarding tabanlı plan başlangıcı, konu ağırlığı + performansa göre öneri ve adaptasyon motoru. |
| **Kumbara (problem notları)** | Fotoğraflı soru notu; etiketler; çözüldü işareti; Cloudinary veya stub depolama; soft delete. |
| **Özet ekranı** | YKS geri sayım kartı, profil özeti, haftalık çalışma trendi, deneme serisi vurgusu. |
| **Araçlar hub** | Çalışmalar, program, Kumbara, görünüm (tema) için tek giriş noktası. |
| **Sürüm kontrolü** | `GET /api/app-config/check-version` ile zorunlu/isteğe bağlı güncelleme bilgisi (mobil açılışta). |

---

## 2. Teknik mimari

### 2.1 Genel

| Bileşen | Teknoloji |
|---------|-----------|
| API | **.NET 8** Minimal API, `FluentValidation`, **JWT Bearer**, **Pomelo EF Core** + **MySQL 8** |
| Çözüm katmanları | **Core** (entity + arayüzler), **Application** (servisler), **Infra** (EF, repository), **Api** (endpoint’ler, DTO, middleware) |
| Mobil | **Expo** + **React Native** (TypeScript), **Expo Router** (dosya tabanlı rota) |
| Loglama (API) | **Serilog**, konsolda **RenderedCompactJsonFormatter**; istek günlüğü (test ortamı hariç) |
| Dağıtım | API için `src/YksTakipApp.Api/Dockerfile`; hedef platform örneği **Railway** + MySQL |

### 2.2 Backend önemli parçalar

- **Kimlik:** BCrypt ile parola; access token (~1 saat); refresh token DB’de; `/users/refresh-token` ve legacy `/refresh-token`.
- **Yetki:** `AdminOnly` / `UserOnly` politikaları; admin konu oluşturma gibi uçlar role bağlı.
- **Rate limiting:** Örn. login ve yazma pencereleri (`Program.cs` içinde tanımlı limiter isimleri).
- **CORS:** Geliştirmede geniş; production’da `CORS__AllowedOrigins` (virgülle ayrı liste).
- **Güvenlik başlıkları:** Production’da X-Content-Type-Options, X-Frame-Options, HSTS (HTTPS), vb.
- **Hata:** `GlobalExceptionMiddleware` ile tutarlı HTTP yanıtları.
- **Sağlık:** `GET /health` — DB bağlantısı; izleme uyumlu JSON (`ok` / `degraded`).
- **Geliştirme:** İsteğe bağlı demo seed (`DevDataSeeder`, `AdminDevSeeder`); `SKIP_DEV_SEED` ile kapatılabilir; dev’de migration uygulama.
- **Problem notu görselleri:** `IProblemNoteImageStorage` — **Cloudinary** veya **stub** (yapılandırmaya bağlı).

### 2.3 Veri modeli (özet)

Kullanıcı; konu (katalog); kullanıcı–konu ilişkisi ve durum; çalışma süresi (tarih, dakika, isteğe bağlı konu); deneme sonucu + ders detayları; program girdisi (tekrar tipi, gün, dakika aralığı, başlık, isteğe bağlı konu); problem notu (görsel URL, etiket, çözüldü, silinme zamanı). İndeksler için ayrı migration’lar (performans fazı) mevcut.

### 2.4 HTTP yüzeyi (gruplar)

- **Users:** `POST /users/register`, `POST /users/login`, `POST /users/refresh-token`, `GET /users/me` (profil + kullanıcı konuları + özet sayılar).
- **Topics:** katalog `GET /topics` (public/cache); kullanıcı konuları ve CRUD/admin uçları.
- **StudyTime:** ekleme, çoklu oluşturma, uyumluluk uçları, sayfalı liste.
- **Exam:** ekleme, liste, silme.
- **Stats:** `/stats/summary`, `/weekly`, `/progress`, `/wins`, `/stats/exam/tyt|ayt|brans`.
- **Goals:** hedef oluşturma/listeleme, durum görüntüleme, atlama.
- **Planner:** haftalık plan üretme/sorgulama, görev durum güncelleme.
- **Recommendations:** kişiselleştirilmiş konu önerileri.
- **Adaptation:** performans değerlendirme ve dinamik plan uyarlama.
- **Event wiring:** exam kaydı sonrası adaptation değerlendirmesi background worker kuyruğu ile fire-and-forget çalışır.
- **Schedule:** liste, ekleme, güncelleme, silme.
- **Problem notes:** liste, ekleme, güncelleme, silme.
- **App config:** `GET /api/app-config/check-version?platform=…`
- **Sistem:** `/`, `/health`, geliştirmede `/dbtest`.

### 2.5 Testler

`tests/YksTakipApp.Tests` altında **servis birim testleri** (kullanıcı, konu, çalışma, deneme, program, istatistik, problem notu) ve **entegrasyon testleri** (kullanıcı ve konu endpoint’leri). Kapsam genişletilebilir.

---

## 3. Mobil uygulama (Expo)

### 3.1 Yapı

- **Rotalar:** `mobile/app/` — `(auth)` giriş/kayıt/KVKK; `(app)` sekme + gizli rotalar (çalışma, program, Kumbara, ayarlar, hedef onboarding, akıllı plan, dinamik plan, öneriler).
- **Sekmeler (alt bar):** Özet, Konular, Araçlar, Denemeler, İstatistik.
- **Kütüphane:** `mobile/src/lib/` — `api.ts` (axios, JWT, refresh interceptor), `auth.tsx`, kronometre ve senkron yardımcıları, `log.ts`, yapılandırma.
- **Bileşenler:** `mobile/src/components/` (ör. özet, istatistik görselleri, konu seçici, geri sayım).
- **Tema:** `ThemeContext` + renkler; ayarlardan görünüm.
- **API tipleri:** `mobile/src/types/api.ts` (DTO’lar ile uyumlu).

### 3.2 İstemci davranışı

- API tabanı: `EXPO_PUBLIC_API_URL` / config (`mobile/src/lib/config.ts`).
- Oturum: SecureStore + refresh ile sessiz yenileme.
- Çalışma kaydı: ağ yoksa kuyruklama (`pendingStudyTimes`); Android’de foreground servis bildirimi.

---

## 4. Operasyon ve güvenlik notları

1. **Production:** `Jwt__Key` (≥32 karakter), `Jwt__Issuer`, `Jwt__Audience`, `ConnectionStrings__DefaultConnection` (MySQL; Railway’de boş genişleme hatalarına karşı doğrulanmış bağlantı), `CORS__AllowedOrigins`.
2. Sırlar repoda değil; ortam / secret store.
3. Deploy sonrası: `dotnet ef database update` (veya pipeline migration).
4. Mobil: EAS ve mağaza için sürüm, gizlilik metni, API URL.

---

## 5. Durum ve yol haritası (özet)

| Bileşen | Durum |
|---------|--------|
| API işlevleri | Ana domain uçları + hedef/planlayıcı/öneri/adaptasyon endpoint’leri uygulanmış; sağlık ve güvenlik başlıkları aktif |
| Mobil | Ana akışlar, Android kronometre bildirimi ve hedef tabanlı plan ekranları mevcut |
| Test | Servis + kısmi entegrasyon; artırılabilir |
| Prod deploy | Tamamlandı (ortam değişkenleri + migration uygulanmış) |

**İleride düşünülebilecekler (ürün kararı):** planlı hatırlatıcılar, çevrimdışı tam senkron, widget, haftalık hedef/rozet, veri dışa aktarma, ayrı admin web arayüzü.

---

*Son güncelleme: 5 Mayıs 2026*
