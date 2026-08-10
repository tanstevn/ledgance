using Ledgance.Shared.Application.Abstractions;

namespace Ledgance.Shared.Application.Models {
    public class PaginatedRequest<T> : IQuery<PaginatedResult<T>> {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public SortDirection? SortDirection { get; set; }
        public string? SearchValue { get; set; }
    }

    public enum SortDirection {
        Ascending,
        Descending
    }
}
