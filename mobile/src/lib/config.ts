import { Platform } from 'react-native';

const PORT = 5278;

/**
 * EXPO_PUBLIC_API_URL yoksa:
 * - Android emülatör: bilgisayardaki localhost → 10.0.2.2
 * - iOS simülatör / web: localhost
 * Fiziksel telefon: mobile/.env içinde mutlaka `EXPO_PUBLIC_API_URL=http://BILGISAYAR_IP:5278` ayarla.
 */
function defaultDevBaseUrl(): string {
  if (Platform.OS === 'android') {
    return `http://10.0.2.2:${PORT}`;
  }
  return `http://localhost:${PORT}`;
}

export function getApiBaseUrl(): string {
  const url = process.env.EXPO_PUBLIC_API_URL?.trim();
  if (url) return url.replace(/\/$/, '');
  return defaultDevBaseUrl();
}
