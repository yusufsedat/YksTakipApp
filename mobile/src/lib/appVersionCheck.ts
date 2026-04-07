import Constants from 'expo-constants';
import { Alert, Linking, Platform } from 'react-native';

import { apiGet } from './api';

type VersionConfigResponse = {
  platform: 'android' | 'ios';
  minimumVersion: string;
  latestVersion: string;
  storeUrl: string;
};

let checkedOnce = false;

function normalizeSemver(v: string): [number, number, number] {
  const cleaned = v.trim().replace(/^v/i, '');
  const core = cleaned.split('-')[0];
  const parts = core.split('.');
  const major = Number(parts[0] ?? 0) || 0;
  const minor = Number(parts[1] ?? 0) || 0;
  const patch = Number(parts[2] ?? 0) || 0;
  return [major, minor, patch];
}

function compareSemver(a: string, b: string): number {
  const av = normalizeSemver(a);
  const bv = normalizeSemver(b);
  for (let i = 0; i < 3; i++) {
    if (av[i] > bv[i]) return 1;
    if (av[i] < bv[i]) return -1;
  }
  return 0;
}

async function openStore(storeUrl: string): Promise<void> {
  const androidMarket = 'market://details?id=com.yusufsedat.sinavkilit';
  const target = Platform.OS === 'android' ? androidMarket : storeUrl;
  const fallback = storeUrl;
  try {
    const ok = await Linking.canOpenURL(target);
    if (ok) {
      await Linking.openURL(target);
      return;
    }
    await Linking.openURL(fallback);
  } catch {
    // no-op
  }
}

export async function checkAppVersionOnBoot(): Promise<void> {
  if (checkedOnce) return;
  checkedOnce = true;

  if (Platform.OS !== 'android' && Platform.OS !== 'ios') return;

  try {
    const currentVersion = (Constants.expoConfig?.version ?? '0.0.0').trim();
    const platform = Platform.OS as 'android' | 'ios';
    const cfg = await apiGet<VersionConfigResponse>(`/api/app-config/check-version?platform=${platform}`);

    const forceCmp = compareSemver(currentVersion, cfg.minimumVersion);
    if (forceCmp < 0) {
      Alert.alert('Güncelleme Zorunlu', 'Uygulamayı kullanmaya devam etmek için güncelleme yapmalısın.', [
        {
          text: 'Güncelle',
          onPress: () => {
            void openStore(cfg.storeUrl);
          },
        },
      ], { cancelable: false });
      return;
    }

    const latestCmp = compareSemver(currentVersion, cfg.latestVersion);
    if (latestCmp < 0) {
      Alert.alert(
        'Yeni Sürüm Mevcut',
        'Yeni bir sürüm mevcut, güncelleyip yeni özellikleri denemek ister misin?',
        [
          { text: 'Sonra', style: 'cancel' },
          { text: 'Şimdi', onPress: () => { void openStore(cfg.storeUrl); } },
        ],
        { cancelable: true }
      );
    }
  } catch {
    // Ağ/API hatası: uygulama akışını engelleme
  }
}
