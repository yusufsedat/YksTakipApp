namespace YksTakipApp.Core.Models;

public sealed record ChurnSummaryDto(
    int ChurnTriggerCount,
    int ChurnedUserCount,
    double AvgChurnLatencyDays,
    int Trend7dCount,
    int Trend14dCount,
    int NewUserChurnCount,
    int ActiveUserChurnCount);

public sealed record FeedbackLoopUserDto(
    int UserId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int CompletedCount,
    int DeferredCount,
    int SkippedCount,
    int PriorityRequestCount,
    int ManualRegenerateCount,
    double DifficultyScore,
    double SatisfactionScore,
    string PrimaryReasonCode,
    bool IsNewUserSegment,
    bool IsActiveUserSegment);

public sealed record FeedbackLoopSummaryDto(
    DateOnly From,
    DateOnly To,
    int UserCount,
    double AvgDifficultyScore,
    double AvgSatisfactionScore,
    int HighDifficultyUserCount,
    int NewUserCount,
    int ActiveUserCount);
