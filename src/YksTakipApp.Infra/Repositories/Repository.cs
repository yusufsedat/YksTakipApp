using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Infra.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DbContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(DbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task<IEnumerable<T>> GetAllForReadAsync() => await _dbSet.AsNoTracking().ToListAsync();

        public async Task<T?> GetByIdForReadAsync(int id)
        {
            // FindAsync tracking gerektirir; NoTracking ile alternatif
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
        }

        public async Task<IEnumerable<T>> FindForReadAsync(Expression<Func<T, bool>> predicate)
            => await _dbSet.AsNoTracking().Where(predicate).ToListAsync();

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public void Update(T entity) => _dbSet.Update(entity);

        public void Remove(T entity) => _dbSet.Remove(entity);

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
