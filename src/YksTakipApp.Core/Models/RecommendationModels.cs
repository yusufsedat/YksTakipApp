namespace YksTakipApp.Core.Models;

public enum RecommendationType
{
    TopicStudy,
    Review,
    Practice
}

public enum RecommendationReasonCode
{
    WeakExamTrend,
    LowStudyTime,
    HighOsymWeight,
    MasteryRisk
}

public sealed record TopicPriorityDto(
    int TopicId,
    string TopicName,
    string SubjectName,
    int PriorityScore,
    string Reason,
    RecommendationType RecommendationType,
    RecommendationReasonCode ReasonCode,
    string ReasonShort,
    IReadOnlyDictionary<string, string>? ReasonMeta = null);
