using System.Linq.Expressions;

namespace GaRan.Repositories;

public interface IRepository<T> where T : class
{
    IQueryable<T> Query();
    Task<List<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task<int> SaveAsync();
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
}
