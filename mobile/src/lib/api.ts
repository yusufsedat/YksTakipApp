import axios, { AxiosError, type AxiosInstance, type AxiosRequestConfig } from 'axios';
import { getApiBaseUrl } from './config';

export type ApiErrorBody = {
  message?: string;
  title?: string;
  errors?: Record<string, string[]>;
};

type AuthHooks = {
  getAccessToken: () => Promise<string | null>;
  getRefreshToken: () => Promise<string | null>;
  refreshTokens: (refreshToken: string) => Promise<{ token: string; refreshToken: string }>;
  persistTokens: (token: string, refreshToken: string) => Promise<void>;
  clearSession: () => Promise<void>;
};

let authHooks: AuthHooks | null = null;
let refreshPromise: Promise<string | null> | null = null;

export function setApiAuthHooks(hooks: AuthHooks) {
  authHooks = hooks;
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
    ? "Geliştirme: API çalışıyor mu? Telefonda mobile/.env ile EXPO_PUBLIC_API_URL (LAN IP:5278) ayarla."
    : "Production API erişilemiyor; Railway deploy ve CORS origin’lerini kontrol et.";
  const detail = cause instanceof Error ? cause.message : String(cause);
  return `Ağ isteği başarısız — ${base}. ${hint} [${detail}]`;
}

const apiClient: AxiosInstance = axios.create({
  baseURL: getApiBaseUrl(),
  timeout: 15000,
  headers: { Accept: 'application/json' },
});

apiClient.interceptors.request.use(async (config) => {
  if (!config.headers) config.headers = {} as never;
  const token = await authHooks?.getAccessToken();
  if (token) {
    (config.headers as Record<string, string>).Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiErrorBody>) => {
    const original = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined;
    const status = error.response?.status;
    if (!original || status !== 401 || original._retry || !authHooks) {
      return Promise.reject(toApiError(error));
    }

    // Refresh endpoint itself başarısızsa döngüye girme.
    if ((original.url ?? '').includes('/users/refresh-token')) {
      await authHooks.clearSession();
      return Promise.reject(toApiError(error));
    }

    original._retry = true;

    if (!refreshPromise) {
      refreshPromise = (async () => {
        const refreshToken = await authHooks!.getRefreshToken();
        if (!refreshToken) return null;
        try {
          const refreshed = await authHooks!.refreshTokens(refreshToken);
          await authHooks!.persistTokens(refreshed.token, refreshed.refreshToken);
          return refreshed.token;
        } catch {
          await authHooks!.clearSession();
          return null;
        } finally {
          refreshPromise = null;
        }
      })();
    }

    const newAccessToken = await refreshPromise;
    if (!newAccessToken) {
      return Promise.reject(new Error('Oturum süresi doldu. Lütfen tekrar giriş yapın.'));
    }

    if (!original.headers) original.headers = {} as never;
    (original.headers as Record<string, string>).Authorization = `Bearer ${newAccessToken}`;
    return apiClient.request(original);
  }
);

function toApiError(error: AxiosError<ApiErrorBody>): Error {
  if (!error.response) return new Error(networkFailureMessage(error));
  return new Error(parseErrorMessage(error.response.data ?? null, error.response.status));
}

export async function apiGet<T>(path: string): Promise<T> {
  const res = await apiClient.get<T>(path.startsWith('/') ? path : `/${path}`);
  return res.data;
}

export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  const res = await apiClient.post<T>(path.startsWith('/') ? path : `/${path}`, body);
  return res.data;
}

export async function apiPut<T>(path: string, body: unknown): Promise<T> {
  const res = await apiClient.put<T>(path.startsWith('/') ? path : `/${path}`, body);
  return res.data;
}

export async function apiDelete(path: string): Promise<void> {
  await apiClient.delete(path.startsWith('/') ? path : `/${path}`);
}
