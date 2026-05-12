# Beta Debug Kontrol Listesi (Faz 7 Öncesi)

Bu doküman Faz 6'da eklenen `PlannerDecisionLog`, `PlanQualityScore`, `FeatureFlag` ve `planner-debug` altyapıları üzerinden 6 operasyonel sorunun **tek admin endpoint çağrısıyla** nasıl cevaplandığını gösterir.

> Tüm endpointler `AdminOnly` policy'si arkasındadır. Admin JWT zorunludur.

## 1) En sık NoPlan reasonCode hangisi?

```
GET /admin/planner/decision-logs/stats?from=<utc>&to=<utc>
```

`topNoPlanReasons[0].reasonCode` → en sık görülen başarısızlık nedeni.

Örnek cevap:

```json
{
  "totalCalls": 132,
  "noPlanCount": 37,
  "topNoPlanReasons": [
    { "reasonCode": "noTopics", "count": 18 },
    { "reasonCode": "requiresGoal", "count": 12 },
    { "reasonCode": "noRecommendations", "count": 7 }
  ]
}
```

**Yorum kuralı:**

- `requiresGoal` baskınsa → onboarding/hedef kurulum akışı zayıf.
- `noTopics` baskınsa → konu seçim ekranı bypass ediliyor.
- `dailyCapacityTooLow` baskınsa → varsayılan kapasite çok düşük ayarlanmış.
- `noRecommendations` baskınsa → öneri motoru zayıf çıktı veriyor, ileri planda parametre ayarı gerekir.

## 2) QualityScore ortalaması kaç?

Aynı endpointin `avgQualityScore` alanı.

```json
{ "avgQualityScore": 64.3 }
```

**Eşikler:**

- `>= 60` → sağlıklı, ileri faza geçiş güvenli.
- `40-60` → uyarı; subjectBalance/repetitionSafety bileşenlerine bak.
- `< 40` → kritik; planner parametreleri/öneri çıktıları gözden geçirilmeli.

## 3) Warning/Risky plan oranı ne?

```json
{
  "qualityBandDistribution": {
    "healthy": 70,
    "warning": 20,
    "risky": 5
  }
}
```

Oran: `(warning + risky) / (healthy + warning + risky)`.

- `<= %20` → kabul edilebilir.
- `> %30` → Faz 7'ye geçmeden score formülünü ve recommendation çıktısını gözden geçir.

## 4) Priority isteği gerçekten plana giriyor mu?

Aynı endpoint:

```json
{
  "priorityFulfillmentRate": 0.86,
  "callsWithUnplacedPriority": 8
}
```

`priorityFulfillmentRate`, success satırlarında `priorityPlaced / priorityActive` ortalaması.

- `>= 0.90` → priority yerleşimi sağlıklı.
- `< 0.75` → kapasite yetmiyor ya da expired filter çok agresif, incele.

Detay için tek bir kullanıcıda:

```
GET /admin/planner/decision-logs/{id}
```

`breakdownJson.priority.skippedTopicIds` listesini gör.

## 5) Feature flag admin endpointleri güvenli çalışıyor mu?

Güvenlik kontrol noktaları (Faz 6.3'te uygulanmıştır):

- Tüm `/admin/feature-flags/*` rotaları `AdminOnly` policy zorunludur.
- `PUT /admin/feature-flags/{key}` çağrısında:
  - `rolloutPercentage` 0-100 aralığına clamp edilir.
  - `segment` whitelist'i ile sınırlıdır (`null`, `new_user`, `active_user`, `beta`).
- `POST /admin/feature-flags/{key}/overrides`:
  - Bilinmeyen flag → `404`.
  - `expiresAt < now` → `400` (geçmişe override yazılmaz).
- `IFeatureFlagService` precedence: **override > segment > rollout > global**. `userId == null` ise sadece `IsEnabled` döner; segment/rollout hesaplanmaz.

Manuel doğrulama:

```
PUT /admin/feature-flags/dynamicBuffer.enabled
{
  "isEnabled": true,
  "rolloutPercentage": 9999,
  "segment": "invalid_segment"
}
```

`400` dönmeli (clamp + whitelist).

## 6) planner-debug çıktısı tek bakışta problemi anlatıyor mu?

```
GET /admin/users/{userId}/planner-debug
```

Bölümler:

- `user` → role/segment
- `activeGoal` → hedef var mı, kapasite alanları dolu mu
- `capacity` → günlük/haftalık dağılım
- `latestPlan` → son haftanın task listesi
- `latestDecisionLog` → status + reasonCode + qualityBand
- `priorityRequests` → aktif/expired priority topic'leri
- `recentChurnEvents` → son 30 güne ait churn tetiklenmeleri
- `featureFlags` → kullanıcı için efektif flag durumu

**Tek bakışta tanı:**

| Belirti | Bakılacak bölüm |
| --- | --- |
| Plan gelmiyor | `latestDecisionLog.reasonCode` |
| Plan kalitesiz | `latestDecisionLog.qualityBand` + `qualityComponents` |
| Priority görünmüyor | `priorityRequests` ↔ `latestPlan` karşılaştırması |
| Notification gelmiyor | `featureFlags.notifications.proactive.enabled` |
| Onboarding takıldı | `activeGoal == null` + `user.createdAt` |

## Hızlı operasyon akışı

1. `GET /admin/planner/decision-logs/stats` → genel sağlık.
2. `topNoPlanReasons[0]` baskınsa kök neden ekranını incele.
3. `qualityBand` dağılımı bozuksa örnek bir log için detay: `GET /admin/planner/decision-logs/{id}`.
4. Tekil kullanıcı şikayetinde: `GET /admin/users/{userId}/planner-debug`.
5. Feature flag etkisinden şüpheleniyorsan `PUT /admin/feature-flags/{key}` ile kapat ve şikayetin sürüp sürmediğini kontrol et.

---

Son güncelleme: 11 Mayıs 2026
