using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_MoveTopicsSeedToMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var seedRows = GetSeedRows();
            foreach (var row in seedRows)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO `Topics` (`Id`, `Name`, `Category`, `Subject`)
                    SELECT {row.Id}, '{SqlEscape(row.Name)}', '{SqlEscape(row.Category)}', '{SqlEscape(row.Subject)}'
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM `Topics`
                        WHERE `Category` = '{SqlEscape(row.Category)}'
                          AND `Subject` = '{SqlEscape(row.Subject)}'
                          AND `Name` = '{SqlEscape(row.Name)}'
                    );
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM `Topics`
                WHERE `Id` BETWEEN 10001 AND 10116;
                """);
        }

        private static string SqlEscape(string value) => value.Replace("'", "''");

        private static (int Id, string Category, string Subject, string Name)[] GetSeedRows() =>
        [
            (10001, "TYT", "Türkçe", "Sözcükte Anlam"),
            (10002, "TYT", "Türkçe", "Cümlede Anlam"),
            (10003, "TYT", "Türkçe", "Paragraf"),
            (10004, "TYT", "Türkçe", "Sözel Mantık"),
            (10005, "TYT", "Türkçe", "Ses Bilgisi"),
            (10006, "TYT", "Türkçe", "Yazım Kuralları"),
            (10007, "TYT", "Türkçe", "Noktalama İşaretleri"),
            (10008, "TYT", "Türkçe", "Dil Bilgisi (Temel düzey)"),
            (10009, "TYT", "Matematik", "Temel Kavramlar"),
            (10010, "TYT", "Matematik", "Sayılar"),
            (10011, "TYT", "Matematik", "Bölme ve Bölünebilme"),
            (10012, "TYT", "Matematik", "OBEB - OKEK"),
            (10013, "TYT", "Matematik", "Rasyonel Sayılar"),
            (10014, "TYT", "Matematik", "Basit Eşitsizlikler"),
            (10015, "TYT", "Matematik", "Mutlak Değer"),
            (10016, "TYT", "Matematik", "Üslü Sayılar"),
            (10017, "TYT", "Matematik", "Köklü Sayılar"),
            (10018, "TYT", "Matematik", "Çarpanlara Ayırma"),
            (10019, "TYT", "Matematik", "Oran - Orantı"),
            (10020, "TYT", "Matematik", "Problemler (Yaş, Kar-Zarar, İşçi, Hareket vb.)"),
            (10021, "TYT", "Matematik", "Kümeler"),
            (10022, "TYT", "Matematik", "Fonksiyon (Temel)"),
            (10023, "TYT", "Matematik", "Veri - Grafik - Tablo"),
            (10024, "TYT", "Geometri", "Doğruda Açılar"),
            (10025, "TYT", "Geometri", "Üçgenler"),
            (10026, "TYT", "Geometri", "Çokgenler"),
            (10027, "TYT", "Geometri", "Dörtgenler"),
            (10028, "TYT", "Geometri", "Çember ve Daire"),
            (10029, "TYT", "Geometri", "Katı Cisimler (Temel)"),
            (10030, "TYT", "Tarih", "Tarih Bilimine Giriş"),
            (10031, "TYT", "Tarih", "İlk Çağ Uygarlıkları"),
            (10032, "TYT", "Tarih", "İslamiyet Öncesi Türk Tarihi"),
            (10033, "TYT", "Tarih", "İslam Tarihi"),
            (10034, "TYT", "Tarih", "Osmanlı Tarihi"),
            (10035, "TYT", "Tarih", "Kurtuluş Savaşı"),
            (10036, "TYT", "Tarih", "Atatürk İlke ve İnkılapları"),
            (10037, "TYT", "Coğrafya", "Harita Bilgisi"),
            (10038, "TYT", "Coğrafya", "İklim"),
            (10039, "TYT", "Coğrafya", "Yer Şekilleri"),
            (10040, "TYT", "Coğrafya", "Nüfus ve Yerleşme"),
            (10041, "TYT", "Coğrafya", "Türkiye Coğrafyası"),
            (10042, "TYT", "Felsefe", "Felsefenin Konusu"),
            (10043, "TYT", "Felsefe", "Bilgi Felsefesi"),
            (10044, "TYT", "Felsefe", "Varlık Felsefesi"),
            (10045, "TYT", "Felsefe", "Ahlak Felsefesi"),
            (10046, "TYT", "Din Kültürü", "İslam Bilgisi"),
            (10047, "TYT", "Din Kültürü", "İnanç Konuları"),
            (10048, "TYT", "Din Kültürü", "Ahlak"),
            (10049, "TYT", "Din Kültürü", "Din ve Hayat"),
            (10050, "TYT", "Fizik", "Fizik Bilimine Giriş"),
            (10051, "TYT", "Fizik", "Madde ve Özellikleri"),
            (10052, "TYT", "Fizik", "Kuvvet ve Hareket"),
            (10053, "TYT", "Fizik", "Enerji"),
            (10054, "TYT", "Fizik", "Isı ve Sıcaklık"),
            (10055, "TYT", "Fizik", "Basınç"),
            (10056, "TYT", "Kimya", "Kimya Bilimi"),
            (10057, "TYT", "Kimya", "Atom ve Periyodik Sistem"),
            (10058, "TYT", "Kimya", "Kimyasal Türler"),
            (10059, "TYT", "Kimya", "Maddenin Halleri"),
            (10060, "TYT", "Kimya", "Karışımlar"),
            (10061, "TYT", "Biyoloji", "Canlıların Ortak Özellikleri"),
            (10062, "TYT", "Biyoloji", "Hücre"),
            (10063, "TYT", "Biyoloji", "Canlıların Sınıflandırılması"),
            (10064, "TYT", "Biyoloji", "Ekosistem"),
            (10065, "AYT", "AYT Matematik", "Matematik"),
            (10066, "AYT", "AYT Matematik", "Fonksiyonlar"),
            (10067, "AYT", "AYT Matematik", "Polinomlar"),
            (10068, "AYT", "AYT Matematik", "Dereceden Denklemler"),
            (10069, "AYT", "AYT Matematik", "Permütasyon - Kombinasyon"),
            (10070, "AYT", "AYT Matematik", "Binom"),
            (10071, "AYT", "AYT Matematik", "Olasılık"),
            (10072, "AYT", "AYT Matematik", "Logaritma"),
            (10073, "AYT", "AYT Matematik", "Diziler"),
            (10074, "AYT", "AYT Matematik", "Limit"),
            (10075, "AYT", "AYT Matematik", "Türev"),
            (10076, "AYT", "AYT Matematik", "İntegral"),
            (10077, "AYT", "AYT Geometri", "Geometri"),
            (10078, "AYT", "AYT Geometri", "Üçgenler (İleri düzey)"),
            (10079, "AYT", "AYT Geometri", "Analitik Geometri"),
            (10080, "AYT", "AYT Geometri", "Çember ve Daire"),
            (10081, "AYT", "AYT Geometri", "Katı Cisimler"),
            (10082, "AYT", "AYT Geometri", "Vektörler"),
            (10083, "AYT", "AYT Fizik", "Vektörler"),
            (10084, "AYT", "AYT Fizik", "Kuvvet ve Hareket"),
            (10085, "AYT", "AYT Fizik", "Elektrik ve Manyetizma"),
            (10086, "AYT", "AYT Fizik", "Dalgalar"),
            (10087, "AYT", "AYT Fizik", "Optik"),
            (10088, "AYT", "AYT Fizik", "Modern Fizik"),
            (10089, "AYT", "AYT Kimya", "Modern Atom Teorisi"),
            (10090, "AYT", "AYT Kimya", "Gazlar"),
            (10091, "AYT", "AYT Kimya", "Çözeltiler"),
            (10092, "AYT", "AYT Kimya", "Kimyasal Tepkimeler"),
            (10093, "AYT", "AYT Kimya", "Kimyasal Denge"),
            (10094, "AYT", "AYT Kimya", "Asit - Baz"),
            (10095, "AYT", "AYT Kimya", "Elektrokimya"),
            (10096, "AYT", "AYT Kimya", "Organik Kimya"),
            (10097, "AYT", "AYT Biyoloji", "Hücre Bölünmeleri"),
            (10098, "AYT", "AYT Biyoloji", "Kalıtım"),
            (10099, "AYT", "AYT Biyoloji", "Evrim"),
            (10100, "AYT", "AYT Biyoloji", "Sistemler (Sindirim, Dolaşım vb.)"),
            (10101, "AYT", "AYT Biyoloji", "Bitki Biyolojisi"),
            (10102, "AYT", "AYT Biyoloji", "Ekoloji"),
            (10103, "AYT", "AYT Edebiyat", "Edebiyat Akımları"),
            (10104, "AYT", "AYT Edebiyat", "Şiir Bilgisi"),
            (10105, "AYT", "AYT Edebiyat", "Tanzimat"),
            (10106, "AYT", "AYT Edebiyat", "Servet-i Fünun"),
            (10107, "AYT", "AYT Edebiyat", "Cumhuriyet Dönemi"),
            (10108, "AYT", "AYT Edebiyat", "Yazar - Eser"),
            (10109, "AYT", "AYT Tarih", "Türk-İslam Devletleri"),
            (10110, "AYT", "AYT Tarih", "Osmanlı Devleti"),
            (10111, "AYT", "AYT Tarih", "İnkılap Tarihi"),
            (10112, "AYT", "AYT Tarih", "Çağdaş Dünya"),
            (10113, "AYT", "AYT Coğrafya", "Türkiye Coğrafyası"),
            (10114, "AYT", "AYT Coğrafya", "Ekonomik Faaliyetler"),
            (10115, "AYT", "AYT Coğrafya", "Nüfus"),
            (10116, "AYT", "AYT Coğrafya", "Çevre ve Toplum")
        ];
    }
}
