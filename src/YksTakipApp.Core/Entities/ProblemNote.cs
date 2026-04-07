namespace YksTakipApp.Core.Entities
{
    /// <summary>
    /// Çözemediğim soru notu — fotoğraf + etiket + çözümü öğrenildi mi.
    /// </summary>
    public class ProblemNote
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        /// <summary>Cloudinary secure URL veya eski kayıtlarda ham/base64 (geriye dönük).</summary>
        public string ImageUrl { get; set; } = "";

        /// <summary>Cloudinary public id (silme için). Eski satırlarda null olabilir.</summary>
        public string? ImagePublicId { get; set; }

        /// <summary>JSON dizi: ["Matematik","Türev"]</summary>
        public string TagsJson { get; set; } = "[]";

        public bool SolutionLearned { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}
