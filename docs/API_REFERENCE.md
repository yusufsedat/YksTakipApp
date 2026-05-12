# YksTakipApp API Referansı

Bu dokuman backend Minimal API'lerinin guncel referansıdır. Her endpoint icin: metod, yol, auth gereksinimi, rate limit, kısa body/query sekli ve davranis yer alir.

## Genel Konvansiyonlar

- **Base URL**: ortamına göre (`https://<host>`).
- **Auth**: `Authorization: Bearer <jwt>`. `[Authorized]` veya `RequireAuthorization` olan tüm endpointler JWT ister.
- **Roller**:
  - `AdminOnly` policy → sadece Admin
  - `UserOnly` policy → Authorized user
- **Rate Limit**:
  - `writes` → yazma endpointleri (POST/PUT/PATCH/DELETE üzerinde)
  - `login` → kimlik endpointleri (`/users/login`, `/users/refresh-token`)
- **Idempotency**: Yazma endpointlerinde `Idempotency-Key` header'ı veya body içinde `clientRequestId`. Replay'de orijinal status code korunur.
- **Correlation**: Her istek `X-Correlation-Id` header'ı ile izlenir; response header'ında da döner.
- **Hata gövdesi**: RFC 7807 ProblemDetails veya `{ "message": "..." }`.
- **Tarih formatı**: `DateOnly` → `YYYY-MM-DD`. `DateTime` → ISO 8601 UTC.

---

## 1. System

### `GET /`

- **Auth**: Yok
- Servis canlı mı kontrolü.

### `GET /health`

- **Auth**: Yok
- DB bağlantısı, worker heartbeat ve queue backlog metrikleri.

### `GET /dbtest`

- **Auth**: Yok
- Sadece `Development` ortamında aktif. DB CanConnect testi.

---

## 2. Users

### `POST /users/register`

- **Auth**: Yok
- **Body**: `{ name, email, password }`
- `400` → email zaten kayıtlı.

### `POST /users/login`

- **Auth**: Yok | **RateLimit**: `login`
- **Body**: `{ email, password }`
- **Response**: `{ token, refreshToken, user }`

### `POST /users/refresh-token` ve `POST /refresh-token` (legacy)

- **Auth**: Yok | **RateLimit**: `login`
- **Body**: `{ refreshToken }`
- **Response**: `{ token, refreshToken, user }` | `401` expired/invalid

### `GET /users/me`

- **Auth**: Authorized
- Profil + topic listesi + son 7 gün study + exam streak.

---

## 3. Topics

### `POST /topics`

- **Auth**: AdminOnly | **RateLimit**: `writes`
- **Body**: `{ name, category, subject }`
- Global konu katalogu mutasyonu; topic cache invalidate edilir.

### `GET /topics`

- **Auth**: Yok
- **Query**: `page` (default 1), `pageSize` (1-500, default 20), `sort` (`name|-name|category|-category`)
- Topic katalogu (24h IMemoryCache).

### `GET /topics/{topicId}/progress`

- **Auth**: Authorized
- Kullanıcının topic için mastery, IsLocked, son aktivite bilgisi.

### `GET /user/topics`

- **Auth**: Authorized
- Kullanıcının `UserTopic` listesi (status, mastery, priority).

### `POST /user/topics/add`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ topicId }`

### `POST /user/topics/update`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ topicId, status, learnedExternally? }`
- `status` ∈ `NotStarted | InProgress | Completed | NeedsReview`.

### `DELETE /user/topics/{topicId}`

- **Auth**: Authorized | **RateLimit**: `writes`
- Sadece kullanıcının `UserTopic` kaydını siler; global katalogtan etkilemez.

### `POST /topics/{topicId}/request-priority`

- **Auth**: Authorized | **RateLimit**: `writes` | **Idempotent**
- Konuyu priority işaretler ve aynı çağrıda haftalık planı yeniden üretir.
- `404` → UserTopic bulunamadı.
- **Response**: `{ message, plan, tasks[] }`

---

## 4. Goals

### `GET /users/goals/status`

- **Auth**: Authorized
- **Response**: `{ hasActiveGoal, canSkip, currentGoal | null }`

### `POST /users/goals`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ targetUniversity, targetDepartment, targetTytNet?, targetAytNet?, dailyAvailableMinutes }`
- Immutable goal history; aktif sürümü günceller.

### `POST /users/goals/skip`

- **Auth**: Authorized | **RateLimit**: `writes`
- Onboarding'i atlama sayacı.
- `403` → skip limiti dolmuş. `409` → aktif hedef varken skip kullanılamaz.

---

## 5. Planner

### `POST /planner/generate`

- **Auth**: Authorized | **RateLimit**: `writes` | **Idempotent**
- **Body**: `{ startDate: "YYYY-MM-DD", clientRequestId? }`
- **Response (200)**: `{ status: "Success", tasks: [...] }`
- **Response (422)**: `{ status: "NoPlanGenerated", reasonCode: "requiresGoal", message }`
- **Response (200, no-plan)**: `{ status, reasonCode, message, ... }` — `dailyCapacityTooLow | noTopics | noRecommendations`
- Tüm çağrılar `PlannerDecisionLog`'a yazılır.

### `GET /planner/weekly`

- **Auth**: Authorized
- **Query**: `start`, `end` (`YYYY-MM-DD`, en fazla 14 günlük aralık)
- Çağrı içinde `CheckAndTriggerChurnAsync` koşar.

### `PATCH /planner/tasks/{taskId}/status`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ status }` — `Planned | Completed | Skipped | Deferred`
- `Completed` study task ise topic'in priority flag'i otomatik resolve edilir.
- DiagnosticTest tipinde özel akış (`IAdaptationService.RecordDiagnosticTestResultAsync`).

---

## 6. StudyTime

### `POST /studytime/add`

- **Auth**: Authorized | **RateLimit**: `writes` | **Idempotent**
- **Body**: `{ date, durationMinutes, topicId?, clientRequestId? }`

### `POST /studytime/create`

- **Auth**: Authorized | **RateLimit**: `writes` | **Idempotent**
- Kronometre akışı; aynı gün/konu kayıtlarını birleştirir. `add` ile aynı body.
- **Response**: `{ message, replay, item }`

### `POST /studytime/bulk-create`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ items: StudyTimeRequest[] }` (max 200)
- Offline kuyruk için toplu sync.
- **Response**: `{ savedCount, failedCount, failedIndexes }`

### `POST /api/studytimes`

- **Auth**: Authorized | **RateLimit**: `writes` | **Idempotent**
- Eski mobil istemci uyumluluğu. `subject` adıyla `Topic` join'i yapar.
- **Body**: `{ userId?, durationMinutes, subject, date, clientRequestId? }`

### `GET /studytime/list`

- **Auth**: Authorized
- **Query**: `page` (default 1), `pageSize` (1-100, default 20)
- Tarihe göre azalan sıralı.

---

## 7. Exams

### `POST /exam/add`

- **Auth**: Authorized | **RateLimit**: `writes` | **Idempotent**
- **Body**: `ExamResultRequest`
  - `examName, examType (TYT|AYT|BRANS), subject?, date, netTyt, netAyt, durationMinutes?, difficulty?, errorReasons?, details: [{ subject, correct, wrong, blank }]`

### `GET /exam/list`

- **Auth**: Authorized
- **Query**: `page`, `pageSize` (1-100), `sort` (`date|-date|name|-name`), `type` (filter)
- Her item içinde `examDetails[]`.

### `DELETE /exam/delete/{id}`

- **Auth**: Authorized
- Kendi kayıtlarından siler.

---

## 8. Recommendations

### `GET /recommendations/today`

- **Auth**: Authorized
- En fazla 5 konu; `priorityScore`, `reasonCode`, `reasonShort`, `reasonMeta` içerir. Yazma yapmaz.

---

## 9. Adaptation

### `POST /adaptation/evaluate-performance`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ topicId, recentExamScorePercent (0-100) }`
- Düşük skor + `learnedExternally` koşulu varsa otomatik DiagnosticTest görevi planlar.
- `204 NoContent`

### `POST /adaptation/diagnostic-tasks/{taskId}/result`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ result: "passed" | "failed" | "skipped" }`
- **Response**: `{ outcome, task }`

---

## 10. ProblemNotes (Kumbara)

### `GET /problem-notes/list`

- **Auth**: Authorized
- Tag + çözüm bilgisi ile birlikte.

### `POST /problem-notes/add`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ imageBase64, tags[], solutionLearned }`

### `PUT /problem-notes/{id}`

- **Auth**: Authorized | **RateLimit**: `writes`
- **Body**: `{ tags[], solutionLearned, imageBase64? }`

### `DELETE /problem-notes/{id}`

- **Auth**: Authorized | **RateLimit**: `writes`

---

## 11. Stats

Hepsi `RequireAuthorization` + `/stats` veya `/exam/stats` group altında.

### `GET /stats/summary`

Çalışma, konu ve sınav verilerinden özet metrikler.

### `GET /stats/weekly`

Son 7 günün dağılımı.

### `GET /stats/progress`

Konu tamamlama + zaman içindeki gelişim.

### `GET /stats/wins`

Motivasyon başarı listesi.

### `GET /exam/stats/tyt`

TYT performans metrikleri.

### `GET /exam/stats/ayt`

AYT performans metrikleri.

### `GET /exam/stats/brans`

Branş bazlı analiz.

---

## 12. Notifications

### `POST /notifications/preview`

- **Auth**: Authorized | **RateLimit**: `writes`
- Bugüne ait tekil bildirim payload listesini döner ve `UserNotificationLog` yazar (dedup garantili).
- **Response**: `NotificationPayload[]` — `{ type, message, payload }`

---

## 13. Analytics

Hepsi `RequireAuthorization`.

### `GET /analytics/churn/summary?from=&to=`

Aralıkta kayıtlı `UserPlannerChurnEvent` özet.

### `GET /analytics/feedback-loop/summary?from=&to=`

DifficultyScore + SatisfactionScore agregat + primary reason.

### `GET /analytics/feedback-loop/user/{userId}?from=&to=`

Tek kullanıcının feedback-loop metriği.

> `from` opsiyonel, default `to - 13` gün. `to` default bugün.

---

## 14. AppConfig

### `GET /api/app-config/check-version?platform=ios|android`

- **Auth**: Yok
- Minimum versiyon + zorunlu güncelleme bayrağı.

---

## 15. Admin / Planner (Faz 6 Debug)

Tümü `AdminOnly` policy ister.

### `GET /admin/planner/decision-logs`

- **Query**: `userId?, from?, to?, reasonCode?, take (1-100, default 50), skip (0-10000, default 0)`
- **Response**: `PlannerDecisionLogSummaryDto[]`
  - `id, userId, weekStart, weekEnd, status, reasonCode, taskCountTotal, qualityScore, qualityBand, durationMs, createdAt`

### `GET /admin/planner/decision-logs/{id}`

- **Response**: `PlannerDecisionLogDetailDto` (`breakdownJson` dahil)

### `GET /admin/planner/decision-logs/stats`

- **Query**: `from?: DateTime`, `to?: DateTime` (UTC)
- **Response**:
  ```json
  {
    "totalCalls": 0,
    "successCount": 0,
    "noPlanCount": 0,
    "topNoPlanReasons": [{ "reasonCode": "noTopics", "count": 0 }],
    "avgQualityScore": null,
    "qualityBandDistribution": { "healthy": 0, "warning": 0, "risky": 0 },
    "priorityFulfillmentRate": null,
    "callsWithUnplacedPriority": 0
  }
  ```
- Faz 7 hazırlık kontrol listesi için tek çağrı.

### `GET /admin/users/{userId}/planner-debug`

- **Response**: `PlannerDebugSnapshot` — `user, activeGoal, capacity, segment, latestPlan, latestDecisionLog, priorityRequests, recentChurnEvents, featureFlags`
- `404` → kullanıcı yok.

---

## 16. Admin / Feature Flags (Faz 6.3)

Tümü `AdminOnly` policy ister. Group prefix: `/admin/feature-flags`.

### `GET /admin/feature-flags`

- **Query**: `take (1-100, default 50), skip (0-10000)`
- **Response**: `FeatureFlagDto[]` — `{ key, isEnabled, description, rolloutPercentage, segment, createdAt, updatedAt }`

### `PUT /admin/feature-flags/{key}`

- **Body**: `{ isEnabled?, rolloutPercentage? (0-100), segment?, description? }`
- Validasyon:
  - `segment` boş veya `new_user | active_user | beta`
  - `rolloutPercentage` 0-100
- `404` → flag yok.

### `POST /admin/feature-flags/{key}/overrides`

- **Body**: `{ userId, isEnabled, expiresAt? }`
- Upsert davranışı; `204 NoContent`.
- `404` → flag yok.

### `DELETE /admin/feature-flags/{key}/overrides/{userId}`

- `204 NoContent` veya `404`.

---

## Reason Code Sözlüğü

### `PlanGenerationReasonCode`

| Code | HTTP | Anlam |
| --- | --- | --- |
| `requiresGoal` | 422 | Aktif hedef yok |
| `dailyCapacityTooLow` | 200 | workingDaily < 30 dk |
| `noTopics` | 200 | UserTopic yok |
| `noRecommendations` | 200 | Topic var ama plana giremedi |

### `PlanQualityBand`

| Band | Aralık |
| --- | --- |
| Healthy | ≥ 60 |
| Warning | 40-59 |
| Risky | < 40 |

### `ScheduleTaskStatus`

`Planned | Completed | Skipped | Deferred`

### `TaskType`

`Study | Review | DiagnosticTest`

---

## Idempotency Kuralları

- Yazma endpointlerinde:
  - `Idempotency-Key` header'ı (tercih edilen)
  - Veya body içinde `clientRequestId`
- Aynı `userId + operation + key` kombinasyonu DB unique index ile korunur.
- Replay durumunda orijinal HTTP status ve response döner (örn. `RequiresGoal` 422 aynen replay edilir).

---

Son güncelleme: 12 Mayıs 2026
