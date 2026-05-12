# YksTakipApp Ozellik ve Islev Dokumani

Bu dokuman, uygulamanin kullaniciya sundugu tum temel fonksiyonlari urun bakis acisiyla detaylandirir.

## 1) Urun Konumlandirmasi

`YksTakipApp`, YKS ogrencisinin gunluk calisma kararlarini sadeleştiren bir "AI Dijital Koc" uygulamasidir.

Temel deger onerisi:

- Ne calismaliyim? -> Oneri ve dinamik plan
- Ne kadar ilerledim? -> Istatistik ve trend
- Nerede zorlandim? -> Adaptasyon + notlar
- Acil durum var mi? -> "One Cek / Gundemime Al" ozelligi

## 2) Ana Kullanici Yolculugu

1. Kullanici kayit/giris yapar
2. Hedefini belirler (universite, bolum, gunluk kapasite)
3. Konularini olusturur/takip eder
4. AI haftalik gorev plani uretir (`ScheduleTask`)
5. Kullanici gorevleri tamamlar/atlar
6. Deneme ve calisma kayitlari geldikce sistem adaptasyon yapar
7. Ozel durumlarda bir konuyu "one cek" ile aninda plana dahil eder

## 3) Ozellik Gruplari

## 3.1 Hesap ve Kimlik

- Kayit olma
- Giris yapma
- JWT tabanli oturum
- Refresh token ile sessiz oturum yenileme
- Profil ve temel kullanici ozet bilgileri

Kullanici degeri:

- Cihaz degisse de hesap surekliligi
- Oturum kopmadan stabil kullanim

## 3.2 Hedef Yonetimi ve Onboarding

- Hedef universite/bolum girisi
- TYT/AYT hedef netleri
- Gunluk calisma kapasitesi (`DailyAvailableMinutes`)
- Skip/aktif hedef durumu yonetimi

Kullanici degeri:

- AI planlama kapasiteyi ogrencinin gercek gunluk imkanina gore kurgular

## 3.3 Konu Katalogu ve Kullanici Konulari

- Global katalogdan konu goruntuleme
- Kullanici listesine konu ekleme/karma
- Konu durumu guncelleme
- Mastery/locked alanlariyla adaptif izleme

Kullanici degeri:

- Hangi konularin aktif takipte oldugu netlesir
- Kisisel konu haritasi olusur

## 3.4 One Cek / Gundemime Al (On-Demand Priority)

Bu ozellik, kullanicinin acil durumlarda AI skorunu beklemeden konu dayatmasini saglar.

Davranis:

- Kullanici konu icin "oncelik talebi" gonderir
- Sistem o konuyu `IsPriorityRequested = true` isaretler
- Planner hemen yeniden calisir
- Ilgili konu once bugun/yarin kapasitesine yerlestirilir
- Yerlesen kaydin flag'i otomatik `false` olur

Senaryo ornekleri:

- Okul yazilisi yaklasti
- Ogretmen odev verdi
- Ogrenci zayif oldugu konuyu bu hafta zorunlu eklemek istiyor

Kullanici degeri:

- AI kocluk modeli korunur ama ogrencinin manuel acil kontrol hakki vardir

## 3.5 Dinamik Planlama (AI Weekly Planner)

Planlama artik tamamen AI gorev modeli uzerindedir (`ScheduleTask`).

Kurallar:

- Gunluk kapasitenin `%80`i planli gorev, `%20`si buffer
- Priority konular once yerlestirilir
- Kalan kapasite recommendation skorlarina gore doldurulur
- Gorevler haftalik periyotta yeniden uretilebilir

Gorev aksiyonlari:

- Planned
- Completed
- Skipped
- Deferred

Kullanici degeri:

- Cok karmasik manuel program yapmadan net gunluk gorev listesi
- Duruma gore hizli yeniden planlama

## 3.6 Oneri Motoru

Sistem, calisilacak konu onceligini puanlar:

- Konu agirligi (OSYM agirlik vb.)
- Mevcut performans
- Mastery ve durum bilgileri

Kullaniciya cikan sonuc:

- Bugun/hafta icin calisma onceligi yuksek konular

## 3.7 Adaptasyon Motoru

Gelen performans sinyallerine gore plani ve konu durumlarini ayarlar:

- Deneme sonuclari
- Diagnostic test sonuclari
- Konu durum degisimleri

Kullanici degeri:

- Sabit plan yerine ogrencinin guncel seviyesine uyumlanan plan

## 3.8 Calisma Kaydi (Study Time)

- Tarih ve dakika bazli kayit
- Konu baglama (opsiyonel)
- Toplu/senkronizasyon destekli endpointler
- Mobilde ag yokken gecici kuyruklama

Kullanici degeri:

- Emek kaybolmaz, daha dogru ilerleme analizi olusur

## 3.9 Deneme Yonetimi

- TYT/AYT/Brans deneme girisi
- Toplam net ve ders kirilimlari
- Listeleme ve gecmis goruntuleme

Kullanici degeri:

- Sadece hisle degil, sayisal sonuc ile calisma yonu belirleme

## 3.10 Istatistik ve Ilerleme Analitigi

Saglanan gorunumler:

- Son 7 gun toplam dakika
- Haftalik karsilastirma
- Deneme serisi bilgileri
- Brans/ders bazli performans
- Trend ve ortalama netler

Kullanici degeri:

- "Calisiyorum ama gelisiyor muyum?" sorusuna somut cevap

## 3.11 Kumbara (Problem Notlari)

- Fotografli soru notu ekleme
- Etiketleme
- Cozuldumu takibi
- Soft delete

Kullanici degeri:

- Zorlandigi sorulari kaybetmeden tekrar donup calisma imkani

## 3.12 Araclar ve Ekran Deneyimi

Mobilde ana deneyim:

- Ozet
- Konular
- Araclar
- Denemeler
- Istatistik

Plan deneyimi sade:

- "Gunun Gorevleri" odakta
- Manuel/otomatik ayirimi yok
- Tek gorev modeli ile net akis

## 4) Roller ve Yetkilendirme

- `User`: kendi verisi uzerinde tam kullanim
- `Admin`: konu katalogu gibi yonetsel islemler

Kullanici guveni acisindan:

- Kim neyi degistirebilir net sekilde sinirlandirilmis

## 5) Is Kurali Ozetleri

- Minimum gunluk kapasite dogrulamasi
- Task status gecisleri
- Priority talebinin tek kullanimlik olmasi (flag reset)
- Rate limit ile yazma endpointlerinin korunmasi
- Validation hatalarinin tutarli donmesi

## 6) Uygulama Icinde One Cikan Değerler

- Sadelik: ogrenciye "ne yapacagini" net soyleyen plan
- Kontrol: acil durumda manuel one cekme hakki
- Kisisellestirme: hedefe, performansa ve davranisa uyum
- Sureklilik: kayit, trend, geri bildirim dongusu

## 7) Gelecek Ozellik Onerileri (Urun Perspektifi)

- Priority talebi icin sure/son tarih bilgisi (ornegin "3 gun oncelikli")
- Gorev bazli bildirim stratejileri
- Haftalik hedef tamamlama rozet sistemi
- Veli/mentor paylasimli rapor modu
- Daha derin adaptasyon aciklamalari ("neden bu konuyu onerdim?")

---

Son guncelleme: 5 Mayis 2026
