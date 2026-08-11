import { PaginatedRequest } from "@/types/request";
import { PaginatedResult, Result, ResultErrors } from "@/types/result";
import { get, post, postForm, del, put } from "@/util/http";

import {
  QueryClient,
  UseMutationOptions,
  UseQueryOptions,
  keepPreviousData,
  useInfiniteQuery,
  useMutation,
  useQuery,
} from "@tanstack/react-query";

export type MutationType = "post" | "put" | "delete";

export const DefaultPagination = {
  page: 1,
};

const usePaginatedQuery = <T>(
  url: string,
  params: PaginatedRequest<T> & Record<string, unknown>,
  options?: Omit<UseQueryOptions<PaginatedResult<T>, ResultErrors>, "queryKey">,
) => {
  const formattedUrl = formatFullUrl(url);

  return useQuery<PaginatedResult<T>, ResultErrors>({
    queryKey: [url, params],
    queryFn: async () => {
      const result = await get<PaginatedResult<T>>(formattedUrl, params);
      return result.successful ? result : Promise.reject(result.errors);
    },
    // Page and filter changes swap the query key; keeping the previous rows on screen while
    // the next page loads turns pagination into a content swap instead of a skeleton flash.
    placeholderData: keepPreviousData,
    ...options,
  });
};

/**
 * Pages a list endpoint one page at a time and keeps the pages loaded — the shape the card
 * grids use, where scrolling to the end asks the API for the next page rather than pulling the
 * whole table up front.
 */
const useApiInfiniteQuery = <T>(
  url: string,
  params: PaginatedRequest<T> & Record<string, unknown> = {},
  options?: { queryKey?: unknown[]; enabled?: boolean },
) => {
  const formattedUrl = formatFullUrl(url);

  return useInfiniteQuery<
    PaginatedResult<T>,
    ResultErrors,
    { pages: PaginatedResult<T>[]; pageParams: number[] },
    unknown[],
    number
  >({
    queryKey: options?.queryKey ?? [url, params],
    enabled: options?.enabled,
    initialPageParam: 1,
    queryFn: async ({ pageParam }) => {
      const result = await get<PaginatedResult<T>>(formattedUrl, {
        ...params,
        page: pageParam,
      });
      return result.successful ? result : Promise.reject(result.errors);
    },
    getNextPageParam: (last) =>
      last.pageNumber < last.totalPages ? last.pageNumber + 1 : undefined,
    // A search-term change swaps the query key; the grid keeps showing the previous cards
    // until the filtered set arrives.
    placeholderData: keepPreviousData,
  });
};

const prefetchPaginatedQuery = async <T>(
  url: string,
  params: PaginatedRequest<T>,
  options?: UseQueryOptions<PaginatedResult<T>, ResultErrors>,
) => {
  const queryClient = new QueryClient();

  const formattedUrl = formatFullUrl(url);

  await queryClient.prefetchQuery<PaginatedResult<T>, ResultErrors>({
    queryKey: options?.queryKey ?? [url, params],
    queryFn: async () => {
      const result = await get<PaginatedResult<T>>(formattedUrl);
      return result.successful ? result : Promise.reject(result.errors);
    },
    ...options,
  });

  return queryClient;
};

const prefetchApiQuery = async <T>(
  url: string,
  options?: UseQueryOptions<T, ResultErrors>,
) => {
  const queryClient = new QueryClient();

  const formattedUrl = formatFullUrl(url);

  await queryClient.prefetchQuery<T, ResultErrors>({
    queryKey: options?.queryKey ?? [url],
    queryFn: async () => {
      const result = await get<Result<T>>(formattedUrl);
      return result.successful ? result.data : Promise.reject(result.errors);
    },
    ...options,
  });

  return queryClient;
};

const useApiQuery = <T>(
  url: string,
  options?: UseQueryOptions<T, ResultErrors>,
) => {
  const formattedUrl = formatFullUrl(url);

  return useQuery<T, ResultErrors>({
    queryKey: options?.queryKey ?? [url],
    queryFn: async () => {
      const result = await get<Result<T>>(formattedUrl);
      return result.successful ? result.data : Promise.reject(result.errors);
    },
    ...options,
  });
};

const useApiMutation = <TResponse, TBody>(
  url: string,
  mutationType?: MutationType,
  options?: Omit<
    UseMutationOptions<TResponse, ResultErrors, TBody>,
    "queryKey"
  >,
) => {
  const formattedUrl = formatFullUrl(url);

  const action =
    mutationType === "put" ? put : mutationType === "delete" ? del : post;

  return useMutation<TResponse, ResultErrors, TBody>({
    mutationFn: async (body) => {
      const result = await action<Result<TResponse>>(formattedUrl, body);
      return result.successful ? result.data : Promise.reject(result.errors);
    },
    ...options,
  });
};

export interface ApiAction<TBody = unknown> {
  url: string;
  body?: TBody;
  method?: MutationType;
}

/**
 * A mutation whose target URL is decided per call — for row-level actions like posting
 * one journal entry or signing off one working paper.
 */
const useApiAction = <TResponse = unknown, TBody = unknown>(
  options?: Omit<
    UseMutationOptions<TResponse, ResultErrors, ApiAction<TBody>>,
    "mutationFn"
  >,
) =>
  useMutation<TResponse, ResultErrors, ApiAction<TBody>>({
    mutationFn: async ({ url, body, method }) => {
      const action =
        method === "put" ? put : method === "delete" ? del : post;
      const result = await action<Result<TResponse>>(
        formatFullUrl(url),
        body ?? {},
      );
      return result.successful ? result.data : Promise.reject(result.errors);
    },
    ...options,
  });

/** Multipart upload mutation with a per-call URL. */
const useApiUpload = <TResponse = unknown>(
  options?: Omit<
    UseMutationOptions<TResponse, ResultErrors, { url: string; form: FormData }>,
    "mutationFn"
  >,
) =>
  useMutation<TResponse, ResultErrors, { url: string; form: FormData }>({
    mutationFn: async ({ url, form }) => {
      const result = await postForm<Result<TResponse>>(formatFullUrl(url), form);
      return result.successful ? result.data : Promise.reject(result.errors);
    },
    ...options,
  });

/**
 * One-off GET outside the query cache — for values that must be fresh on every call, such
 * as short-lived signed download URLs.
 */
const fetchApiData = async <T>(
  url: string,
  queryParams?: Record<string, unknown>,
): Promise<T> => {
  const result = await get<Result<T>>(formatFullUrl(url), queryParams);
  return result.successful ? result.data : Promise.reject(result.errors);
};

const formatFullUrl = (url: string) => {
  let baseAddr = process.env.NEXT_PUBLIC_API_URL;

  if (!baseAddr) {
    throw new Error("NEXT_PUBLIC_API_URL is not set");
  }

  if (baseAddr[baseAddr.length - 1] === "/") {
    baseAddr = baseAddr.slice(0, baseAddr.length - 1);
  }

  if (url[0] === "/") {
    url = url.slice(1);
  }

  return baseAddr + "/" + url;
};

export {
  usePaginatedQuery,
  useApiInfiniteQuery,
  useApiQuery,
  useApiMutation,
  useApiAction,
  useApiUpload,
  fetchApiData,
  prefetchApiQuery,
  prefetchPaginatedQuery,
};
