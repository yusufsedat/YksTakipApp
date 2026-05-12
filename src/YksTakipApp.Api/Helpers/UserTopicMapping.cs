using System.Text.Json;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Entities;

namespace YksTakipApp.Api.Helpers;

public static class UserTopicMapping
{
    public static UserTopicResponseDto ToResponseDto(UserTopic ut) =>
        new()
        {
            UserId = ut.UserId,
            TopicId = ut.TopicId,
            Status = (int)ut.Status,
            MasteryStatus = JsonNamingPolicy.CamelCase.ConvertName(ut.MasteryStatus.ToString()),
            MasteryConfidence = ut.MasteryConfidence,
            IsLocked = ut.IsLocked
        };
}
