import { router } from 'expo-router';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ExamCountdownCard } from '../../src/components/ExamCountdownCard';
import { apiGet } from '../../src/lib/api';
import { useAuth } from '../../src/lib/auth';
import type { StatsProgress } from '../../src/types/api';
import { useTheme } from '../../src/theme';

type MeProfile = {
  id: number;
  name: string;
  email: string;
  stats?: {
    totalMinutesLast7Days: number;
    examCount: number;
    examStreakDays?: number;
  };
};

export default function HomeScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const { user, logout } = useAuth();
  const [profile, setProfile] = useState<MeProfile | null>(null);
  const [progress, setProgress] = useState<StatsProgress | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        content: { padding: 24 },
        greeting: { fontSize: 22, fontWeight: '700', color: colors.text },
        email: { fontSize: 15, color: colors.textMuted, marginTop: 4, marginBottom: 20 },
        muted: { color: colors.textMuted },
        error: { color: colors.errorText, marginBottom: 12 },
        countdownWrap: { marginBottom: 16 },
        cards: { gap: 12 },
        card: {
          backgroundColor: colors.surface,
          borderRadius: 12,
          padding: 14,
          borderWidth: 1,
          borderColor: colors.border,
        },
        cardLabel: { fontSize: 14, color: colors.textMuted },
        cardValueRow: { flexDirection: 'row', alignItems: 'baseline', gap: 8, marginTop: 2 },
        cardValue: { fontSize: 24, fontWeight: '700', color: colors.text },
        trend: { fontSize: 13, fontWeight: '700', color: colors.textMuted },
        streakCard: {
          borderColor: colors.streakBorder,
          backgroundColor: colors.streakBg,
        },
        streakValue: { fontSize: 22, fontWeight: '800', color: colors.streakTitle, marginTop: 4 },
        streakHint: { fontSize: 13, color: colors.streakText, marginTop: 8, lineHeight: 18 },
        hint: { marginTop: 20, fontSize: 14, color: colors.textMuted, lineHeight: 20 },
        logout: {
          marginTop: 28,
          paddingVertical: 14,
          alignItems: 'center',
          borderRadius: 10,
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.surfaceMuted,
        },
        logoutText: { color: colors.textSecondary, fontSize: 16, fontWeight: '600' },
      }),
    [colors]
  );

  const scrollContentStyle = useMemo(
    () => [styles.content, { paddingBottom: 48 + insets.bottom }],
    [styles.content, insets.bottom]
  );

  const loadAll = useCallback(async () => {
    setLoadError(null);
    setLoading(true);
    try {
      const [me, p] = await Promise.all([
        apiGet<MeProfile>('/users/me'),
        apiGet<StatsProgress>('/stats/progress'),
      ]);
      setProfile(me);
      setProgress(p);
    } catch (e) {
      setLoadError(e instanceof Error ? e.message : 'Profil/istatistikler yüklenemedi.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  async function onRefresh() {
    setRefreshing(true);
    try {
      await loadAll();
      setLoadError(null);
    } catch (e) {
      setLoadError(e instanceof Error ? e.message : 'Yenileme başarısız.');
    } finally {
      setRefreshing(false);
    }
  }

  async function onLogout() {
    await logout();
    router.replace('/(auth)/login');
  }

  const minutes = profile?.stats?.totalMinutesLast7Days ?? 0;
  const exams = profile?.stats?.examCount ?? 0;
  const streak = profile?.stats?.examStreakDays ?? 0;
  const changePercent = progress?.changePercent ?? null;
  const trendArrow = changePercent == null ? null : changePercent >= 0 ? '▲' : '▼';
  const trendText = changePercent == null ? null : `${changePercent >= 0 ? '+' : ''}${Math.round(changePercent)}%`;
  const trendColor = changePercent == null ? colors.textMuted : changePercent >= 0 ? colors.statusProgress : colors.textMuted;

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={scrollContentStyle}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
    >
      <Text style={styles.greeting}>
        Merhaba, {profile?.name ?? user?.name ?? '…'}
      </Text>
      <Text style={styles.email}>{profile?.email ?? user?.email}</Text>

      <View style={styles.countdownWrap}>
        <ExamCountdownCard />
      </View>

      {loading ? <Text style={styles.muted}>Yükleniyor…</Text> : null}
      {loadError ? <Text style={styles.error}>{loadError}</Text> : null}

      {!loading && !loadError ? (
        <View style={styles.cards}>
          {streak >= 2 ? (
            <View style={[styles.card, styles.streakCard]}>
              <Text style={styles.cardLabel}>Deneme serisi</Text>
              <Text style={styles.streakValue}>{streak} gün üst üste</Text>
              <Text style={styles.streakHint}>Her gün küçük bir adım — böyle devam.</Text>
            </View>
          ) : null}
          <View style={styles.card}>
            <Text style={styles.cardLabel}>Son 7 gün çalışma</Text>
            <View style={styles.cardValueRow}>
              <Text style={styles.cardValue}>{minutes} dk</Text>
              {trendText ? <Text style={[styles.trend, { color: trendColor }]}>{trendArrow} {trendText}</Text> : null}
            </View>
          </View>
          <View style={styles.card}>
            <Text style={styles.cardLabel}>Toplam deneme</Text>
            <Text style={[styles.cardValue, { fontSize: 22, marginTop: 2 }]}>{exams}</Text>
          </View>
        </View>
      ) : null}

      <Text style={styles.hint}>
        Alttaki sekmelerden konularını ve denemelerini yönetebilir; ayrıca Araçlar sekmesinden çalışma/program/kumbara adımlarını yapabilirsin.
      </Text>

      <Pressable style={styles.logout} onPress={onLogout}>
        <Text style={styles.logoutText}>Çıkış yap</Text>
      </Pressable>
    </ScrollView>
  );
}
