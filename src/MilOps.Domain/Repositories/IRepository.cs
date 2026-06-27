using System.Linq.Expressions;
using MilOps.Domain.Common;

namespace MilOps.Domain.Repositories;

/// <summary>
/// Generic repository contract. Read side returns tracked entities by default;
/// callers pass <paramref name="asNoTracking"/> for read-only queries. The
/// UnitOfWork pattern is used for transactional writes (see <see cref="IUnitOfWork"/>).
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? spec = null, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T>? spec = null, CancellationToken ct = default);
    Task<bool> AnyAsync(ISpecification<T> spec, CancellationToken ct = default);
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Remove(T entity);
}

/// <summary>Coordinates a single transaction across repositories (Commit/Rollback).</summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
}

/// <summary>Marks a class as a LINQ-based query specification.</summary>
public interface ISpecification<T> where T : class
{
    Expression<Func<T, bool>>? Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    int? Take { get; }
    int? Skip { get; }
}
