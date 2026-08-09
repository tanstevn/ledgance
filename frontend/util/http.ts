import { ResultErrors } from "@/types/result";

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
  try {
    const request = {
      method,
      headers: {
        Accept: "application/json",
      },
    } as RequestInit;

    if (body) {
      // @ts-ignore
      request.headers["Content-Type"] = "application/json";
      request.body = JSON.stringify(body);
    }

    if (queryParams) {
      url = `${url}?${(
        serializeQueryParamsFromObject(queryParams).join("&") ?? ""
      ).trim()}`;
    }

    const response = await fetch(url, request);

    if (!response.ok) {
      return Promise.reject([
        "Something went wrong. Result: " + response.status,
      ] as ResultErrors);
    }

    return await response.json();
  } catch (error: any) {
    return Promise.resolve(
      error.response ?? {
        successful: false,
        data: null,
        errors: "Something went wrong.",
      },
    );
  }
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
