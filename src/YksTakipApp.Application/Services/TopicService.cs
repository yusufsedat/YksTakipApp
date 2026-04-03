using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using System.Data.Common;

namespace YksTakipApp.Application.Services
{
    public class TopicService : ITopicService
    {
        private readonly IRepository<Topic> _topicRepo;
        private readonly IRepository<UserTopic> _userTopicRepo;

        public TopicService(IRepository<Topic> topicRepo, IRepository<UserTopic> userTopicRepo)
        {
            _topicRepo = topicRepo;
            _userTopicRepo = userTopicRepo;
        }

        public async Task AddTopicAsync(string name, string category, string subject = "")
        {
            await _topicRepo.AddAsync(new Topic { Name = name, Category = category, Subject = subject ?? "" });
            await _topicRepo.SaveChangesAsync();
        }

        public async Task<IEnumerable<Topic>> GetAllAsync()
            => await _topicRepo.GetAllAsync();

        public async Task<IEnumerable<UserTopic>> GetUserTopicsAsync(int userId)
            => await _userTopicRepo.FindAsync(ut => ut.UserId == userId);

        public async Task AddUserTopicAsync(int userId, int topicId)
        {
            // Konunun var olup olmadığını kontrol et
            var topic = (await _topicRepo.FindAsync(t => t.Id == topicId)).FirstOrDefault();
            if (topic == null)
                throw new InvalidOperationException($"Topic with id {topicId} not found.");

            // Kullanıcının bu konuyu zaten ekleyip eklemediğini kontrol et
            var existing = (await _userTopicRepo.FindAsync(ut => ut.UserId == userId && ut.TopicId == topicId)).FirstOrDefault();
            if (existing != null)
                throw new InvalidOperationException("Bu konu zaten kullanıcının listesinde mevcut.");

            // Yeni UserTopic oluştur (varsayılan durum: NotStarted)
            await _userTopicRepo.AddAsync(new UserTopic 
            { 
                UserId = userId, 
                TopicId = topicId, 
                Status = TopicStatus.NotStarted 
            });
            try
            {
                await _userTopicRepo.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Aynı anda iki istek gelirse "existing == null" kontrolünden sonra da çift insert olabilir.
                // MySQL duplicate key durumunu, endpoint'in yakalayacağı InvalidOperationException'a çeviriyoruz.
                var msg = ex.InnerException?.Message ?? ex.Message;
                if (msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("usertopics.PRIMARY", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Bu konu zaten kullanıcının listesinde mevcut.");
                }

                throw;
            }
        }

        public async Task UpdateUserTopicAsync(int userId, int topicId, TopicStatus status)
        {
            var existing = (await _userTopicRepo.FindAsync(ut => ut.UserId == userId && ut.TopicId == topicId)).FirstOrDefault();

            if (existing == null)
            {
                throw new InvalidOperationException("Bu konu kullanıcının listesinde bulunamadı. Önce konuyu eklemek için /user/topics/add endpoint'ini kullanın.");
            }

            existing.Status = status;
            _userTopicRepo.Update(existing);
            await _userTopicRepo.SaveChangesAsync();
        }

        public async Task RemoveUserTopicAsync(int userId, int topicId)
        {
            var existing = (await _userTopicRepo.FindAsync(ut => ut.UserId == userId && ut.TopicId == topicId)).FirstOrDefault();
            if (existing is null)
                throw new InvalidOperationException("Bu konu listenizde yok.");

            _userTopicRepo.Remove(existing);
            await _userTopicRepo.SaveChangesAsync();
        }
    }
}
