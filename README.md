# YksTakipApp 🚀

YKS öğrencilerinin çalışma sürecini uçtan uca takip etmesini sağlayan full-stack uygulama.

## ✨ Öne Çıkan Özellikler
- **Kimlik Doğrulama:** JWT tabanlı auth ve rol yapısı (Admin/User).
- **Takip Sistemi:** Konu kataloğu ve kullanıcıya özel konu ilerleme takibi.
- **Zaman Yönetimi:** Günlük çalışma süresi kayıtları ve listeleme.
- **İstatistik:** TYT/AYT ve branş deneme sonuçları için analiz ekranları.
- **Planlama:** Haftalık ve aylık çalışma programı (schedule) yönetimi.
- **Mobil Arayüz:** React Native ile modern ve hızlı kullanıcı deneyimi.

## 🛠️ Teknik Yığın
- **Backend:** .NET 8 Minimal API, Clean Architecture, EF Core
- **Database:** MySQL
- **Auth/Security:** JWT, BCrypt, CORS, Rate Limiting
- **Mobile:** React Native (Expo), TypeScript, Expo Router
- **CI/CD:** GitHub Actions (Build/Test/Code Quality), Railway (Deploy)

## 📄 Lisans
Bu proje [MIT](LICENSE) lisansı ile paylaşılmaktadır.

## 🚀 Hızlı Başlangıç
### Backend
```bash
dotnet restore
dotnet build
dotnet run --project src/YksTakipApp.Api
```
### Mobile
```bash
cd mobile
npm install
npm run start
```
`mobile/.env.example` dosyasını `mobile/.env` olarak kopyalayıp `EXPO_PUBLIC_API_URL` ayarını geliştirme ortamına göre güncelleyebilirsiniz.

## 📱 Ekran Görüntüleri

<p align="center">
  <img src="screenshots/ana-sayfa_yks.jpg" width="200" alt="Ana Sayfa" />
  <img src="screenshots/konular_yks.jpg" width="200" alt="Konular" />
  <img src="screenshots/calismalar_yks.jpg" width="200" alt="Çalışmalar" />
  <img src="screenshots/denemeler_yks.jpg" width="200" alt="Denemeler" />
  <br><br>
  <img src="screenshots/program_yks.jpg" width="200" alt="Program" />
  <img src="screenshots/stats_yks.jpg" width="200" alt="İstatistikler" />
  <img src="screenshots/araclar_yks.jpg" width="200" alt="Araçlar" />
  <img src="screenshots/notebook_yks.jpg" width="200" alt="Notebook" />
</p>

