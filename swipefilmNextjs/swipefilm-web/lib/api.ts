// ✅ Relatif — frontend et backend sont servis depuis la même origine (voir
// Program.cs, UseStaticFiles/MapFallback). Plus besoin d'une URL figée au
// build (NEXT_PUBLIC_API_URL), qui n'a de sens que si les deux tournent sur
// des origines séparées — ce qui n'est plus le cas.
const API_BASE = "";

interface RequestOptions extends RequestInit {
  // ✅ Gardé pour compat avec les appels existants — n'a plus d'effet
  // depuis le passage au cookie de session (HttpOnly, le navigateur le
  // gère seul) : il n'y a plus de header à ajouter ou à sauter.
  skipAuth?: boolean;
}

async function request<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const { skipAuth: _skipAuth, headers, ...rest } = options;

  const finalHeaders: Record<string, string> = {
    "Content-Type": "application/json",
    ...(headers as Record<string, string>),
  };

  const res = await fetch(`${API_BASE}${path}`, {
    ...rest,
    headers: finalHeaders,
    // ✅ Cookie de session HttpOnly — le navigateur ne l'envoie que si on
    // le demande explicitement. "same-origin" suffit : frontend et backend
    // sont servis depuis la même origine (voir Program.cs, UseStaticFiles).
    credentials: "same-origin",
  });

  if (!res.ok) {
    const message = await res.text().catch(() => res.statusText);
    throw new ApiError(res.status, message || res.statusText);
  }

  if (res.status === 204) return undefined as T;

  return res.json() as Promise<T>;
}

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ApiError";
  }
}

export const api = {
  get: <T>(path: string, options?: RequestOptions) =>
    request<T>(path, { ...options, method: "GET" }),

  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, {
      ...options,
      method: "POST",
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),

  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, {
      ...options,
      method: "PUT",
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),

  delete: <T>(path: string, options?: RequestOptions) =>
    request<T>(path, { ...options, method: "DELETE" }),
};
