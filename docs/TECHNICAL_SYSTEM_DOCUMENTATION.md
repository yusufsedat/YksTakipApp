# YksTakipApp Teknik Sistem Dokumani

Bu dokuman, `YksTakipApp` sisteminin teknik mimarisini, katmanlarini, veri akislarini, API tasarimini ve operasyonel prensiplerini ayrintili olarak aciklar.

## 1) Sistem Amaci ve Teknik Vizyon

`YksTakipApp`, YKS ogrencileri icin mobil merkezli bir "AI destekli dijital kocluk" platformudur. Teknik hedefler:

- Mobil istemciyi hizli ve sade tutmak
- Domain kurallarini backend servis katmaninda toplamak
- Veri tutarliligini EF Core + migration disiplini ile korumak
- AI planlama/oneri/adaptasyon akislarini senkron endpoint + asenkron worker birlikte yurutmek

## 2) Cozum Mimarisi

Proje cok katmanli bir .NET cozum yapisi kullanir:

- `src/YksTakipApp.Core`
  - Entity modelleri
  - Enumlar
  - Servis arayuzleri
  - Domain DTO/Model recordlari
- `src/YksTakipApp.Application`
  - Is kurallari (service implementation)
  - Domain orchestration
  - Background queue altyapisi
- `src/YksTakipApp.Infra`
  - `AppDbContext`
  - EF Core konfigurasiyonlari
  - Generic repository implementasyonu
  - Migrationlar
- `src/YksTakipApp.Api`
  - Minimal API endpoint tanimlari
  - DTO/validator/mapping katmani
  - Kimlik dogrulama, rate limit, middleware, swagger
  - Hosted worker
- `mobile/`
  - Expo + React Native istemci
  - Ekranlar (expo-router)
  - API istemcisi ve servis katmani
  - Tema, responsive yardimcilari, tipler

Bu ayrim sayesinde:

- API katmani "ince", service katmani "kalin" kalir
- Test edilebilirlik artar
- Migration ve veri modeli degisiklikleri kontrollu ilerler

## 3) Backend Teknoloji Stack'i

- Runtime: .NET 8
- API stili: ASP.NET Core Minimal API
- ORM: EF Core (Pomelo MySQL provider)
- DB: MySQL 8
- Validation: FluentValidation
- Auth: JWT Bearer + refresh token
- Logging: Serilog
- Caching: IMemoryCache (ozellikle topic katalogu)
- Rate limit: fixed-window limiter

## 4) Veritabani ve Domain Modeli

### Ana tablolar / entityler

- `Users`
  - Kimlik bilgileri, rol, refresh token, aktif hedef referansi
- `Topics`
  - Global konu katalogu (`category`, `subject`, `osymWeight`)
- `UserTopics`
  - Kullanici-konu iliskisi
  - Durum + mastery + kilit + oncelik talebi (`IsPriorityRequested`)
- `UserGoals`
  - Hedef universite/bolum, net hedefleri, gunluk kapasite
- `ScheduleTasks`
  - AI tarafindan uretilen tek plan tablosu
  - Task date, duration, status, type, reason
- `StudyTimes`
  - Manuel/otomatik calisma sure kayitlari
- `ExamResults` + `ExamDetails`
  - Deneme sonuclari ve ders kirilimlari
- `ProblemNotes`
  - Kumbara notlari (gorsel + etiket + cozuldu)
- `TopicPrerequisites`
  - Konu onkosul grafi (adaptasyon/planner yardimci modeli)
- `PlannerDecisionLogs` (Faz 6.1)
  - Plan generation cagri ozeti: status/reasonCode/quality/priority/recommendation/duration + sinirli `BreakdownJson`
- `FeatureFlags`, `UserFeatureFlagOverrides` (Faz 6.3)
  - Global flag konfigurasyonu + per-user override (opsiyonel expiry)
- `UserNotificationPreferences` (Faz 6.3)
  - Bildirim tercihleri (daily reminder, recovery reminder, weekly review, quiet hours)
- `UserPlannerChurnEvents`
  - Hafta + reason unique churn olay kayitlari

### Planlama modeli notu

- Eski manuel planlama modeli (`ScheduleEntry`) kaldirilmistir.
- Sistemde planlama icin tek kaynak `ScheduleTasks` tablosudur.

### Migration disiplini

- Model degisiklikleri migration ile ileri tasinir
- Snapshot (`AppDbContextModelSnapshot`) surekli guncel tutulur
- Deployment surecinde migration uygulamasi zorunludur

## 5) API Katmani (Minimal API)

Endpointler domain bazli extension class'lara ayrilmistir:

- `UserEndpoints`
- `TopicEndpoints`
- `StudyTimeEndpoints`
- `ExamEndpoints`
- `StatsEndpoints`
- `GoalEndpoints`
- `RecommendationEndpoints`
- `PlannerEndpoints`
- `AdaptationEndpoints`
- `ProblemNoteEndpoints`
- `AppConfigEndpoints`
- `AnalyticsEndpoints`
- `AdminPlannerEndpoints` (Faz 6.1 + 6.4)
- `AdminFeatureFlagEndpoints` (Faz 6.3)
- `NotificationEndpoints`

### Planner API cekirdegi

- `POST /planner/generate`
  - Haftalik gorev setini yeniden olusturur
- `GET /planner/weekly`
  - Tarih araliginda gorevleri listeler
- `PATCH /planner/tasks/{taskId}/status`
  - Gorev durum guncellemesi

### On-demand priority endpoint

- `POST /topics/{topicId}/request-priority`
  - `UserTopics.IsPriorityRequested = true`
  - Hemen ardindan planner regenerate tetiklenir

### Admin / Debug API (Faz 6)

- `GET /admin/planner/decision-logs?userId=&from=&to=&take=&skip=`
- `GET /admin/planner/decision-logs/{id}` (BreakdownJson dahil)
- `GET /admin/planner/decision-logs/stats?from=&to=` (Faz 7 hazirlik agregati)
- `GET /admin/users/{userId}/planner-debug` (tek aggregate snapshot)
- `GET /admin/feature-flags`
- `PUT /admin/feature-flags/{key}` (rollout clamp + segment whitelist)
- `POST /admin/feature-flags/{key}/overrides`
- `DELETE /admin/feature-flags/{key}/overrides/{userId}`

Tum admin rotalari `AdminOnly` policy zorunludur.

## 6) Servis Katmani Is Akislari

### 6.1 TopicService

- Kullanici konu listesi CRUD islemleri
- Konu durum guncelleme
- Priority talebi isaretleme (`RequestPriorityAsync`)

### 6.2 DynamicPlannerService

`GenerateWeeklyPlanAsync` akisi:

1. Gunluk kapasiteyi aktif hedeften alir (`DailyAvailableMinutes`)
2. Kapasiteyi `%80 calisma` + `%20 buffer` seklinde ayirir
3. Priority isaretli konulari once yerlestirir
   - Once bugun/yarin, sonra haftanin kalan gunleri
   - Uygun ilk kapasiteye zorunlu `Planned` gorev atar
4. Yerlesen priority flag'lerini transaction icinde `false` yapar
5. Kalan kapasiteyi recommendation skorlarina gore dagitir
6. Haftalik planned gorevleri atomik sekilde yeniden yazar

### 6.3 RecommendationService + AdaptationService

- Recommendation:
  - Konu agirligi + performans + durum bilgisine gore oncelik puani olusturur
- Adaptation:
  - Deneme/diagnostic geri beslemeleriyle mastery ve konu kilit mekanigini gunceller

### 6.4 Background worker

- `AdaptationWorker` + `IBackgroundTaskQueue`
- Event tetiklemeli arka plan degerlendirme islemlerini API isteginden ayirir

### 6.5 Planner Decision Logger (Faz 6.1)

- `IPlannerDecisionContextBuilder` → sinirli JSON snapshot (capacity/priority/recommendationSummary/perDayRemaining/qualityComponents)
- `IPlannerDecisionLogger` → resilient persist; logger hatasi planner sonucunu bozmaz, structured `LogError` atilir

### 6.6 Plan Quality Scorer (Faz 6.2)

- `IPlanQualityScorer` saf in-memory servis
- 6 bilesen: CapacityFit / PriorityCoverage / WeaknessCoverage / SubjectBalance / RepetitionSafety / OverloadSafety
- Score `DecisionLog.QualityScore` + `QualityBand` alanlarinda persist

### 6.7 Feature Flag Service (Faz 6.3)

- `IUserSegmentResolver` (cached) → `new_user` / `active_user` / `beta` / `null`
- `IFeatureFlagService` precedence: override > segment > rollout > global
- `userId == null` → sadece global `IsEnabled`
- Deterministik rollout (`StableBucket`)
- Admin endpointlerinde rollout clamp ve segment whitelist validasyonu

### 6.8 Planner Debug Reader (Faz 6.4)

- `IPlannerDebugReader` → user/activeGoal/capacity/segment/latestPlan/latestDecisionLog/priorityRequests/recentChurnEvents/featureFlags aggregate

## 7) Mobil Mimari (Expo)

### Katmanlar

- `mobile/app/`:
  - Ekran rotalari
- `mobile/src/lib/api.ts`:
  - Tum HTTP cagrilari icin ortak katman (`apiGet/apiPost/apiPut/apiDelete/apiPatch`)
  - Token + refresh mekanizmasi
- `mobile/src/services/*`:
  - Domain bazli istemci servisleri (`planner`, `goals`, `recommendations`, `adaptation`)
- `mobile/src/types/*`:
  - API/uygulama tipleri

### UI prensibi

- Ekranlar API orchestrator'u degildir
- Agir is akisi `services` katmaninda tutulur
- Loglama `LogService` uzerinden yapilir

## 8) Guvenlik, Dayaniklilik ve Operasyon

### Guvenlik

- JWT imza anahtari production'da zorunlu
- Role-based policy (`AdminOnly`, `UserOnly`)
- Security headers (production)
- CORS allowlist (production)

### Dayaniklilik

- Db execution strategy ve transaction kullanimi
- API seviyesinde validation + merkezi exception handling
- Rate limit ile yazma endpointlerinin korunmasi

### Gozlemlenebilirlik

- Serilog request logging
- Domain odakli bilgi/warn/error kayitlari
- `/health` endpointi ile DB baglanti saglik kontrolu

## 9) Build, Migration ve Calistirma

### Lokal backend

- `dotnet build src/YksTakipApp.Api/YksTakipApp.Api.csproj`
- `dotnet run --project src/YksTakipApp.Api/YksTakipApp.Api.csproj`

### Migration yonetimi

- Yeni migration:
  - `dotnet ef migrations add <Ad> --project src/YksTakipApp.Infra/YksTakipApp.Infra.csproj --startup-project src/YksTakipApp.Api/YksTakipApp.Api.csproj`
- Veritabanina uygula:
  - `dotnet ef database update --project src/YksTakipApp.Infra/YksTakipApp.Infra.csproj --startup-project src/YksTakipApp.Api/YksTakipApp.Api.csproj`

### Mobil

- `cd mobile`
- `npm install`
- `npx expo start`

## 10) Teknik Riskler ve Onerilen Sonraki Adimlar

- Planner icin daha guclu test kapsami (priority + edge-case kapasite dagilimi)
- API contract testleri (planner/topic integration)
- Uzun vadede read-model/analytics ayrimi (raporlama performansi icin)
- Kritk endpointlerde idempotency ve retriable komut modeli
- DecisionLog retention (90 gun sonra archive/purge job)

---

Son guncelleme: 11 Mayis 2026
