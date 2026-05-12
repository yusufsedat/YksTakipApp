# YksTakipApp Faz Ozeti (1-6)

Bu dokuman, son gelistirme adimlarinda tamamlanan Faz 1-6 kapsamlarini teknik ve urun etkisi acisindan ozetler.

## Faz 1 - Veri Akisi ve Tutarlilik

- StudyTime ve ExamResult create akislari icin idempotency deseni eklendi.
- `Idempotency-Key` (header/body) desteklendi, replay durumunda duplicate insert engellendi.
- Veritabani seviyesinde `(UserId, ClientRequestId)` unique indexleri eklendi.
- Retry ve race condition durumlarinda guvenli geri donus davranisi saglandi.
- `/health` endpointi DB baglantisi + worker durumu + queue backlog metrikleriyle gelistirildi.

## Faz 2 - Planlayiciyi Dinamiklestirme

- Sabit buffer modeli kaldirildi, kullanici performansina gore dinamik buffer hesaplandi.
- Incremental planning modeli uygulandi:
  - Gecmis/completed gorevler korunur.
  - Sadece kalan aralik revize edilir.
- Priority talebi tek seferlik olmaktan cikarildi:
  - `PriorityRequestedAt`, `PriorityExpiresAt`, `PriorityResolvedAt` ile lifecycle yonetimi.
  - TTL suresi dolunca veya konu tamamlaninca kapanis.

## Faz 3 - Kapali Dongu Adaptasyon

- Kumbara (ProblemNotes) verisi planner'a review gorevi olarak enjekte edildi.
- Haftalik review task cap + de-dup kurallari eklendi.
- Planlanan sure vs gerceklesen sure farkindan kapasite multiplier uretilip bir sonraki plana yansitildi.
- AdaptationService daha agresif mastery guncellemesine alindi:
  - Dusuk/yuksek performansa farkli hizda confidence ve lock/unlock tepkisi.
  - Esikler options/config ile yonetilir hale getirildi.

## Faz 4 - Baglamsal Kullanici Etkilesimi

- Recommendation ciktilarina explainability alanlari eklendi:
  - `reasonCode`, `reasonShort`, `reasonMeta`
- Deterministic gerekce secimi:
  - weak exam trend, low study time, high osym weight, mastery risk
- Proaktif bildirim policy engine eklendi:
  - hedefe yaklasma, hedef tamamlama, dunu telafi mesajlari
  - gunluk dedup ve notification log persistence
- Kritik planner tetikleyicilerine retriable command/idempotent execution modeli eklendi:
  - command acquire/complete/fail yasam dongusu
  - replay'de kayitli response donusu

## Faz 5 - Beta Analizi ve Optimizasyon

- Churn event persistence modeli eklendi:
  - `UserPlannerChurnEvent` tablosu
  - hafta + reason duplicate korumasi (unique index)
- `CheckAndTriggerChurnAsync` icinde churn event uretimi ve context loglama eklendi.
- Analytics servis ve endpointleri eklendi:
  - `GET /analytics/churn/summary`
  - `GET /analytics/feedback-loop/summary`
  - `GET /analytics/feedback-loop/user/{userId}`
- Feedback-loop skorlamasi (deterministic):
  - DifficultyScore ve SatisfactionScore formulleri
  - PrimaryReasonCode ve temel segmentasyon (new vs active user)

## Faz 6 - Beta Debug & Decision Visibility

### 6.1 Planner Decision Logging

- `PlannerDecisionLog` entity'si (`Phase12_PlannerDecisionLog` migration) ile her plan generation cagrisi icin tek satir kayit.
- `IPlannerDecisionContextBuilder` sinirli JSON snapshot olusturur (`capacity`, `priority`, `recommendationSummary`, `perDayRemaining`, `qualityComponents`).
- `IPlannerDecisionLogger` resilient: hata aninda exception sizdirmaz, structured `LogError` yazar (userId/weekStart/reasonCode/correlationId/idempotencyKey).
- `DynamicPlannerService` her exit point'inde (`Success`, `RequiresGoal`, `DailyCapacityTooLow`, `NoTopics`, `NoRecommendations`) loglar.
- `recommendationSkippedByCapacityCount` ve `recommendationSkippedByDuplicateCount` ayri ayri sayilir.
- Admin endpointleri: `GET /admin/planner/decision-logs`, `GET /admin/planner/decision-logs/{id}`, `GET /admin/planner/decision-logs/stats`.

### 6.2 Plan Quality Score

- `PlanQualityScore` (0-100) + `PlanQualityBand` (`Healthy/Warning/Risky`).
- Alti bilesen: `CapacityFit`, `PriorityCoverage`, `WeaknessCoverage`, `SubjectBalance`, `RepetitionSafety`, `OverloadSafety`. Risk bilesenleri "Safety" olarak isimlendirildi: yuksek deger her zaman iyi.
- `IPlanQualityScorer` saf servistir; sadece in-memory veriden hesaplar.
- Score `DecisionLog.QualityScore` + `QualityBand` alanlarinda persist edilir; non-Healthy band'lerde warning log atilir.

### 6.3 Feature Flags ve Notification Preferences

- `FeatureFlag`, `UserFeatureFlagOverride`, `UserNotificationPreference` entity'leri (`Phase13_FeatureFlagsAndPreferences` migration).
- `IUserSegmentResolver` heuristik segment dondurur: `new_user` (hesap < 14 gun + son 7 gunde < 2 study tamamlanmis), `active_user` (son 7 gunde >= 4 tamamlanmis), `beta` (override), `null`.
- `IFeatureFlagService` precedence: **override > segment > rollout > global**. `userId == null` ise sadece global `IsEnabled` etkin; segment/rollout calismaz.
- Rollout deterministik hash bucket (`StableBucket`) ile.
- Idempotent flag seeding `DevDataSeeder.SeedFeatureFlagsAsync` icinde (`dynamicBuffer.enabled`, `aggressiveAdaptation.enabled`, `churnAutoTrigger.enabled`, `notifications.proactive.enabled`, `planner.reviewInjection.enabled`, `planner.suppression.enabled`).
- Admin endpointleri: `GET /admin/feature-flags`, `PUT /admin/feature-flags/{key}`, `POST /admin/feature-flags/{key}/overrides`, `DELETE /admin/feature-flags/{key}/overrides/{userId}`.

### 6.4 Admin Planner Debug Read API

- Tek aggregate endpoint: `GET /admin/users/{userId}/planner-debug`.
- `IPlannerDebugReader` su bolumleri toplar: `user`, `activeGoal`, `capacity`, `segment`, `latestPlan`, `latestDecisionLog`, `priorityRequests`, `recentChurnEvents`, `featureFlags`.
- UI gerekmiyor; raw JSON tek bakista problem tespiti icin yeterli.

### Beta Operasyon Kontrol Listesi (Faz 7 oncesi)

`docs/BETA_DEBUG_CHECKLIST.md` dosyasinda 6 soru tek endpoint cagrisina map edilmistir:

1. `GET /admin/planner/decision-logs/stats` → en sik reasonCode, ortalama quality, band dagilimi, priority placement orani.
2. Tekil tani icin `GET /admin/users/{userId}/planner-debug`.

## Operasyonel Notlar

- Tum fazlarda backward compatibility korunarak ilerlenmistir.
- Domain logic agirlikli olarak service katmaninda tutulmustur; endpointler ince birakilmistir.
- EF Core migrationlari faz bazli uretilmis ve index/unique tasarimi performans odakli tamamlanmistir.
- Build ve test adimlari her faz sonunda calistirilmis, hata durumlari duzeltilip yeniden dogrulanmistir.
