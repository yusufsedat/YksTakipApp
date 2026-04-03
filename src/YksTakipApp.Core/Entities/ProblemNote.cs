namespace YksTakipApp.Core.Entities
{
    /// <summary>
    /// Çözemediğim soru notu — fotoğraf + etiket + çözümü öğrenildi mi.
    /// </summary>
    public class ProblemNote
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        /// <summary>data:image/jpeg;base64,... veya ham base64 (istemci data: ile kullanır).</summary>
        public string ImageBase64 { get; set; } = "";

        /// <summary>JSON dizi: ["Matematik","Türev"]</summary>
        public string TagsJson { get; set; } = "[]";

        public bool SolutionLearned { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}
