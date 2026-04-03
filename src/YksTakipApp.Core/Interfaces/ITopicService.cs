using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Interfaces
{
    public interface ITopicService
    {
        Task AddTopicAsync(string name, string category, string subject = "");
        Task<IEnumerable<Topic>> GetAllAsync();
        Task<IEnumerable<UserTopic>> GetUserTopicsAsync(int userId);
        Task AddUserTopicAsync(int userId, int topicId);
        Task UpdateUserTopicAsync(int userId, int topicId, TopicStatus status);
        Task RemoveUserTopicAsync(int userId, int topicId);
    }
}
