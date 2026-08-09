import { ResultErrors } from "@/types/result";
import { getSupabaseClient } from "@/lib/supabase";

const authorizationHeader = async (): Promise<Record<string, string>> => {
  try {
    const { data } = await getSupabaseClient().auth.getSession();

    return data.session
      ? { Authorization: `Bearer ${data.session.access_token}` }
      : {};
  } catch {
    // No browser session available (server render, or Supabase not configured).
    return {};
  }
};

const serializeQueryParamsFromObject = function (
  paramsObject: any,
  path?: string,
): string[] {
  var str = new Array<string>();

  for (var objectProperty of Object.keys(paramsObject)) {
    const value = paramsObject[objectProperty];

    let propPath = objectProperty;

    if (path) {
      if (propPath) {
        propPath = `${path}.${propPath}`;
      } else {
        propPath = `${path}`;
      }
    }
    if (value == null) {
      continue;
    }
    if (value instanceof Date) {
      str.push(`${encodeURIComponent(propPath)}=${value.toISOString()}`);
    } else if (Array.isArray(value)) {
      if (value.length !== 0) {
        const key = encodeURIComponent(propPath);
        const arrayParams = value.map((x) => `${key}=${encodeURIComponent(x)}`);
        const joinedParams = arrayParams.join("&");
        str.push(joinedParams);
      }
    } else if (typeof value === "object") {
      str.push(...serializeQueryParamsFromObject(value, propPath));
    } else {
      str.push(`${encodeURIComponent(propPath)}=${encodeURIComponent(value)}`);
    }
  }

  return str;
};

const common = async (
  method: any,
  url: string,
  body?: any,
  queryParams?: any,
) => {
  const headers: Record<string, string> = {
    Accept: "application/json",
    ...(await authorizationHeader()),
  };

  const request: RequestInit = { method, headers };

  if (body) {
    headers["Content-Type"] = "application/json";
    request.body = JSON.stringify(body);
  }

  if (queryParams) {
    url = `${url}?${(
      serializeQueryParamsFromObject(queryParams).join("&") ?? ""
    ).trim()}`;
  }

  let response: Response;

  try {
    response = await fetch(url, request);
  } catch {
    return Promise.reject(["Unable to reach the server."] as ResultErrors);
  }

  const payload = await response.json().catch(() => null);

  if (!response.ok) {
    // The API returns the same Result envelope for failures, so surface its messages.
    const errors = payload?.errors;

    return Promise.reject(
      (Array.isArray(errors) && errors.length > 0
        ? errors
        : [`Request failed with status ${response.status}.`]) as ResultErrors,
    );
  }

  return payload;
};

const get = async <T>(url: string, queryParams?: any): Promise<T> =>
  common("GET", url, undefined, queryParams);

const post = async <T = any>(url: string, body: any): Promise<T> =>
  common("POST", url, body);

const del = async <T = any>(url: any, body: any): Promise<T> =>
  common("DELETE", url, body);

const put = async <T = any>(url: any, body: any): Promise<T> =>
  common("PUT", url, body);

export { get, post, del, put };
