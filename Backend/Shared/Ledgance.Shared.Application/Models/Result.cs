namespace Ledgance.Shared.Application.Models {
    public class Result<TData> {
        public bool Successful { get; set; }
        public TData? Data { get; set; }
        public IEnumerable<string>? Errors { get; set; }

        public static Result<TData> Success(TData data) {
            return new() {
                Data = data,
                Successful = true
            };
        }

        public static Result<TData> Error(string error, TData? data = default) {
            return new() {
                Errors = new List<string> {
                    error
                },
                Successful = false,
                Data = data
            };
        }

        public static Result<TData> MultipleErrors(IEnumerable<string> errors) {
            return new() {
                Errors = errors,
                Successful = false
            };
        }
    }

    public class PaginatedResult<T> : Result<T> {
        public int PageNumber { get; set; }
        public int ItemsPerPage { get; set; }
        public int ResultsCount { get; set; }
        public int TotalResultsCount { get; set; }
        public int TotalPages { get; set; }
        public new IEnumerable<T> Data { get; set; } = default!;

        public static PaginatedResult<T> Success(IEnumerable<T> data) {
            return new() {
                Data = data,
                Successful = true
            };
        }

        public new static PaginatedResult<T> Error(string error) {
            return new() {
                Errors = new List<string> {
                    error
                },
                Successful = false,
                Data = []
            };
        }

        public new static PaginatedResult<T> MultipleErrors(IEnumerable<string> errors) {
            return new() {
                Errors = errors,
                Successful = false
            };
        }
    }
}
