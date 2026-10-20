using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Infrastructure.Persistence.Extensions;

/// <summary>
/// Các phương thức mở rộng IQueryable hỗ trợ phân trang, lọc ngày và sắp xếp an toàn bằng Whitelist.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Thực thi phân trang bất đồng bộ, trả về đối tượng PagedResult chuẩn.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return new PagedResult<T>(Array.Empty<T>(), 0, pageIndex, pageSize);
        }

        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, pageIndex, pageSize);
    }

    /// <summary>
    /// Thực thi phân trang bất đồng bộ, trả về Tuple (Items, TotalCount) để tương thích với các Repository hiện tại.
    /// </summary>
    public static async Task<(IReadOnlyList<T> Items, int TotalCount)> ToPagedTupleAsync<T>(
        this IQueryable<T> query,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return (Array.Empty<T>(), 0);
        }

        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Áp dụng cơ chế sắp xếp an toàn theo Whitelist Dictionary.
    /// Ngăn chặn hoàn toàn lỗi SQL Injection và Exception khi client truyền tên cột không tồn tại.
    /// </summary>
    public static IQueryable<T> ApplySafeSorting<T>(
        this IQueryable<T> query,
        IReadOnlyDictionary<string, Expression<Func<T, object>>> whitelistSortMap,
        string? sortBy,
        SortOrder sortOrder,
        Expression<Func<T, object>> defaultSortExpression,
        SortOrder defaultSortOrder = SortOrder.Desc)
    {
        Expression<Func<T, object>> selectedExpression = defaultSortExpression;
        var selectedOrder = defaultSortOrder;

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var normalizedKey = sortBy.Trim().ToLowerInvariant();
            if (whitelistSortMap.TryGetValue(normalizedKey, out var matchedExpression))
            {
                selectedExpression = matchedExpression;
                selectedOrder = sortOrder;
            }
        }

        return selectedOrder == SortOrder.Asc
            ? query.OrderBy(selectedExpression)
            : query.OrderByDescending(selectedExpression);
    }

    /// <summary>
    /// Áp dụng cơ chế sắp xếp an toàn theo Whitelist Dictionary với Enum định kiểu mạnh (Strongly-typed Enum).
    /// </summary>
    public static IQueryable<T> ApplySafeSorting<T, TSortBy>(
        this IQueryable<T> query,
        IReadOnlyDictionary<TSortBy, Expression<Func<T, object>>> whitelistSortMap,
        TSortBy? sortBy,
        SortOrder sortOrder,
        Expression<Func<T, object>> defaultSortExpression,
        SortOrder defaultSortOrder = SortOrder.Desc) where TSortBy : struct, Enum
    {
        Expression<Func<T, object>> selectedExpression = defaultSortExpression;
        var selectedOrder = defaultSortOrder;

        if (sortBy.HasValue && whitelistSortMap.TryGetValue(sortBy.Value, out var matchedExpression))
        {
            selectedExpression = matchedExpression;
            selectedOrder = sortOrder;
        }

        return selectedOrder == SortOrder.Asc
            ? query.OrderBy(selectedExpression)
            : query.OrderByDescending(selectedExpression);
    }

    /// <summary>
    /// Áp dụng lọc khoảng thời gian chuẩn hóa UTC cho trường kiểu DateTime non-nullable.
    /// </summary>
    public static IQueryable<T> ApplyDateRange<T>(
        this IQueryable<T> query,
        Expression<Func<T, DateTime>> dateSelector,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (fromDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            var parameter = dateSelector.Parameters[0];
            var greaterThanOrEqual = Expression.GreaterThanOrEqual(dateSelector.Body, Expression.Constant(startUtc));
            query = query.Where(Expression.Lambda<Func<T, bool>>(greaterThanOrEqual, parameter));
        }

        if (toDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            var parameter = dateSelector.Parameters[0];
            var lessThanOrEqual = Expression.LessThanOrEqual(dateSelector.Body, Expression.Constant(endUtc));
            query = query.Where(Expression.Lambda<Func<T, bool>>(lessThanOrEqual, parameter));
        }

        return query;
    }

    /// <summary>
    /// Áp dụng lọc khoảng thời gian chuẩn hóa UTC cho trường kiểu DateTime? nullable.
    /// </summary>
    public static IQueryable<T> ApplyDateRangeNullable<T>(
        this IQueryable<T> query,
        Expression<Func<T, DateTime?>> dateSelector,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (fromDate.HasValue)
        {
            DateTime? startUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            var parameter = dateSelector.Parameters[0];
            var hasValue = Expression.Property(dateSelector.Body, "HasValue");
            var value = Expression.Property(dateSelector.Body, "Value");
            var greaterThanOrEqual = Expression.GreaterThanOrEqual(value, Expression.Constant(startUtc.Value));
            var andAlso = Expression.AndAlso(hasValue, greaterThanOrEqual);
            query = query.Where(Expression.Lambda<Func<T, bool>>(andAlso, parameter));
        }

        if (toDate.HasValue)
        {
            DateTime? endUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            var parameter = dateSelector.Parameters[0];
            var hasValue = Expression.Property(dateSelector.Body, "HasValue");
            var value = Expression.Property(dateSelector.Body, "Value");
            var lessThanOrEqual = Expression.LessThanOrEqual(value, Expression.Constant(endUtc.Value));
            var andAlso = Expression.AndAlso(hasValue, lessThanOrEqual);
            query = query.Where(Expression.Lambda<Func<T, bool>>(andAlso, parameter));
        }

        return query;
    }
}
