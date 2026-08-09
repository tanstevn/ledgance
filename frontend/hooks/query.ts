import { PaginatedRequest } from "@/types/request";
import { PaginatedResult, Result, ResultErrors } from "@/types/result";
import { get, post, del, put } from "@/util/http";

import {
  QueryClient,
  UseMutationOptions,
  UseQueryOptions,
  useMutation,
  useQuery,
} from "@tanstack/react-query";

export type MutationType = "post" | "put" | "delete";

export const DefaultPagination = {
  page: 1,
};

const usePaginatedQuery = <T>(
  url: string,
  params: PaginatedRequest<T>,
  options?: Omit<UseQueryOptions<PaginatedResult<T>, ResultErrors>, "queryKey">,
) => {
  const formattedUrl = formatFullUrl(url);

  return useQuery<PaginatedResult<T>, ResultErrors>({
    queryKey: [url, params],
    queryFn: async () => {
      const result = await get<PaginatedResult<T>>(formattedUrl, params);
      return result.successful ? result : Promise.reject(result.errors);
    },
    ...options,
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
  useApiQuery,
  useApiMutation,
  prefetchApiQuery,
  prefetchPaginatedQuery,
};
