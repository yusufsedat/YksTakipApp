using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Models;

public enum PlanGenerationStatus
{
    Success,
    NoPlanGenerated
}

public enum PlanGenerationReasonCode
{
    None = 0,
    RequiresGoal,
    DailyCapacityTooLow,
    /// <summary>Kullanıcının takip ettiği konu yok; UI konu seçim ekranına yönlendirmeli.</summary>
    NoTopics,
    /// <summary>Konular var ama öneri/priority/preserved görev üretilemedi; UI bilgilendirme ekranı basmalı.</summary>
    NoRecommendations
}

/// <summary>
/// Plan üretim sonucu. Boş plan dönüldüğünde <see cref="ReasonCode"/> ve UI'ın anlamlı ekran basabilmesi için
/// <see cref="Message"/> dolu gelir.
/// </summary>
public sealed class PlanGenerationResult
{
    public PlanGenerationStatus Status { get; init; } = PlanGenerationStatus.Success;
    public PlanGenerationReasonCode ReasonCode { get; init; } = PlanGenerationReasonCode.None;
    public string? Message { get; init; }
    public IReadOnlyList<ScheduleTask> Tasks { get; init; } = Array.Empty<ScheduleTask>();
    public int? CurrentMinutes { get; init; }
    public int? MinimumRequiredMinutes { get; init; }

    public static PlanGenerationResult Success(IReadOnlyList<ScheduleTask> tasks) =>
        new() { Status = PlanGenerationStatus.Success, Tasks = tasks };

    public static PlanGenerationResult RequiresGoal() =>
        new()
        {
            Status = PlanGenerationStatus.NoPlanGenerated,
            ReasonCode = PlanGenerationReasonCode.RequiresGoal,
            Message = "Akıllı plan oluşturmak için önce hedef ve günlük çalışma süresi belirle."
        };

    public static PlanGenerationResult DailyCapacityTooLow(int currentMinutes, int minimumMinutes) =>
        new()
        {
            Status = PlanGenerationStatus.NoPlanGenerated,
            ReasonCode = PlanGenerationReasonCode.DailyCapacityTooLow,
            Message = "Günlük çalışma kapasiten plan üretmek için yetersiz.",
            CurrentMinutes = currentMinutes,
            MinimumRequiredMinutes = minimumMinutes
        };

    public static PlanGenerationResult NoTopics() =>
        new()
        {
            Status = PlanGenerationStatus.NoPlanGenerated,
            ReasonCode = PlanGenerationReasonCode.NoTopics,
            Message = "Plan oluşturmak için önce takip etmek istediğin konuları seç."
        };

    public static PlanGenerationResult NoRecommendations() =>
        new()
        {
            Status = PlanGenerationStatus.NoPlanGenerated,
            ReasonCode = PlanGenerationReasonCode.NoRecommendations,
            Message = "Şu an plan üretmek için yeterli sinyal yok; bir kaç çalışma kaydından sonra öneriler oluşur."
        };
}
