import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { getDailyRecommendations } from '../../src/services/recommendations';
import type { TopicPriority } from '../../src/types/recommendations';
import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { useTheme, type ThemeColors } from '../../src/theme';

function scoreBadgeColor(score: number, colors: ThemeColors) {
  if (score < 30) return colors.success;
  if (score <= 60) return colors.statAccent2;
  return colors.dangerText;
}

function recommendationLabel(t: TopicPriority['recommendationType']): string {
  switch (t) {
    case 'practice':
      return 'Pratik';
    case 'review':
      return 'Tekrar';
    default:
      return 'Çalışma';
  }
}

export default function RecommendationsScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const [items, setItems] = useState<TopicPriority[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    setLoading(true);
    try {
      const data = await getDailyRecommendations();
      setItems(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Öneriler yüklenemedi.');
    } finally {
      setLoading(false);
    }
  }, []);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const data = await getDailyRecommendations();
      setItems(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Öneriler yüklenemedi.');
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        content: { padding: 20, paddingBottom: 32 + insets.bottom },
        title: { fontSize: 22, fontWeight: '800', color: colors.text },
        sub: { marginTop: 6, fontSize: 14, color: colors.textMuted, lineHeight: 20 },
        centered: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 24 },
        error: { marginTop: 12, color: colors.errorText, fontSize: 14 },
        card: {
          marginTop: 14,
          backgroundColor: colors.surface,
          borderRadius: 12,
          borderWidth: 1,
          borderColor: colors.border,
          padding: 14,
        },
        cardHeader: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: 10 },
        subjectBadge: {
          alignSelf: 'flex-start',
          backgroundColor: colors.chip,
          paddingHorizontal: 10,
          paddingVertical: 4,
          borderRadius: 8,
        },
        subjectBadgeText: { fontSize: 12, fontWeight: '700', color: colors.chipText },
        topicName: { marginTop: 8, fontSize: 17, fontWeight: '800', color: colors.text, flex: 1 },
        scoreBadge: {
          paddingHorizontal: 10,
          paddingVertical: 6,
          borderRadius: 10,
          borderWidth: 1,
          borderColor: colors.border,
        },
        scoreBadgeText: { fontSize: 14, fontWeight: '800' },
        typeChip: {
          marginTop: 8,
          alignSelf: 'flex-start',
          paddingHorizontal: 8,
          paddingVertical: 3,
          borderRadius: 6,
          backgroundColor: colors.surfaceMuted,
        },
        typeChipText: { fontSize: 12, fontWeight: '600', color: colors.textSecondary },
        barTrack: {
          marginTop: 10,
          height: 8,
          borderRadius: 4,
          backgroundColor: colors.barTrack,
          overflow: 'hidden',
        },
        barFill: { height: '100%', borderRadius: 4 },
        reason: { marginTop: 10, fontSize: 13, color: colors.textMuted, lineHeight: 19 },
        empty: { marginTop: 24, textAlign: 'center', color: colors.textMuted, fontSize: 14 },
      }),
    [colors, insets.bottom]
  );

  if (loading && items.length === 0) {
    return (
      <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={colors.primary} />
          <Text style={[styles.sub, { marginTop: 12, textAlign: 'center' }]}>Öneriler hazırlanıyor…</Text>
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} tintColor={colors.primary} />}
      >
        <Text style={styles.title}>Bugünkü öneriler</Text>
        <Text style={styles.sub}>Son çalışmaların ve denemelerine göre önceliklendirilmiş en fazla 5 konu.</Text>

        {error ? <Text style={styles.error}>{error}</Text> : null}

        {items.length === 0 && !error ? <Text style={styles.empty}>Şu an listelenecek öneri yok.</Text> : null}

        {items.map((item) => {
          const badgeColor = scoreBadgeColor(item.priorityScore, colors);
          const barPct = Math.min(100, Math.max(0, item.priorityScore));
          return (
            <View key={item.topicId} style={styles.card}>
              <View style={styles.cardHeader}>
                <View style={{ flex: 1 }}>
                  <View style={styles.subjectBadge}>
                    <Text style={styles.subjectBadgeText}>{item.subjectName || 'Ders'}</Text>
                  </View>
                  <Text style={styles.topicName}>{item.topicName}</Text>
                  <View style={styles.typeChip}>
                    <Text style={styles.typeChipText}>{recommendationLabel(item.recommendationType)}</Text>
                  </View>
                </View>
                <View style={[styles.scoreBadge, { borderColor: badgeColor }]}>
                  <Text style={[styles.scoreBadgeText, { color: badgeColor }]}>{item.priorityScore}</Text>
                </View>
              </View>
              <View style={styles.barTrack}>
                <View style={[styles.barFill, { width: `${barPct}%`, backgroundColor: badgeColor }]} />
              </View>
              <Text style={styles.reason}>{item.reason}</Text>
            </View>
          );
        })}
      </ScrollView>
    </SafeAreaView>
  );
}
