import { Platform } from 'react-native';

const PORT = 5278;

/** Production API (Railway). Release build’da EXPO_PUBLIC_API_URL tanımlı değilse kullanılır. */
const PRODUCTION_API_BASE_URL = 'https://ykstakipapp-production.up.railway.app';

/**
 * Geliştirme (`expo start`, __DEV__):
 * - EXPO_PUBLIC_API_URL varsa (mobile/.env) onu kullan.
 * - Yoksa Android emülatör → 10.0.2.2, iOS sim / web → localhost.
 *
 * Production build (__DEV__ === false):
 * - EAS / release’te genelde .env yok; varsayılan Railway URL’si kullanılır.
 * - İstersen EAS Secrets veya .env ile EXPO_PUBLIC_API_URL ile override edebilirsin.
 */
function defaultDevBaseUrl(): string {
  if (Platform.OS === 'android') {
    return `http://10.0.2.2:${PORT}`;
  }
  return `http://localhost:${PORT}`;
}

export function getApiBaseUrl(): string {
  const envUrl = process.env.EXPO_PUBLIC_API_URL?.trim();
  const normalized = envUrl ? envUrl.replace(/\/$/, '') : '';

  if (__DEV__) {
    if (normalized) return normalized;
    return defaultDevBaseUrl();
  }

  if (normalized) return normalized;
  return PRODUCTION_API_BASE_URL;
}
