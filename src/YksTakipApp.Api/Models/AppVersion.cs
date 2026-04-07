namespace YksTakipApp.Api.Models;

public sealed class AppVersion
{
    public string Platform { get; set; } = "";
    public string MinimumVersion { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public string StoreUrl { get; set; } = "";
}
