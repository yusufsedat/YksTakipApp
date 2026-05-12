import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import { router } from 'expo-router';
import { useCallback, useEffect, useLayoutEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { LogService } from '../../src/lib/log';
import { submitDiagnosticResult } from '../../src/services/adaptation';
import { generateWeeklyPlan, getWeeklyTasks, updateTaskStatus } from '../../src/services/planner';
import type {
  PlanGenerationReasonCode,
  PlanGenerationResponse,
  ScheduleTaskDto,
  ScheduleTaskStatus,
} from '../../src/types/planner';
import { useTheme } from '../../src/theme';

const WEEK_LABELS_MON = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'];

function toIsoLocal(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function startOfIsoWeekMonday(ref = new Date()): string {
  const d = new Date(ref.getFullYear(), ref.getMonth(), ref.getDate());
  const dow = d.getDay();
  const diff = dow === 0 ? -6 : 1 - dow;
  d.setDate(d.getDate() + diff);
  return toIsoLocal(d);
}

function addCalendarDays(iso: string, n: number): string {
  const [y, mo, da] = iso.split('-').map(Number);
  const d = new Date(y, mo - 1, da);
  d.setDate(d.getDate() + n);
  return toIsoLocal(d);
}

function statusLabelTr(s: ScheduleTaskStatus): string {
  switch (s) {
    case 'completed':
      return 'Tamamlandı';
    case 'skipped':
      return 'Atlandı';
    case 'deferred':
      return 'Ertelendi';
    default:
      return 'Planlı';
  }
}

const DIAGNOSTIC_REASON_FALLBACK =
  'Son denemelerde bu konuda zorlandığını fark ettik. Temelde bir eksik kalıp kalmadığını görmek için bu kısa tekrar testini çözelim.';

function isDiagnosticTask(t: ScheduleTaskDto): boolean {
  const k = (t.taskType ?? 'study').toString().toLowerCase();
  return k === 'diagnostictest';
}

export default function DynamicPlanScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const navigation = useNavigation();

  const [weekStart, setWeekStart] = useState(() => startOfIsoWeekMonday());
  const [selectedIso, setSelectedIso] = useState(() => toIsoLocal(new Date()));
  const [tasks, setTasks] = useState<ScheduleTaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<number | null>(null);
  const [noPlanReason, setNoPlanReason] = useState<PlanGenerationReasonCode | null>(null);

  const weekEnd = useMemo(() => addCalendarDays(weekStart, 6), [weekStart]);

  const weekDays = useMemo(
    () =>
      Array.from({ length: 7 }, (_, i) => {
        const iso = addCalendarDays(weekStart, i);
        return { iso, short: WEEK_LABELS_MON[i], dayNum: iso.slice(8, 10) };
      }),
    [weekStart]
  );

  const loadWeek = useCallback(async () => {
    setError(null);
    setLoading(true);
    try {
      const data = await getWeeklyTasks(weekStart, weekEnd);
      setTasks(data);
      const today = toIsoLocal(new Date());
      if (today >= weekStart && today <= weekEnd) setSelectedIso(today);
      else setSelectedIso(weekStart);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Plan yüklenemedi.');
      LogService.warn('planner weekly load failed', e);
    } finally {
      setLoading(false);
    }
  }, [weekStart, weekEnd]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const data = await getWeeklyTasks(weekStart, weekEnd);
      setTasks(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Plan yüklenemedi.');
      LogService.warn('planner weekly refresh failed', e);
    } finally {
      setRefreshing(false);
    }
  }, [weekStart, weekEnd]);

  useEffect(() => {
    void loadWeek();
  }, [loadWeek]);

  const handleNoPlan = useCallback((result: PlanGenerationResponse) => {
    LogService.warn('planner generate no plan', { reasonCode: result.reasonCode });
    setTasks(result.tasks ?? []);
    setError(result.message ?? 'Plan üretilemedi.');
    setNoPlanReason(result.reasonCode);

    // BACKLOG: dailyCapacityTooLow için ileride /(app)/goals/edit-capacity hazır olunca route güncellenmeli.
    const routeFor: Partial<Record<PlanGenerationReasonCode, () => void>> = {
      requiresGoal: () => router.push('/(app)/goal-onboarding'),
      dailyCapacityTooLow: () => router.push('/(app)/goal-onboarding'),
      noTopics: () => router.push('/(app)/topics'),
      // noRecommendations: yönlendirme yok; aşağıda action panel basılır.
    };

    const navigate = routeFor[result.reasonCode];
    if (!navigate) return;
    Alert.alert('Plan oluşturulamadı', result.message ?? 'Plan oluşturulamadı.', [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Devam et', onPress: navigate },
    ]);
  }, []);

  const regenerate = useCallback(async () => {
    setError(null);
    setNoPlanReason(null);
    setLoading(true);
    try {
      const result = await generateWeeklyPlan(weekStart);
      if (result.status === 'noPlanGenerated') {
        handleNoPlan(result);
        return;
      }
      setTasks(result.tasks);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Plan üretilemedi.');
      LogService.error('planner generate failed', e);
    } finally {
      setLoading(false);
    }
  }, [weekStart, handleNoPlan]);

  const onRegeneratePress = useCallback(() => {
    Alert.alert(
      'Planı yeniden üret',
      'Bu haftanın planlanmış görevleri silinip önerilere göre yeniden oluşturulacak.',
      [
        { text: 'Vazgeç', style: 'cancel' },
        { text: 'Üret', style: 'destructive', onPress: () => void regenerate() },
      ]
    );
  }, [regenerate]);

  useLayoutEffect(() => {
    navigation.setOptions({
      headerRight: () => (
        <Pressable
          onPress={() => onRegeneratePress()}
          hitSlop={12}
          style={{ marginRight: 14, flexDirection: 'row', alignItems: 'center', gap: 4 }}
        >
          <Ionicons name="refresh" size={22} color={colors.primary} />
        </Pressable>
      ),
    });
  }, [navigation, onRegeneratePress, colors.primary]);

  const dayTasks = useMemo(() => tasks.filter((t) => t.taskDate === selectedIso), [tasks, selectedIso]);

  const patchStatus = useCallback(async (taskId: number, status: ScheduleTaskStatus) => {
    setActionId(taskId);
    setError(null);
    try {
      const updated = await updateTaskStatus(taskId, status);
      setTasks((prev) => prev.map((t) => (t.id === taskId ? updated : t)));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Durum güncellenemedi.');
      LogService.warn('planner task status patch failed', e);
    } finally {
      setActionId(null);
    }
  }, []);

  const submitDiag = useCallback(async (taskId: number, result: 'passed' | 'failed' | 'skipped') => {
    setActionId(taskId);
    setError(null);
    try {
      const { task } = await submitDiagnosticResult(taskId, result);
      setTasks((prev) => prev.map((x) => (x.id === task.id ? task : x)));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Teşhis sonucu gönderilemedi.');
      LogService.warn('diagnostic result submit failed', e);
    } finally {
      setActionId(null);
    }
  }, []);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        weekNav: {
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingHorizontal: 12,
          paddingVertical: 8,
          borderBottomWidth: 1,
          borderBottomColor: colors.border,
        },
        weekNavBtn: { padding: 8 },
        weekNavTitle: { fontSize: 14, fontWeight: '700', color: colors.text },
        dayStrip: { maxHeight: 88, borderBottomWidth: 1, borderBottomColor: colors.border },
        dayChip: {
          width: 52,
          paddingVertical: 10,
          marginHorizontal: 4,
          borderRadius: 12,
          alignItems: 'center',
          backgroundColor: colors.surface,
          borderWidth: 1,
          borderColor: colors.border,
        },
        dayChipSelected: { backgroundColor: colors.primary, borderColor: colors.primary },
        dayChipText: { fontSize: 12, fontWeight: '700', color: colors.textSecondary },
        dayChipTextSelected: { color: '#fff' },
        dayNum: { fontSize: 16, fontWeight: '800', color: colors.text, marginTop: 2 },
        dayNumSelected: { color: '#fff' },
        content: { padding: 16, paddingBottom: 32 + insets.bottom },
        title: { fontSize: 18, fontWeight: '800', color: colors.text },
        sub: { marginTop: 4, fontSize: 13, color: colors.textMuted },
        error: { marginTop: 10, color: colors.errorText, fontSize: 14 },
        card: {
          marginTop: 12,
          backgroundColor: colors.surface,
          borderRadius: 12,
          borderWidth: 1,
          borderColor: colors.border,
          padding: 14,
        },
        cardTop: { flexDirection: 'row', justifyContent: 'space-between', gap: 10 },
        subjectBadge: {
          alignSelf: 'flex-start',
          backgroundColor: colors.chip,
          paddingHorizontal: 8,
          paddingVertical: 3,
          borderRadius: 6,
        },
        subjectBadgeText: { fontSize: 11, fontWeight: '700', color: colors.chipText },
        topicName: { marginTop: 6, fontSize: 16, fontWeight: '800', color: colors.text },
        metaRow: { marginTop: 8, flexDirection: 'row', alignItems: 'center', gap: 8 },
        durBadge: {
          paddingHorizontal: 8,
          paddingVertical: 4,
          borderRadius: 8,
          backgroundColor: colors.surfaceMuted,
        },
        durText: { fontSize: 13, fontWeight: '700', color: colors.text },
        statusChip: { fontSize: 12, fontWeight: '600', color: colors.textMuted },
        actions: { marginTop: 12, flexDirection: 'row', gap: 10 },
        btn: {
          flex: 1,
          paddingVertical: 10,
          borderRadius: 10,
          alignItems: 'center',
          borderWidth: 1,
        },
        btnPrimary: { backgroundColor: colors.primary, borderColor: colors.primary },
        btnSecondary: { backgroundColor: colors.surfaceMuted, borderColor: colors.border },
        btnTextPrimary: { color: '#fff', fontWeight: '800', fontSize: 14 },
        btnTextSecondary: { color: colors.text, fontWeight: '700', fontSize: 14 },
        empty: { marginTop: 24, textAlign: 'center', color: colors.textMuted, fontSize: 14 },
        bottomRegen: { marginTop: 20 },
        bottomRegenBtn: {
          paddingVertical: 14,
          borderRadius: 12,
          alignItems: 'center',
          backgroundColor: colors.surfaceMuted,
          borderWidth: 1,
          borderColor: colors.border,
        },
        bottomRegenText: { fontWeight: '800', color: colors.primary, fontSize: 15 },
        cardDiagnostic: {
          marginTop: 12,
          backgroundColor: colors.diagnosticBg,
          borderRadius: 12,
          borderWidth: 1,
          borderColor: colors.diagnosticBorder,
          padding: 14,
        },
        diagnosticBadge: {
          alignSelf: 'flex-start',
          flexDirection: 'row',
          alignItems: 'center',
          gap: 6,
          paddingHorizontal: 8,
          paddingVertical: 4,
          borderRadius: 8,
          backgroundColor: colors.surfaceMuted,
        },
        diagnosticBadgeText: { fontSize: 12, fontWeight: '800', color: colors.diagnosticText },
        diagnosticReason: { marginTop: 10, fontSize: 13, lineHeight: 19, color: colors.textSecondary },
        diagBtnRow: { marginTop: 10, flexDirection: 'column', gap: 8 },
        centered: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 24 },
        suggestCard: {
          marginTop: 14,
          padding: 14,
          backgroundColor: colors.surface,
          borderRadius: 12,
          borderWidth: 1,
          borderColor: colors.border,
          gap: 10,
        },
        suggestTitle: { fontSize: 15, fontWeight: '800', color: colors.text },
        suggestSub: { fontSize: 13, color: colors.textSecondary, lineHeight: 18 },
        suggestBtn: {
          flexDirection: 'row',
          alignItems: 'center',
          gap: 10,
          paddingVertical: 10,
          paddingHorizontal: 12,
          borderRadius: 10,
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.surfaceMuted,
        },
        suggestBtnText: { fontSize: 14, fontWeight: '700', color: colors.text },
      }),
    [colors, insets.bottom]
  );

  if (loading && tasks.length === 0 && !error) {
    return (
      <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={colors.primary} />
          <Text style={[styles.sub, { marginTop: 12 }]}>Plan yükleniyor…</Text>
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
      <View style={styles.weekNav}>
        <Pressable style={styles.weekNavBtn} onPress={() => setWeekStart((w) => addCalendarDays(w, -7))} hitSlop={8}>
          <Ionicons name="chevron-back" size={22} color={colors.text} />
        </Pressable>
        <Text style={styles.weekNavTitle}>
          {weekStart} → {weekEnd}
        </Text>
        <Pressable style={styles.weekNavBtn} onPress={() => setWeekStart((w) => addCalendarDays(w, 7))} hitSlop={8}>
          <Ionicons name="chevron-forward" size={22} color={colors.text} />
        </Pressable>
      </View>

      <FlatList
        horizontal
        data={weekDays}
        keyExtractor={(item) => item.iso}
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={{ paddingVertical: 10, paddingHorizontal: 8 }}
        style={styles.dayStrip}
        renderItem={({ item }) => {
          const sel = item.iso === selectedIso;
          return (
            <Pressable
              style={[styles.dayChip, sel && styles.dayChipSelected]}
              onPress={() => setSelectedIso(item.iso)}
            >
              <Text style={[styles.dayChipText, sel && styles.dayChipTextSelected]}>{item.short}</Text>
              <Text style={[styles.dayNum, sel && styles.dayNumSelected]}>{item.dayNum}</Text>
            </Pressable>
          );
        }}
      />

      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} tintColor={colors.primary} />}
      >
        <Text style={styles.title}>Günün Görevleri</Text>
        <Text style={styles.sub}>
          {selectedIso} — {dayTasks.length} görev
        </Text>
        {error ? <Text style={styles.error}>{error}</Text> : null}

        {noPlanReason === 'noRecommendations' ? (
          <View style={styles.suggestCard}>
            <Text style={styles.suggestTitle}>Şimdi ne yapabilirsin?</Text>
            <Text style={styles.suggestSub}>
              Sistem yeterli sinyale sahip olmadığı için bu hafta plan üretemedi. Aşağıdakilerden birkaçını yaparsan bir sonraki üretimde plan çıkarılır.
            </Text>
            <Pressable style={styles.suggestBtn} onPress={() => router.push('/(app)/topics')}>
              <Ionicons name="list" size={18} color={colors.primary} />
              <Text style={styles.suggestBtnText}>Konu durumlarını güncelle</Text>
            </Pressable>
            <Pressable style={styles.suggestBtn} onPress={() => router.push('/(app)/exams')}>
              <Ionicons name="document-text" size={18} color={colors.primary} />
              <Text style={styles.suggestBtnText}>Deneme sonucu ekle</Text>
            </Pressable>
            <Pressable style={styles.suggestBtn} onPress={() => router.push('/(app)/notebook')}>
              <Ionicons name="bookmark" size={18} color={colors.primary} />
              <Text style={styles.suggestBtnText}>Kumbara / problem notu ekle</Text>
            </Pressable>
          </View>
        ) : null}

        {dayTasks.length === 0 && !error ? (
          <Text style={styles.empty}>Bu gün için atanmış görev yok.</Text>
        ) : null}

        {dayTasks.map((t) => {
          const busy = actionId === t.id;
          const doneLike = t.status === 'completed' || t.status === 'skipped';
          const diag = isDiagnosticTask(t);
          if (diag) {
            const reasonText = (t.reason && t.reason.trim()) || DIAGNOSTIC_REASON_FALLBACK;
            return (
              <View key={t.id} style={styles.cardDiagnostic}>
                <View style={styles.cardTop}>
                  <View style={{ flex: 1 }}>
                    <View style={styles.diagnosticBadge}>
                      <Ionicons name="search" size={16} color={colors.diagnosticText} />
                      <Text style={styles.diagnosticBadgeText}>Teşhis Testi</Text>
                    </View>
                    <View style={styles.subjectBadge}>
                      <Text style={styles.subjectBadgeText}>{t.subjectName || 'Ders'}</Text>
                    </View>
                    <Text style={styles.topicName}>{t.topicName}</Text>
                    <Text style={styles.diagnosticReason}>{reasonText}</Text>
                    <View style={styles.metaRow}>
                      <View style={styles.durBadge}>
                        <Text style={styles.durText}>{t.durationMinutes} dk</Text>
                      </View>
                      <Text style={styles.statusChip}>{statusLabelTr(t.status)}</Text>
                    </View>
                  </View>
                </View>
                <View style={styles.diagBtnRow}>
                  <Pressable
                    style={[styles.btn, styles.btnPrimary, (doneLike || busy) && { opacity: 0.45 }]}
                    disabled={doneLike || busy}
                    onPress={() => void submitDiag(t.id, 'passed')}
                  >
                    <Text style={styles.btnTextPrimary}>Çözdüm — Başardım</Text>
                  </Pressable>
                  <Pressable
                    style={[styles.btn, styles.btnSecondary, (doneLike || busy) && { opacity: 0.45 }]}
                    disabled={doneLike || busy}
                    onPress={() => void submitDiag(t.id, 'failed')}
                  >
                    <Text style={styles.btnTextSecondary}>Zorlandım</Text>
                  </Pressable>
                  <Pressable
                    style={[styles.btn, styles.btnSecondary, (doneLike || busy) && { opacity: 0.45 }]}
                    disabled={doneLike || busy}
                    onPress={() => void submitDiag(t.id, 'skipped')}
                  >
                    <Text style={styles.btnTextSecondary}>Atla</Text>
                  </Pressable>
                </View>
              </View>
            );
          }
          return (
            <View key={t.id} style={styles.card}>
              <View style={styles.cardTop}>
                <View style={{ flex: 1 }}>
                  <View style={styles.subjectBadge}>
                    <Text style={styles.subjectBadgeText}>{t.subjectName || 'Ders'}</Text>
                  </View>
                  <Text style={styles.topicName}>{t.topicName}</Text>
                  <View style={styles.metaRow}>
                    <View style={styles.durBadge}>
                      <Text style={styles.durText}>{t.durationMinutes} dk</Text>
                    </View>
                    <Text style={styles.statusChip}>{statusLabelTr(t.status)}</Text>
                  </View>
                </View>
              </View>
              <View style={styles.actions}>
                <Pressable
                  style={[styles.btn, styles.btnPrimary, (doneLike || busy) && { opacity: 0.45 }]}
                  disabled={doneLike || busy}
                  onPress={() => void patchStatus(t.id, 'completed')}
                >
                  <Text style={styles.btnTextPrimary}>Tamamla</Text>
                </Pressable>
                <Pressable
                  style={[styles.btn, styles.btnSecondary, (doneLike || busy) && { opacity: 0.45 }]}
                  disabled={doneLike || busy}
                  onPress={() => void patchStatus(t.id, 'skipped')}
                >
                  <Text style={styles.btnTextSecondary}>Atla</Text>
                </Pressable>
              </View>
            </View>
          );
        })}

        <View style={styles.bottomRegen}>
          <Pressable style={styles.bottomRegenBtn} onPress={() => onRegeneratePress()} disabled={loading}>
            <Text style={styles.bottomRegenText}>Planı yeniden üret</Text>
          </Pressable>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
