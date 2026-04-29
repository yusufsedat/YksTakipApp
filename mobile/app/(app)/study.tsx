import DateTimePicker from '@react-native-community/datetimepicker';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  AppState,
  Platform,
  Pressable,
  RefreshControl,
  SectionList,
  StyleSheet,
  Text,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';

import { apiGet, apiPost } from '../../src/lib/api';
import { dateToApiIso, parseYmd, todayYmd } from '../../src/lib/date';
import {
  pauseStopwatchForegroundService,
  startStopwatchForegroundService,
  stopStopwatchForegroundService,
  syncStopwatchForegroundNotification,
} from '../../src/lib/stopwatchForegroundService';
import {
  enqueuePendingStudyTime,
  flushPendingStudyTimes,
  isLikelyNetworkError,
} from '../../src/lib/pendingStudyTimes';
import { readStopwatchState, writeStopwatchState } from '../../src/lib/stopwatchState';
import type { Paginated, StudyTimeDto, TopicDto, UserTopicDto } from '../../src/types/api';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { TopicPickerModal } from '../../src/components/TopicPickerModal';
import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import { mergeUserTopicsWithCatalog, type UserTopicRow } from '../../src/lib/userTopicRows';
import { useTheme } from '../../src/theme';

function localDayKeyFromIso(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function todayLocalKey(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function yesterdayLocalKey(): string {
  const d = new Date();
  d.setDate(d.getDate() - 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function isAfterLocalToday(d: Date): boolean {
  const a = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  const b = new Date();
  const c = new Date(b.getFullYear(), b.getMonth(), b.getDate());
  return a.getTime() > c.getTime();
}

function formatSectionTitle(dayKey: string, totalMin: number): string {
  const total = `${totalMin} dk`;
  if (dayKey === todayLocalKey()) return `Bugün (Toplam: ${total})`;
  if (dayKey === yesterdayLocalKey()) return `Dün (Toplam: ${total})`;
  try {
    const [y, m, d] = dayKey.split('-').map(Number);
    const dt = new Date(y, m - 1, d);
    const label = dt.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
    return `${label} (Toplam: ${total})`;
  } catch {
    return `${dayKey} (Toplam: ${total})`;
  }
}

function formatRowDate(iso: string) {
  try {
    return new Date(iso).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
  } catch {
    return '';
  }
}

function formatDigital(ms: number): string {
  const totalSec = Math.floor(ms / 1000);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

export default function StudyScreen() {
  const { colors } = useTheme();
  const [items, setItems] = useState<StudyTimeDto[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [minutesStr, setMinutesStr] = useState('60');
  const [dateStr, setDateStr] = useState(todayYmd());
  const [dateVal, setDateVal] = useState(() => {
    const p = parseYmd(todayYmd());
    return p ?? new Date();
  });
  const [showPicker, setShowPicker] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [elapsedMs, setElapsedMs] = useState(0);
  const [startedAtMs, setStartedAtMs] = useState<number | null>(null);
  const [timerSaving, setTimerSaving] = useState(false);
  const appStateRef = useRef(AppState.currentState);

  const [topicRows, setTopicRows] = useState<UserTopicRow[]>([]);
  const [selectedTopicId, setSelectedTopicId] = useState<number | null>(null);
  const [topicModalOpen, setTopicModalOpen] = useState(false);

  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);
  const insets = useSafeAreaInsets();

  const styles = useMemo(
    () =>
      StyleSheet.create({
        list: { flex: 1 },
        container: { flex: 1, backgroundColor: colors.bg },
        centered: { flex: 1, justifyContent: 'center', alignItems: 'center' },
        bannerError: { color: colors.errorText, padding: rs(12, scale), backgroundColor: colors.errorBg },
        form: {
          marginHorizontal: rs(16, scale),
          marginTop: rs(8, scale),
          marginBottom: rs(12, scale),
          padding: rs(16, scale),
          backgroundColor: colors.surface,
          borderRadius: rs(12, scale),
          borderWidth: 1,
          borderColor: colors.border,
        },
        formTitle: { fontSize: rs(16, scale), fontWeight: '700', color: colors.text, marginBottom: rs(12, scale) },
        input: {
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingHorizontal: rs(12, scale),
          paddingVertical: rvs(10, vScale),
          fontSize: rs(16, scale),
          marginBottom: rs(10, scale),
          backgroundColor: colors.inputBg,
          color: colors.text,
        },
        quickRow: { flexDirection: 'row', flexWrap: 'wrap', gap: rs(8, scale), marginBottom: rs(12, scale) },
        quickBtn: {
          paddingVertical: rs(8, scale),
          paddingHorizontal: rs(12, scale),
          borderRadius: rs(20, scale),
          backgroundColor: colors.chip,
          borderWidth: 1,
          borderColor: colors.border,
        },
        quickBtnText: { fontSize: rs(13, scale), fontWeight: '700', color: colors.chipText },
        dateBtn: {
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(12, vScale),
          paddingHorizontal: rs(12, scale),
          marginBottom: rs(10, scale),
          backgroundColor: colors.surfaceMuted,
        },
        dateBtnText: { fontSize: rs(16, scale), color: colors.text },
        topicRow: {
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(12, vScale),
          paddingHorizontal: rs(12, scale),
          marginBottom: rs(10, scale),
          backgroundColor: colors.surfaceMuted,
        },
        topicRowText: { fontSize: rs(15, scale), color: colors.text },
        topicRowHint: { fontSize: rs(12, scale), color: colors.textMuted, marginTop: rs(4, scale) },
        fieldError: { color: colors.errorText, marginBottom: rs(8, scale) },
        submitBtn: {
          backgroundColor: colors.primary,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(12, vScale),
          alignItems: 'center',
          marginTop: rs(4, scale),
        },
        submitDisabled: { opacity: 0.7 },
        submitText: { color: colors.onPrimary, fontWeight: '600', fontSize: rs(16, scale) },
        timerCard: {
          marginBottom: rs(10, scale),
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          padding: rs(12, scale),
          backgroundColor: colors.surfaceMuted,
        },
        timerTitle: { fontSize: rs(14, scale), color: colors.textMuted, marginBottom: rs(4, scale), fontWeight: '700' },
        timerDisplay: { fontSize: rs(30, scale), fontWeight: '800', color: colors.primary },
        timerHint: { fontSize: rs(12, scale), color: colors.textMuted, marginTop: rs(4, scale), marginBottom: rs(8, scale) },
        timerButtons: { flexDirection: 'row', gap: rs(8, scale), marginTop: rs(4, scale) },
        timerBtn: {
          flex: 1,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(10, vScale),
          alignItems: 'center',
          justifyContent: 'center',
        },
        timerStartBtn: { backgroundColor: colors.primary },
        timerPauseBtn: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border },
        timerFinishBtn: { backgroundColor: colors.admin },
        timerResetBtn: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border },
        timerBtnText: { color: colors.onPrimary, fontWeight: '700', fontSize: rs(13, scale) },
        timerBtnTextMuted: { color: colors.text, fontWeight: '700', fontSize: rs(13, scale) },
        listHeading: {
          fontSize: rs(15, scale),
          fontWeight: '600',
          color: colors.textMuted,
          marginHorizontal: rs(16, scale),
          marginTop: rs(4, scale),
          marginBottom: rs(10, scale),
        },
        listContent: { paddingHorizontal: rs(16, scale) },
        sectionHeader: {
          paddingHorizontal: rs(4, scale),
          paddingVertical: rs(10, scale),
          backgroundColor: colors.bg,
        },
        sectionTitle: { fontSize: rs(14, scale), fontWeight: '800', color: colors.textSecondary },
        row: {
          flexDirection: 'row',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          paddingVertical: rvs(10, vScale),
          paddingHorizontal: rs(12, scale),
          borderBottomWidth: StyleSheet.hairlineWidth,
          borderBottomColor: colors.border,
          backgroundColor: colors.surface,
          borderRadius: rs(8, scale),
          marginBottom: rs(6, scale),
        },
        rowLeft: { flex: 1, marginRight: rs(8, scale) },
        rowTime: { fontSize: rs(12, scale), color: colors.textMuted, marginBottom: rs(4, scale) },
        rowTopic: { fontSize: rs(14, scale), color: colors.text, fontWeight: '600' },
        rowSub: { fontSize: rs(12, scale), color: colors.textMuted, marginTop: rs(2, scale) },
        rowMin: { fontSize: rs(16, scale), fontWeight: '700', color: colors.statAccent },
        empty: { color: colors.textMuted, textAlign: 'center', marginTop: rs(12, scale) },
      }),
    [scale, vScale, colors]
  );

  const listContentPad = useMemo(
    () => [styles.listContent, { paddingBottom: rvs(32, vScale) + insets.bottom }],
    [styles.listContent, vScale, insets.bottom]
  );

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const saved = await readStopwatchState();
      await flushPendingStudyTimes();
      if (cancelled) return;
      setIsRunning(saved.isRunning);
      setElapsedMs(saved.elapsedMs);
      setStartedAtMs(saved.startedAtMs);
      setSelectedTopicId(saved.selectedTopicId);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!isRunning || startedAtMs === null) return;
    const timer = setInterval(() => {
      setElapsedMs(Date.now() - startedAtMs);
    }, 1000);
    return () => clearInterval(timer);
  }, [isRunning, startedAtMs]);

  useEffect(() => {
    if (!isRunning) return;
    void syncStopwatchForegroundNotification(elapsedMs);
  }, [isRunning, elapsedMs]);

  useEffect(() => {
    const sub = AppState.addEventListener('change', (nextState) => {
      const prev = appStateRef.current;
      appStateRef.current = nextState;
      if (prev.match(/inactive|background/) && nextState === 'active') {
        void (async () => {
          await flushPendingStudyTimes();
          const saved = await readStopwatchState();
          setIsRunning(saved.isRunning);
          setElapsedMs(saved.elapsedMs);
          setStartedAtMs(saved.startedAtMs);
          setSelectedTopicId(saved.selectedTopicId);
        })();
      }
    });
    return () => sub.remove();
  }, []);

  useEffect(() => {
    void writeStopwatchState({
      isRunning,
      elapsedMs,
      startedAtMs,
      selectedTopicId,
    });
  }, [isRunning, elapsedMs, startedAtMs, selectedTopicId]);

  const loadUserTopics = useCallback(async () => {
    try {
      const [catRes, userRes] = await Promise.all([
        apiGet<Paginated<TopicDto>>('/topics?page=1&pageSize=500'),
        apiGet<UserTopicDto[]>('/user/topics'),
      ]);
      setTopicRows(mergeUserTopicsWithCatalog(catRes.items, userRes));
    } catch {
      setTopicRows([]);
    }
  }, []);

  useEffect(() => {
    void loadUserTopics();
  }, [loadUserTopics]);

  const fetchPage = useCallback(async (p: number, append: boolean) => {
    const res = await apiGet<Paginated<StudyTimeDto>>(`/studytime/list?page=${p}&pageSize=20`);
    setTotal(res.meta.total);
    if (append) setItems((prev) => [...prev, ...res.items]);
    else setItems(res.items);
    setPage(p);
  }, []);

  const loadFirst = useCallback(async () => {
    setError(null);
    setLoading(true);
    try {
      await fetchPage(1, false);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Liste yüklenemedi.');
    } finally {
      setLoading(false);
    }
  }, [fetchPage]);

  useEffect(() => {
    void loadFirst();
  }, [loadFirst]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await fetchPage(1, false);
      await loadUserTopics();
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Liste yüklenemedi.');
    } finally {
      setRefreshing(false);
    }
  }, [fetchPage, loadUserTopics]);

  async function loadMore() {
    if (loadingMore || items.length >= total) return;
    setLoadingMore(true);
    try {
      const next = page + 1;
      const res = await apiGet<Paginated<StudyTimeDto>>(`/studytime/list?page=${next}&pageSize=20`);
      setItems((prev) => [...prev, ...res.items]);
      setPage(next);
      setTotal(res.meta.total);
    } catch {
      // sessiz
    } finally {
      setLoadingMore(false);
    }
  }

  const selectedTopicLabel = useMemo(() => {
    if (selectedTopicId == null) return null;
    const r = topicRows.find((t) => t.topicId === selectedTopicId);
    return r ? `${r.category} · ${r.name}` : null;
  }, [selectedTopicId, topicRows]);

  const sections = useMemo(() => {
    const map = new Map<string, StudyTimeDto[]>();
    for (const it of items) {
      const k = localDayKeyFromIso(it.date);
      if (!map.has(k)) map.set(k, []);
      map.get(k)!.push(it);
    }
    for (const arr of map.values()) {
      arr.sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());
    }
    const keys = [...map.keys()].sort((a, b) => b.localeCompare(a));
    return keys.map((key) => {
      const data = map.get(key)!;
      const totalMin = data.reduce((s, x) => s + x.durationMinutes, 0);
      return {
        title: formatSectionTitle(key, totalMin),
        dayKey: key,
        data,
      };
    });
  }, [items]);

  function addMinutes(delta: number) {
    const cur = Number(minutesStr.replace(',', '.'));
    const base = Number.isFinite(cur) && cur > 0 ? cur : 0;
    const next = Math.min(1440, Math.round(base + delta));
    setMinutesStr(String(Math.max(1, next)));
  }

  async function onSubmit() {
    setFormError(null);
    const n = Number(minutesStr.replace(',', '.'));
    if (!Number.isFinite(n) || n < 1 || n > 1440) {
      setFormError('Süre 1–1440 dakika olmalı.');
      return;
    }
    const parsed = parseYmd(dateStr);
    if (!parsed) {
      setFormError('Tarih geçersiz.');
      return;
    }
    if (isAfterLocalToday(parsed)) {
      setFormError('Gelecek tarih seçilemez.');
      return;
    }
    setSubmitting(true);
    try {
      await apiPost('/studytime/add', {
        durationMinutes: Math.round(n),
        date: dateToApiIso(parsed),
        topicId: selectedTopicId,
      });
      setMinutesStr('60');
      setDateStr(todayYmd());
      setDateVal(parseYmd(todayYmd()) ?? new Date());
      setSelectedTopicId(null);
      await fetchPage(1, false);
    } catch (e) {
      setFormError(e instanceof Error ? e.message : 'Kaydedilemedi.');
    } finally {
      setSubmitting(false);
    }
  }

  function startTimer() {
    if (selectedTopicId == null) {
      Alert.alert('Konu seç', 'Kronometreyi başlatmadan önce çalışma ekle kısmından bir konu seç.');
      return;
    }
    if (isRunning || elapsedMs > 0) return;
    setElapsedMs(0);
    setStartedAtMs(Date.now());
    setIsRunning(true);
    void startStopwatchForegroundService(0);
  }

  function pauseTimer() {
    if (!isRunning || startedAtMs === null) return;
    const nextElapsed = Date.now() - startedAtMs;
    setElapsedMs(nextElapsed);
    setStartedAtMs(null);
    setIsRunning(false);
    void pauseStopwatchForegroundService(nextElapsed);
  }

  function resumeTimer() {
    if (selectedTopicId == null) {
      Alert.alert('Konu seç', 'Devam etmeden önce çalışma konusu seçili olmalı.');
      return;
    }
    if (isRunning || elapsedMs <= 0) return;
    setStartedAtMs(Date.now());
    setIsRunning(true);
    void startStopwatchForegroundService(elapsedMs);
  }

  function resetTimer() {
    setIsRunning(false);
    setElapsedMs(0);
    setStartedAtMs(null);
    void stopStopwatchForegroundService();
  }

  async function finishTimer() {
    if (selectedTopicId == null) {
      Alert.alert('Konu seç', 'Bitirmeden önce çalışma ekle kısmından bir konu seç.');
      return;
    }
    const finalMs = isRunning && startedAtMs !== null ? Date.now() - startedAtMs : elapsedMs;
    if (finalMs < 1000) {
      Alert.alert('Kronometre', 'Önce kronometreyi başlat.');
      return;
    }
    const durationMinutes = Math.max(1, Math.round(finalMs / 60000));
    setTimerSaving(true);
    try {
      await apiPost('/studytime/create', {
        durationMinutes,
        date: new Date().toISOString(),
        topicId: selectedTopicId,
      });
      resetTimer();
      await fetchPage(1, false);
      Alert.alert('Başarılı', 'Çalışmalarım bölümüne eklendi!');
    } catch (e) {
      if (isLikelyNetworkError(e)) {
        await enqueuePendingStudyTime({
          durationMinutes,
          date: new Date().toISOString(),
          topicId: selectedTopicId,
        });
        resetTimer();
        Alert.alert('İnternet yok', 'Çalışma süresi güvenli şekilde kaydedildi. Bağlantı gelince otomatik gönderilecek.');
      } else {
        Alert.alert('Kayıt Hatası', e instanceof Error ? e.message : 'Süre kaydedilemedi.');
      }
    } finally {
      setTimerSaving(false);
    }
  }

  if (loading && items.length === 0) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  const listHeader = (
    <>
      <View style={styles.form}>
        <Text style={styles.formTitle}>Çalışma ekle</Text>
        <TextInput
          style={styles.input}
          placeholder="Dakika"
          placeholderTextColor={colors.textMuted}
          keyboardAppearance={colors.keyboardAppearance}
          keyboardType="number-pad"
          value={minutesStr}
          onChangeText={setMinutesStr}
        />
        <View style={styles.quickRow}>
          {([15, 30, 60] as const).map((d) => (
            <Pressable key={d} style={styles.quickBtn} onPress={() => addMinutes(d)}>
              <Text style={styles.quickBtnText}>+{d} dk</Text>
            </Pressable>
          ))}
        </View>
        {Platform.OS === 'web' ? (
          <TextInput
            style={styles.input}
            placeholder="Tarih YYYY-AA-GG"
            placeholderTextColor={colors.textMuted}
            keyboardAppearance={colors.keyboardAppearance}
            value={dateStr}
            onChangeText={setDateStr}
          />
        ) : (
          <>
            <Pressable style={styles.dateBtn} onPress={() => setShowPicker(true)}>
              <Text style={styles.dateBtnText}>Tarih: {dateStr}</Text>
            </Pressable>
            {showPicker ? (
              <DateTimePicker
                value={dateVal}
                mode="date"
                display={Platform.OS === 'ios' ? 'spinner' : 'default'}
                maximumDate={new Date()}
                onChange={(_, selected) => {
                  if (Platform.OS === 'android') setShowPicker(false);
                  if (selected) {
                    setDateVal(selected);
                    const y = selected.getFullYear();
                    const m = String(selected.getMonth() + 1).padStart(2, '0');
                    const d = String(selected.getDate()).padStart(2, '0');
                    setDateStr(`${y}-${m}-${d}`);
                  }
                }}
              />
            ) : null}
          </>
        )}
        <Pressable style={styles.topicRow} onPress={() => setTopicModalOpen(true)}>
          <Text style={styles.topicRowText}>
            {selectedTopicLabel ? `Konu: ${selectedTopicLabel}` : 'Konu seç (Konular’daki listen, isteğe bağlı)'}
          </Text>
          <Text style={styles.topicRowHint}>İstatistiklerde konuya göre süre görmek için</Text>
        </Pressable>
        {selectedTopicId != null ? (
          <Pressable onPress={() => setSelectedTopicId(null)}>
            <Text style={{ color: colors.primary, fontSize: rs(13, scale), marginBottom: rs(8, scale) }}>
              Konu seçimini kaldır
            </Text>
          </Pressable>
        ) : null}
        <View style={styles.timerCard}>
          <Text style={styles.timerTitle}>Kronometre (Seçili Konu)</Text>
          <Text style={styles.timerDisplay}>{formatDigital(elapsedMs)}</Text>
          <Text style={styles.timerHint}>
            {selectedTopicLabel
              ? `Konu: ${selectedTopicLabel}`
              : 'Başlatmak için yukarıdan bir konu seç.'}
          </Text>
          <Text style={styles.timerHint}>Android bildiriminde canli sure gosterilir.</Text>
          <View style={styles.timerButtons}>
            {!(isRunning || elapsedMs > 0) ? (
              <Pressable style={[styles.timerBtn, styles.timerStartBtn]} onPress={startTimer} disabled={timerSaving}>
                <Text style={styles.timerBtnText}>Başlat</Text>
              </Pressable>
            ) : (
              <Pressable
                style={[styles.timerBtn, styles.timerPauseBtn]}
                onPress={isRunning ? pauseTimer : resumeTimer}
                disabled={timerSaving}
              >
                <Text style={styles.timerBtnTextMuted}>{isRunning ? 'Duraklat' : 'Devam Et'}</Text>
              </Pressable>
            )}
            <Pressable style={[styles.timerBtn, styles.timerFinishBtn]} onPress={finishTimer} disabled={timerSaving}>
              <Text style={styles.timerBtnText}>{timerSaving ? 'Kaydediliyor…' : 'Bitir'}</Text>
            </Pressable>
            <Pressable style={[styles.timerBtn, styles.timerResetBtn]} onPress={resetTimer} disabled={timerSaving}>
              <Text style={styles.timerBtnTextMuted}>Sıfırla</Text>
            </Pressable>
          </View>
        </View>
        {formError ? <Text style={styles.fieldError}>{formError}</Text> : null}
        <Pressable
          style={[styles.submitBtn, submitting && styles.submitDisabled]}
          onPress={onSubmit}
          disabled={submitting}
        >
          <Text style={styles.submitText}>{submitting ? 'Kaydediliyor…' : 'Kaydet'}</Text>
        </Pressable>
      </View>
      <Text style={styles.listHeading}>Geçmiş</Text>
    </>
  );

  return (
    <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
      {error ? <Text style={styles.bannerError}>{error}</Text> : null}

      <SectionList
        style={styles.list}
        sections={sections}
        stickySectionHeadersEnabled={false}
        keyExtractor={(it) => String(it.id)}
        ListHeaderComponent={listHeader}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        onEndReached={() => void loadMore()}
        onEndReachedThreshold={0.3}
        renderSectionHeader={({ section }) => (
          <View style={styles.sectionHeader}>
            <Text style={styles.sectionTitle}>{section.title}</Text>
          </View>
        )}
        renderItem={({ item }) => (
          <View style={styles.row}>
            <View style={styles.rowLeft}>
              <Text style={styles.rowTime}>{formatRowDate(item.date)}</Text>
              {item.topicName ? <Text style={styles.rowTopic}>{item.topicName}</Text> : null}
            </View>
            <Text style={styles.rowMin}>{item.durationMinutes} dk</Text>
          </View>
        )}
        ListFooterComponent={
          loadingMore ? <ActivityIndicator style={{ margin: rs(16, scale) }} color={colors.primary} /> : null
        }
        ListEmptyComponent={<Text style={styles.empty}>Henüz kayıt yok.</Text>}
        contentContainerStyle={listContentPad}
      />

      <TopicPickerModal
        visible={topicModalOpen}
        onClose={() => setTopicModalOpen(false)}
        topics={topicRows}
        onSelect={(row) => setSelectedTopicId(row.topicId)}
        title="Konu seç"
      />
    </SafeAreaView>
  );
}
