namespace YksTakipApp.Core.Models;

public sealed record NotificationPayload(
    string Type,
    string Title,
    string Message,
    IDictionary<string, string> Data);
