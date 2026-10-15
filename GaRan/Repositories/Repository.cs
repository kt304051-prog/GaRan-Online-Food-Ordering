using System.Linq.Expressions;
using GaRan.Data;
using Microsoft.EntityFrameworkCore;

namespace GaRan.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly GaRanDbContext _context;
    private readonly DbSet<T> _set;

    public Repository(GaRanDbContext context)
    {
        _context = context;
        _set = context.Set<T>();
    }

    public IQueryable<T> Query() => _set.AsQueryable();
    public Task<List<T>> GetAllAsync() => _set.ToListAsync();
    public Task<T?> GetByIdAsync(int id) => _set.FindAsync(id).AsTask();
    public async Task AddAsync(T entity) => await _set.AddAsync(entity);
    public void Update(T entity) => _set.Update(entity);
    public void Delete(T entity) => _set.Remove(entity);
    public Task<int> SaveAsync() => _context.SaveChangesAsync();
    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate) => _set.AnyAsync(predicate);
}
