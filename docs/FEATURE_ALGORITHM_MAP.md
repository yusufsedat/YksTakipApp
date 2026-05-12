# YksTakipApp Feature -> Algorithm Haritasi

Bu dokuman, sistemdeki ana feature'larin hangi algoritma/mantikla calistigini kisa ama net sekilde map eder.

## 1) Dinamik Planlayici (AI Weekly Planner)

- **Feature:** Haftalik/gunluk gorev uretimi (`ScheduleTask`)
- **Ana servis:** `DynamicPlannerService.GenerateWeeklyPlanAsync`
- **Algoritma/mantik:**
  - Gunluk kapasiteyi `%80 calisma + %20 buffer` ayirma
  - **Priority-first yerlesim:** `IsPriorityRequested == true` konular once yerlestirilir
    - Siralama: bugun -> yarin -> haftanin kalan gunleri
    - Her konu ilk uygun kapasiteye atanir
  - **Skor bazli dagitim:** Kalan kapasite recommendation skorlarina gore dagitilir
  - Gorev yerlestirme: kapasite uygun gunler icinde en cok bosluk olana atama (greedy/LPT benzeri)
  - Islenen priority konularin flag'i transaction icinde `false` cekilir

## 2) One Cek / Gundemime Al (On-Demand Priority)

- **Feature:** Konuyu acil olarak plana zorlama
- **Endpoint:** `POST /topics/{topicId}/request-priority`
- **Algoritma/mantik:**
  - `UserTopic.IsPriorityRequested = true`
  - Hemen planner regenerate tetikleme
  - Planner icinde priority-first kuralinin devreye girmesi

## 3) Oneri Motoru (Recommendation)

- **Feature:** Calisilacak konularin onceliklendirilmesi
- **Ana servis:** `RecommendationService`
- **Algoritma/mantik:**
  - Konu agirligi (OSYM agirligi), performans sinyali ve durum bilgisini birlestiren puanlama
  - Cikti: `TopicPriorityDto` listesi (`PriorityScore`, `Reason`, `RecommendationType`)

## 4) Adaptasyon Motoru

- **Feature:** Ogrenci performansina gore plan/konu davranisini ayarlama
- **Ana servis:** `AdaptationService`
- **Algoritma/mantik:**
  - Deneme ve diagnostic sonuclarini degerlendirme
  - Mastery confidence/status guncelleme
  - Gerekirse konu kilitleme/acma ve plan revizyonu tetikleme

## 5) Churn Koruma (Plan Bos Kalmasin)

- **Feature:** Haftalik planin tamamen bos kalmasini onleme
- **Ana servis:** `DynamicPlannerService.CheckAndTriggerChurnAsync`
- **Algoritma/mantik:**
  - Bugun planned study gorevi var mi kontrolu
  - Haftada herhangi bir study gorevi var mi kontrolu
  - Ikisi de yoksa otomatik haftalik plan uretimi

## 6) Istatistik Hesaplama

- **Feature:** Ozet, trend, ilerleme, deneme analizleri
- **Ana servis:** `StatsService`
- **Algoritma/mantik:**
  - Son 7 gun toplamlari (aggregation)
  - Haftalik karsilastirma (this week vs last week)
  - Deneme ortalama/trend hesaplari (time-series aggregation)
  - Brans/ders kirilimlari (group-by tabanli ozetleme)

## 7) Konu Durum Yonetimi

- **Feature:** Kullanici konu durum guncelleme (NotStarted/InProgress/Completed/NeedsReview)
- **Ana servis:** `TopicService.UpdateUserTopicAsync`
- **Algoritma/mantik:**
  - Durum gecisi + opsiyonel `learnedExternally` hizli mastery uygulamasi
  - Tutarlilik icin kullanici-konu varlik kontrolu

## 8) Study Time Senkronizasyonu

- **Feature:** Mobilde calisma suresi kaydi ve ag kesintisinde kayip olmamasi
- **Ana katman:** mobil `study sync` + backend `StudyTime` endpointleri
- **Algoritma/mantik:**
  - Offline queue (eventual sync)
  - Baglanti geldiginde toplu gonderim/retry
  - Cakisma riskini azaltan idempotent'e yakin istemci davranisi

## 9) Planner Decision Logging (Faz 6.1)

- **Feature:** Plan generation cagrilarinin gozlemlenebilirligi
- **Ana servisler:** `IPlannerDecisionContextBuilder`, `IPlannerDecisionLogger`
- **Algoritma/mantik:**
  - Her `GenerateWeeklyPlanAsync` cagrisi (success ve no-plan dahil) icin tek satir kayit
  - `Stopwatch` ile sure olcumu
  - Sinirli `BreakdownJson` snapshot (capacity/priority/recommendationSummary/perDayRemaining/qualityComponents)
  - Logger basarisiz olursa planner sonucu kaybetmez; structured `LogError` yazar

## 10) Plan Quality Score (Faz 6.2)

- **Feature:** Uretim aninda plan kalitesi
- **Ana servis:** `IPlanQualityScorer`
- **Algoritma/mantik:**
  - 6 bilesen agirlikli ortalama (toplam 100):
    - `CapacityFit` (0-20): planlanan/kapasite uyumu
    - `PriorityCoverage` (0-20): aktif priority topic'lerin ne kadari yerlesti
    - `WeaknessCoverage` (0-15): top recommendation oranlari
    - `SubjectBalance` (0-15): dersler arasi dengeli dagilim
    - `RepetitionSafety` (0-15): ayni gun ayni topic tekrari yokluk
    - `OverloadSafety` (0-15): gunluk overload riski yokluk
  - Band: `Healthy >= 60`, `Warning 40-59`, `Risky < 40`
  - Score `DecisionLog.QualityScore` + `QualityBand` alanlarina yazilir

## 11) Feature Flag Degerlendirme (Faz 6.3)

- **Feature:** Dinamik ozellik kontrolu
- **Ana servisler:** `IFeatureFlagService`, `IUserSegmentResolver`
- **Algoritma/mantik:**
  - Precedence: **user override > segment match > rollout percentage > global IsEnabled**
  - `userId == null` ise sadece global `IsEnabled` bakilir
  - Rollout: `StableBucket(userId, flagKey) % 100 < rolloutPercentage`
  - Segment: `new_user` / `active_user` / `beta` / `null`
  - Caching: `IMemoryCache` ile flag + segment kisa TTL

## 12) Admin Planner Debug Snapshot (Faz 6.4)

- **Feature:** Tek bakista kullanici planner durumu
- **Ana servis:** `IPlannerDebugReader`
- **Endpoint:** `GET /admin/users/{userId}/planner-debug`
- **Algoritma/mantik:**
  - User + activeGoal + capacity + segment + latestPlan + latestDecisionLog + priorityRequests + recentChurnEvents + featureFlags tek aggregate response
  - Tum bolumler `AsNoTracking` + minimal join

---

Son guncelleme: 11 Mayis 2026
