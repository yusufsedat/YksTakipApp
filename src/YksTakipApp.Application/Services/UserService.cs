using BCrypt.Net;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IRepository<User> _repository;

        public UserService(IRepository<User> repository)
        {
            _repository = repository;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            var users = await _repository.FindAsync(u => u.Email == email);
            return users.FirstOrDefault();
        }

        public async Task<User> RegisterAsync(string name, string email, string password)
        {
            var hashed = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = hashed,
                Role = "User"
            };

            await _repository.AddAsync(user);
            await _repository.SaveChangesAsync();
            return user;
        }

        public bool VerifyPassword(string password, string hash)
            => BCrypt.Net.BCrypt.Verify(password, hash);

        
         public async Task<User?> GetByIdAsync(int id)
        {
            var users = await _repository.FindAsync(u => u.Id == id);
            return users.FirstOrDefault();
        }
    }
}
