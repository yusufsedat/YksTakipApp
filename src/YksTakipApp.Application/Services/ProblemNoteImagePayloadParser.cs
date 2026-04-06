namespace YksTakipApp.Application.Services;

internal static class ProblemNoteImagePayloadParser
{
    /// <summary>data:image/...;base64,... veya ham base64 → bayt + içerik türü.</summary>
    public static (byte[] Bytes, string ContentType) Parse(string raw)
    {
        var t = raw.Trim();
        if (t.Length == 0)
            throw new ArgumentException("Görüntü verisi boş.", nameof(raw));

        if (t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var semi = t.IndexOf(';', StringComparison.Ordinal);
            if (semi < 5)
                throw new ArgumentException("Geçersiz data URL.", nameof(raw));
            var mime = t[5..semi];
            var base64Marker = "base64,";
            var idx = t.IndexOf(base64Marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                throw new ArgumentException("data URL içinde base64 bulunamadı.", nameof(raw));
            var b64 = t[(idx + base64Marker.Length)..].Replace("\n", "").Replace("\r", "");
            return (Convert.FromBase64String(b64), string.IsNullOrWhiteSpace(mime) ? "image/jpeg" : mime);
        }

        var plain = t.Replace("\n", "").Replace("\r", "");
        return (Convert.FromBase64String(plain), "image/jpeg");
    }
}
