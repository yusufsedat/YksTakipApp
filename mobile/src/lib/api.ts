import { getApiBaseUrl } from './config';

export type ApiErrorBody = {
  message?: string;
  title?: string;
  errors?: Record<string, string[]>;
};

let tokenGetter: () => Promise<string | null> = async () => null;

export function setAuthTokenGetter(fn: () => Promise<string | null>) {
  tokenGetter = fn;
}

async function buildHeaders(init?: HeadersInit): Promise<Headers> {
  const h = new Headers(init);
  if (!h.has('Accept')) h.set('Accept', 'application/json');
  const token = await tokenGetter();
  if (token) h.set('Authorization', `Bearer ${token}`);
  return h;
}

function parseErrorMessage(body: ApiErrorBody | null, status: number): string {
  if (body?.message) return body.message;
  if (body?.errors) {
    const first = Object.values(body.errors)[0]?.[0];
    if (first) return first;
  }
  if (body?.title) return body.title;
  return `İstek başarısız (${status})`;
}

function networkFailureMessage(cause: unknown): string {
  const base = getApiBaseUrl();
  const hint = __DEV__
    ? "Geliştirme: API çalışıyor mu (dotnet run)? Telefonda mobile/.env ile EXPO_PUBLIC_API_URL (LAN IP:5278) ayarla."
    : "Production API erişilemiyor; Railway deploy ve CORS origin’lerini kontrol et.";
  const detail = cause instanceof Error ? cause.message : String(cause);
  return `Ağ isteği başarısız — ${base}. ${hint} [${detail}]`;
}

async function fetchOrThrow(input: string, init: RequestInit): Promise<Response> {
  try {
    return await fetch(input, init);
  } catch (e) {
    throw new Error(networkFailureMessage(e));
  }
}

export async function apiGet<T>(path: string): Promise<T> {
  const url = `${getApiBaseUrl()}${path.startsWith('/') ? path : `/${path}`}`;
  const res = await fetchOrThrow(url, {
    method: 'GET',
    headers: await buildHeaders(),
  });
  const text = await res.text();
  const json = safeJson(text);
  if (!res.ok) {
    throw new Error(parseErrorMessage(json as ApiErrorBody | null, res.status));
  }
  return json as T;
}

export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  const url = `${getApiBaseUrl()}${path.startsWith('/') ? path : `/${path}`}`;
  const headers = await buildHeaders({ 'Content-Type': 'application/json' });
  const res = await fetchOrThrow(url, {
    method: 'POST',
    headers,
    body: JSON.stringify(body),
  });
  const text = await res.text();
  const json = safeJson(text);
  if (!res.ok) {
    throw new Error(parseErrorMessage(json as ApiErrorBody | null, res.status));
  }
  return json as T;
}

export async function apiPut<T>(path: string, body: unknown): Promise<T> {
  const url = `${getApiBaseUrl()}${path.startsWith('/') ? path : `/${path}`}`;
  const headers = await buildHeaders({ 'Content-Type': 'application/json' });
  const res = await fetchOrThrow(url, {
    method: 'PUT',
    headers,
    body: JSON.stringify(body),
  });
  const text = await res.text();
  const json = safeJson(text);
  if (!res.ok) {
    throw new Error(parseErrorMessage(json as ApiErrorBody | null, res.status));
  }
  return json as T;
}

export async function apiDelete(path: string): Promise<void> {
  const url = `${getApiBaseUrl()}${path.startsWith('/') ? path : `/${path}`}`;
  const res = await fetchOrThrow(url, {
    method: 'DELETE',
    headers: await buildHeaders(),
  });
  const text = await res.text();
  const json = safeJson(text);
  if (!res.ok) {
    throw new Error(parseErrorMessage(json as ApiErrorBody | null, res.status));
  }
}

function safeJson(text: string): unknown | null {
  if (!text) return null;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}
