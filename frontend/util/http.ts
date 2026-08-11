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
  paramsObject: Record<string, unknown>,
  path?: string,
): string[] {
  const str = new Array<string>();

  for (const objectProperty of Object.keys(paramsObject)) {
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
        const arrayParams = value.map(
          (x) => `${key}=${encodeURIComponent(String(x))}`,
        );
        const joinedParams = arrayParams.join("&");
        str.push(joinedParams);
      }
    } else if (typeof value === "object") {
      str.push(
        ...serializeQueryParamsFromObject(
          value as Record<string, unknown>,
          propPath,
        ),
      );
    } else {
      str.push(
        `${encodeURIComponent(propPath)}=${encodeURIComponent(String(value))}`,
      );
    }
  }

  return str;
};

const common = async (
  method: string,
  url: string,
  body?: unknown,
  queryParams?: Record<string, unknown>,
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

    const messages = (
      Array.isArray(errors) && errors.length > 0
        ? errors
        : [`Request failed with status ${response.status}.`]
    ) as ResultErrors;

    notifyEntitlementRequired(response.status, messages);

    return Promise.reject(messages);
  }

  return payload;
};

/** The event an entitlement refusal raises, so the shell can offer an upgrade. */
export const entitlementRequiredEvent = "ledgance:entitlement-required";

/**
 * HTTP 402 means the server refused for want of a plan, not for want of a permission. The
 * refusal is broadcast so one listener can offer the upgrade path, instead of every caller
 * having to recognise it.
 */
const notifyEntitlementRequired = (status: number, messages: ResultErrors) => {
  if (status !== 402 || typeof window === "undefined") {
    return;
  }

  window.dispatchEvent(
    new CustomEvent<{ message: string }>(entitlementRequiredEvent, {
      detail: { message: messages.join(" ") },
    }),
  );
};

const get = async <T>(
  url: string,
  queryParams?: Record<string, unknown>,
): Promise<T> => common("GET", url, undefined, queryParams);

/** Multipart upload — the browser sets the Content-Type boundary itself. */
const postForm = async <T = unknown>(
  url: string,
  form: FormData,
): Promise<T> => {
  const headers: Record<string, string> = {
    Accept: "application/json",
    ...(await authorizationHeader()),
  };

  let response: Response;

  try {
    response = await fetch(url, { method: "POST", headers, body: form });
  } catch {
    return Promise.reject(["Unable to reach the server."] as ResultErrors);
  }

  const payload = await response.json().catch(() => null);

  if (!response.ok) {
    const errors = payload?.errors;

    return Promise.reject(
      (Array.isArray(errors) && errors.length > 0
        ? errors
        : [`Request failed with status ${response.status}.`]) as ResultErrors,
    );
  }

  return payload;
};

const post = async <T = unknown>(url: string, body: unknown): Promise<T> =>
  common("POST", url, body);

const del = async <T = unknown>(url: string, body: unknown): Promise<T> =>
  common("DELETE", url, body);

const put = async <T = unknown>(url: string, body: unknown): Promise<T> =>
  common("PUT", url, body);

export { get, post, postForm, del, put };
