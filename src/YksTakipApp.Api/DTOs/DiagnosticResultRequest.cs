namespace YksTakipApp.Api.DTOs;

public sealed class DiagnosticResultRequest
{
    /// <summary>passed | failed | skipped</summary>
    public string Result { get; set; } = "";
}
