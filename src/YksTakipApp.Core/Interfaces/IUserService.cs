using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User> RegisterAsync(string name, string email, string password);
        bool VerifyPassword(string password, string hash);
        Task<User?> GetByIdAsync(int id);
    }
}
