using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MilOps.Domain.Common;
using MilOps.Domain.Repositories;
using MilOps.Infrastructure.Db;

namespace MilOps.Infrastructure.Persistence;

/// <summary>
/// EF Core generic repository. Translates domain <see cref="ISpecification{T}"/>
/// into a query, applying criteria, includes, ordering, and paging.
/// </summary>
public sealed class EfRepository<T> : IRepository<T> where T : class
{
    private readonly MilOpsDbContext _db;
    private readonly DbSet<T> _set;
    public EfRepository(MilOpsDbContext db) { _db = db; _set = db.Set<T>(); }

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _set.FindAsync(new object?[] { id }, ct).AsTask()!;

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? spec = null, CancellationToken ct = default)
        => await ApplySpec(spec).ToListAsync(ct);

    public async Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default)
        => await ApplySpec(spec).FirstOrDefaultAsync(ct);

    public async Task<int> CountAsync(ISpecification<T>? spec = null, CancellationToken ct = default)
        => spec is null ? await _set.CountAsync(ct) : await ApplySpec(spec).CountAsync(ct);

    public async Task<bool> AnyAsync(ISpecification<T> spec, CancellationToken ct = default)
        => await ApplySpec(spec).AnyAsync(ct);

    public void Add(T entity) => _set.Add(entity);
    public void AddRange(IEnumerable<T> entities) => _set.AddRange(entities);
    public void Remove(T entity) => _set.Remove(entity);

    private IQueryable<T> ApplySpec(ISpecification<T>? spec)
    {
        var q = (IQueryable<T>)_set;
        if (spec is null) return q;
        if (spec.Criteria is not null) q = q.Where(spec.Criteria);
        foreach (var inc in spec.Includes) q = q.Include(inc);
        if (spec.OrderByDescending is not null) q = q.OrderByDescending(spec.OrderByDescending);
        else if (spec.OrderBy is not null) q = q.OrderBy(spec.OrderBy);
        if (spec.Skip.HasValue) q = q.Skip(spec.Skip.Value);
        if (spec.Take.HasValue) q = q.Take(spec.Take.Value);
        return q;
    }
}

/// <summary>EF Core UnitOfWork wrapping SaveChanges and transactions.</summary>
public sealed class EfUnitOfWork : IUnitOfWork, IDisposable
{
    private readonly MilOpsDbContext _db;
    public EfUnitOfWork(MilOpsDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try { await action(); await tx.CommitAsync(ct); }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    // The DbContext's lifetime is owned by DbContextAccessor and the DI scope —
    // NOT by the unit of work — so we must NOT dispose _db here (doing so caused
    // a double-dispose of the shared context). We still satisfy
    // IUnitOfWork : IAsyncDisposable, and we ALSO implement IDisposable so the
    // container can dispose us during a SYNCHRONOUS scope.Dispose() (the shell
    // disposes navigation scopes synchronously). Without IDisposable the
    // container throws "type only implements IAsyncDisposable".
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
