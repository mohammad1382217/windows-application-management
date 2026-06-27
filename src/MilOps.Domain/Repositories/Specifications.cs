using System.Linq.Expressions;
using MilOps.Domain.Common;

namespace MilOps.Domain.Repositories;

/// <summary>
/// A small, framework-agnostic specification base. Keeps the Domain free of
/// IQueryable while still allowing composable, strongly-typed queries.
/// </summary>
public class Specification<T> : ISpecification<T> where T : class
{
    public Expression<Func<T, bool>>? Criteria { get; protected set; }
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; protected set; }
    public Expression<Func<T, object>>? OrderByDescending { get; protected set; }
    public int? Take { get; protected set; }
    public int? Skip { get; protected set; }

    protected void AddInclude(Expression<Func<T, object>> include) => Includes.Add(include);
    protected void ApplyPaging(int skip, int take) { Skip = skip; Take = take; }
}
