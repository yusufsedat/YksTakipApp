namespace YksTakipApp.Api.DTOs
{
    /// <summary>
    /// Mobil kronometre entegrasyonu için uyumlu payload.
    /// UserId claim'den alınır; gönderilen UserId sadece doğrulama amacıyla opsiyoneldir.
    /// </summary>
    public class StudyTimeCreateApiRequest
    {
        public int? UserId { get; set; }
        public int DurationMinutes { get; set; }
        public string Subject { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.UtcNow;
    }
}
