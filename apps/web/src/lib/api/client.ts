import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type CurrentUserResponse = components["schemas"]["CurrentUserResponse"];
export type AuthSessionResponse = components["schemas"]["AuthSessionResponse"];
export type HealthResponse = components["schemas"]["HealthResponse"];

export interface ApiProblemError {
  status: number;
  errorCode: string;
  title: string;
  detail: string;
  traceId?: string;
}

const rawClient = createClient<paths>({
  baseUrl: "",
  credentials: "include",
});

let cachedCsrfToken: string | null = null;

export function setCsrfToken(token: string | null): void {
  cachedCsrfToken = token;
}

export function extractProblemError(error: unknown, fallbackStatus: number): ApiProblemError {
  if (error && typeof error === "object") {
    const rec = error as Record<string, unknown>;
    return {
      status: typeof rec.status === "number" ? rec.status : fallbackStatus,
      errorCode: typeof rec.errorCode === "string" ? rec.errorCode : "unknown",
      title: typeof rec.title === "string" ? rec.title : "Request failed",
      detail: typeof rec.detail === "string" ? rec.detail : "Unexpected API response.",
      traceId: typeof rec.traceId === "string" ? rec.traceId : undefined,
    };
  }

  return {
    status: fallbackStatus,
    errorCode: "unknown",
    title: "Request failed",
    detail: "Unexpected API response.",
  };
}

export async function ensureCsrfToken(): Promise<string> {
  if (cachedCsrfToken) {
    return cachedCsrfToken;
  }

  const { data, error, response } = await rawClient.GET("/api/auth/csrf", {
    cache: "no-store",
  });

  if (error || !data) {
    throw extractProblemError(error, (response as Response).status);
  }

  cachedCsrfToken = data.requestToken;
  return data.requestToken;
}

export async function fetchHealth(): Promise<HealthResponse> {
  const { data, error, response } = await rawClient.GET("/api/health", {
    cache: "no-store",
  });
  if (error || !data) {
    throw extractProblemError(error, (response as Response).status);
  }
  return data;
}

export async function fetchCurrentUser(): Promise<CurrentUserResponse | null> {
  const { data, error, response } = await rawClient.GET("/api/auth/me", {
    cache: "no-store",
  });
  if (response.status === 401) {
    return null;
  }
  if (error || !data) {
    throw extractProblemError(error, (response as Response).status);
  }
  return data;
}

export async function loginWithPassword(
  email: string,
  password: string,
  rememberMe: boolean,
): Promise<AuthSessionResponse> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST("/api/auth/login", {
    headers: {
      "X-CSRF-Token": csrf,
    },
    body: {
      email,
      password,
      rememberMe,
    },
    cache: "no-store",
  });

  if (error || !data) {
    const status = (response as Response).status;
    if (status === 403) {
      cachedCsrfToken = null;
    }
    throw extractProblemError(error, status);
  }

  cachedCsrfToken = data.csrfToken;
  return data;
}

export async function logoutSession(): Promise<void> {
  const csrf = await ensureCsrfToken();
  const { data, error, response } = await rawClient.POST("/api/auth/logout", {
    headers: {
      "X-CSRF-Token": csrf,
    },
    cache: "no-store",
  });

  if (error || !data) {
    const status = (response as Response).status;
    if (status === 403) {
      cachedCsrfToken = null;
    }
    throw extractProblemError(error, status);
  }

  cachedCsrfToken = data.csrfToken;
}
