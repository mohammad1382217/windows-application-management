using System.Linq.Expressions;
using MilOps.Domain.Entities;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Soldiers;

/// <summary>
/// Composable query spec for soldiers: free-text search across name/codes,
/// optional health/active/department filters, and paging.
/// </summary>
public class SoldierSpecification : Specification<Soldier>
{
    public SoldierSpecification(SoldierSearchRequest f)
    {
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            Criteria = s =>
                s.FirstName.Value.Contains(term) ||
                s.LastName.Value.Contains(term) ||
                s.NationalCode.Value.Contains(term) ||
                s.PersonnelCode.Value.Contains(term);
        }

        if (f.HealthType.HasValue)
            Criteria = Predicate.And(Criteria, s => s.HealthType == f.HealthType.Value);

        if (f.IsActive.HasValue)
            Criteria = Predicate.And(Criteria, s => s.IsActive == f.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(f.Department))
            Criteria = Predicate.And(Criteria, s => s.DepartmentName.Contains(f.Department));

        if (!string.IsNullOrWhiteSpace(f.Rank))
            Criteria = Predicate.And(Criteria, s => s.Rank.Contains(f.Rank));

        if (f.EntryDateFrom.HasValue)
            Criteria = Predicate.And(Criteria, s => s.EntryDate >= f.EntryDateFrom.Value);
        if (f.EntryDateTo.HasValue)
            Criteria = Predicate.And(Criteria, s => s.EntryDate <= f.EntryDateTo.Value);

        if (f.ServiceStartFrom.HasValue)
            Criteria = Predicate.And(Criteria, s => s.ServiceStartDate >= f.ServiceStartFrom.Value);
        if (f.ServiceStartTo.HasValue)
            Criteria = Predicate.And(Criteria, s => s.ServiceStartDate <= f.ServiceStartTo.Value);

        if (f.ServiceEndFrom.HasValue)
            Criteria = Predicate.And(Criteria, s => s.ServiceEndDate >= f.ServiceEndFrom.Value);
        if (f.ServiceEndTo.HasValue)
            Criteria = Predicate.And(Criteria, s => s.ServiceEndDate <= f.ServiceEndTo.Value);

        OrderByDescending = s => s.Id;
        ApplyPaging(Math.Max(0, (f.Page - 1) * f.PageSize), f.PageSize);
    }
}

/// <summary>Expression composition helpers (AND / OR with parameter rebinding).</summary>
internal static class Predicate
{
    public static Expression<Func<T, bool>> And<T>(
        Expression<Func<T, bool>>? left, Expression<Func<T, bool>> right)
    {
        if (left is null) return right;
        var param = Expression.Parameter(typeof(T), "x");
        var body = Expression.AndAlso(
            Expression.Invoke(Rebind(left, param), param),
            Expression.Invoke(Rebind(right, param), param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static Expression<Func<T, bool>> Rebind<T>(Expression<Func<T, bool>> expr, ParameterExpression param)
    {
        // Replace the original parameter with the shared one for correct AND-composition.
        var visitor = new ParameterReplacer(expr.Parameters[0], param);
        return Expression.Lambda<Func<T, bool>>(visitor.Visit(expr.Body), param);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;
        public ParameterReplacer(ParameterExpression from, ParameterExpression to) { _from = from; _to = to; }
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : node;
    }
}
