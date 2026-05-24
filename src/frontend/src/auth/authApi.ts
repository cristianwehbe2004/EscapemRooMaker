import { AuthResponse, LoginRequest, RegisterRequest, StoredAuthSession } from "../types/auth";
import { clearAuthSession, setAuthSession } from "./authSession";

const apiBaseUrl = process.env.REACT_APP_API_BASE_URL ?? "http://localhost:5130";

const resolveErrorMessage = async (response: Response): Promise<string> => {
  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    try {
      const body = (await response.json()) as { detail?: string; title?: string; message?: string };
      return body.detail ?? body.message ?? body.title ?? `Request failed: ${response.status}`;
    } catch {
      return `Request failed: ${response.status}`;
    }
  }

  const text = await response.text();
  return text || `Request failed: ${response.status}`;
};

async function postAuth<TRequest>(path: string, payload: TRequest): Promise<StoredAuthSession> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    throw new Error(await resolveErrorMessage(response));
  }

  const result = (await response.json()) as AuthResponse;
  setAuthSession(result);
  return result;
}

async function resolveAuthResponse(response: Response): Promise<StoredAuthSession> {
  if (!response.ok) {
    throw new Error(await resolveErrorMessage(response));
  }

  const result = (await response.json()) as AuthResponse;
  setAuthSession(result);
  return result;
}

export function signIn(request: LoginRequest) {
  return postAuth("/api/auth/login", request);
}

export function registerAccount(request: RegisterRequest) {
  return postAuth("/api/auth/register", request);
}

export async function refreshAuthSession(refreshToken: string) {
  const response = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ refreshToken }),
  });

  return resolveAuthResponse(response);
}

export async function signOut(accessToken: string, refreshToken: string) {
  try {
    if (accessToken && refreshToken) {
      await fetch(`${apiBaseUrl}/api/auth/logout`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify({ refreshToken }),
      });
    }
  } finally {
    clearAuthSession();
  }
}
