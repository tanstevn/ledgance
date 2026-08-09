export type SortDirection = "Ascending" | "Descending";

export interface PaginatedRequest<T> {
  page?: number;
  pageSize?: number;
  sortBy?: Extract<keyof T, string>;
  sortDirection?: SortDirection;
  searchValue?: string;
}
