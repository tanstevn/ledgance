using Ledgance.Shared.Application.Models;
using System.Collections;
using System.Linq.Expressions;

namespace Ledgance.Shared.Application.Extensions {
    public static class QueryableExtensions {
        public static async Task<PaginatedResult<T>> PaginateAsync<T>(
        this IQueryable<T> query,
        PaginatedRequest<T> paginationParameters) {
            var itemsPerPage = 0;
            const int defaultPageSize = 10;

            itemsPerPage = paginationParameters.PageSize;
            itemsPerPage = itemsPerPage < 1 ? defaultPageSize : itemsPerPage;

            var paginatedQuery = paginationParameters.PageSize > 1
                ? query.Skip((paginationParameters.Page - 1) * itemsPerPage)
                : query;

            var results = paginatedQuery.Take(itemsPerPage);

            var paginationResult = new PaginatedResult<T> {
                ItemsPerPage = paginationParameters.PageSize,
                PageNumber = paginationParameters.Page,
                Data = results.ToList(),
                TotalResultsCount = query.Count(),
            };

            paginationResult.TotalPages = (int)Math.Ceiling((decimal)paginationResult.TotalResultsCount / itemsPerPage);
            paginationResult.ResultsCount = paginationResult.Data.Count();

            return paginationResult;
        }

        public static IQueryable<T> IfNotEmptyThenWhere<T>(
            this IQueryable<T> query,
            object? filterValue,
            Expression<Func<T, bool>> predicate) =>
            filterValue switch {
                null => query,
                string stringFilterValue when string.IsNullOrWhiteSpace(stringFilterValue) => query,
                DateTime dateFilterValue when dateFilterValue == DateTime.MinValue => query,
                ICollection { Count: 0 } => query,
                Array { Length: 0 } => query,
                _ => query.Where(predicate)
            };

        public static IQueryable<T> OrderBy<T>(
            this IQueryable<T> query,
            string? key,
            bool isDescending,
            Dictionary<string, Expression<Func<T, object>>>? mappings = null,
            bool orderNullsLast = true) {
            if (string.IsNullOrWhiteSpace(key)) {
                return query;
            }

            if (mappings != null && mappings.TryGetValue(key, out var expr))
                return isDescending
                    ? query.OrderByDescending(expr)
                    : query.OrderBy(expr);

            var orderByMethod = isDescending
                ? nameof(Queryable.OrderByDescending)
                : nameof(Queryable.OrderBy);

            var thenByMethod = isDescending
                ? nameof(Queryable.ThenByDescending)
                : nameof(Queryable.ThenBy);

            var parameterExpression = Expression.Parameter(query.ElementType);
            var methodExpression = Expression.Property(parameterExpression, key);

            var isNullable = Nullable.GetUnderlyingType(methodExpression.Type) != null ||
                !methodExpression.Type.IsValueType;

            Expression? orderByNullableCall = null;
            var includeNullableOrder = orderNullsLast && isNullable;

            if (includeNullableOrder) {
                if (methodExpression.Type == typeof(string)) {
                    const string? stringMethod = nameof(string.IsNullOrWhiteSpace);

                    var stringIsNullExpression = Expression.Call(typeof(string),
                        stringMethod,
                        Array.Empty<Type>(),
                        methodExpression);

                    orderByNullableCall = Expression.Call(typeof(Queryable),
                        orderByMethod,
                        new[] { query.ElementType, stringIsNullExpression.Type },
                        query.Expression,
                        Expression.Quote(Expression.Lambda(stringIsNullExpression, parameterExpression)));
                }
                else {
                    var hasValueExpression =
                        Expression.Equal(methodExpression, Expression.Constant(null));

                    orderByNullableCall = Expression.Call(typeof(Queryable),
                        orderByMethod,
                        new[] { query.ElementType, hasValueExpression.Type },
                        query.Expression,
                        Expression.Quote(Expression.Lambda(hasValueExpression, parameterExpression)));
                }
            }

            var orderByCall = Expression.Call(typeof(Queryable),
                includeNullableOrder ? thenByMethod : orderByMethod,
                new[] { query.ElementType, methodExpression.Type },
                includeNullableOrder
                    ? orderByNullableCall! // Never null, due to `includeNullableOrder` check
                    : query.Expression,
                Expression.Quote(Expression.Lambda(methodExpression, parameterExpression)));

            return (IQueryable<T>)query.Provider.CreateQuery(orderByCall);
        }
    }
}
