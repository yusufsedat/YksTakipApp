using System.Linq.Expressions;

namespace YksTakipApp.Core.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllForReadAsync();
        Task<T?> GetByIdForReadAsync(int id);
        Task<IEnumerable<T>> FindForReadAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);
        Task SaveChangesAsync();
    }
}
