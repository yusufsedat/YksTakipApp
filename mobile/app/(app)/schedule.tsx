import { Ionicons } from '@expo/vector-icons';
import DateTimePicker from '@react-native-community/datetimepicker';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  SectionList,
  StyleSheet,
  Text,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { TopicPickerModal } from '../../src/components/TopicPickerModal';
import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { apiDelete, apiGet, apiPost, apiPut } from '../../src/lib/api';
import { mergeUserTopicsWithCatalog, type UserTopicRow } from '../../src/lib/userTopicRows';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import type { Paginated, ScheduleEntryDto, ScheduleListResponse, TopicDto, UserTopicDto } from '../../src/types/api';
import { useTheme } from '../../src/theme';

const DOW_TR = ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'];
const DOW_ORDER_MON_FIRST = [1, 2, 3, 4, 5, 6, 0];
const WEEK_LABELS_MON = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'];
const MONTHS_TR = [
  'Ocak',
  'Şubat',
  'Mart',
  'Nisan',
  'Mayıs',
  'Haziran',
  'Temmuz',
  'Ağustos',
  'Eylül',
  'Ekim',
  'Kasım',
  'Aralık',
];

function formatMinutes(m: number): string {
  const h = Math.floor(m / 60);
  const min = m % 60;
  return `${String(h).padStart(2, '0')}:${String(min).padStart(2, '0')}`;
}

function dateFromMinutes(m: number): Date {
  const d = new Date();
  d.setHours(Math.floor(m / 60), m % 60, 0, 0);
  return d;
}

function minutesFromDate(d: Date): number {
  return d.getHours() * 60 + d.getMinutes();
}

function isWeekly(e: ScheduleEntryDto) {
  return e.recurrence.toLowerCase() === 'weekly';
}

function getScheduleCategory(title: string): 'numeric' | 'verbal' | 'fen' | 'default' {
  const t = title.toLocaleLowerCase('tr-TR');
  if (/matematik|geometri|sayısal|tyt mat|ayt mat|logaritm|türev|integral|sayılar|olasılık/i.test(t)) {
    return 'numeric';
  }
  if (/türkçe|tarih|coğrafya|felsefe|edebiyat|sözel|paragraf|dil|anlatım|sözcük/i.test(t)) {
    return 'verbal';
  }
  if (/fizik|kimya|biyoloji|fen|ayt\s*fiz|tyt\s*fiz/i.test(t)) {
    return 'fen';
  }
  return 'default';
}

function getCalendarCells(year: number, month: number): (number | null)[] {
  const first = new Date(year, month, 1);
  const firstDow = first.getDay();
  const pad = (firstDow + 6) % 7;
  const dim = new Date(year, month + 1, 0).getDate();
  const cells: (number | null)[] = [];
  for (let i = 0; i < pad; i++) cells.push(null);
  for (let d = 1; d <= dim; d++) cells.push(d);
  return cells;
}

/** Aynı gün için zincirleme: en geç biten slottan sonra başlat (ör. 9–10 sonrası 10–11). */
const DEFAULT_CHAIN_DURATION_MIN = 60;

function computeChainedSlotMinutes(
  allItems: ScheduleEntryDto[],
  recurrence: 'Weekly' | 'Monthly',
  dayOfWeek: number,
  dayOfMonth: number,
  excludeId: number | null
): { startM: number; endM: number } {
  const daySlots = allItems.filter((e) => {
    if (excludeId != null && e.id === excludeId) return false;
    if (recurrence === 'Weekly') {
      return isWeekly(e) && e.dayOfWeek === dayOfWeek;
    }
    return e.recurrence.toLowerCase() === 'monthly' && e.dayOfMonth === dayOfMonth;
  });

  if (daySlots.length === 0) {
    return { startM: 9 * 60, endM: 10 * 60 };
  }

  const maxEnd = Math.max(...daySlots.map((s) => s.endMinute));
  const startM = maxEnd;
  let endM = Math.min(maxEnd + DEFAULT_CHAIN_DURATION_MIN, 1439);
  if (endM <= startM) {
    endM = Math.min(startM + 1, 1439);
  }
  if (endM <= startM) {
    return { startM: 9 * 60, endM: 10 * 60 };
  }
  return { startM, endM };
}

export default function ScheduleScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);

  const [items, setItems] = useState<ScheduleEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [viewTab, setViewTab] = useState<'weekly' | 'monthly'>('weekly');

  const [calendarMonth, setCalendarMonth] = useState(() => {
    const n = new Date();
    return new Date(n.getFullYear(), n.getMonth(), 1);
  });
  const [selectedDayOfMonth, setSelectedDayOfMonth] = useState(() => new Date().getDate());

  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formRecurrence, setFormRecurrence] = useState<'Weekly' | 'Monthly'>('Weekly');
  const [formDayOfWeek, setFormDayOfWeek] = useState(1);
  const [formDayOfMonth, setFormDayOfMonth] = useState(1);
  const [formTitle, setFormTitle] = useState('');
  const [formStart, setFormStart] = useState(() => dateFromMinutes(9 * 60));
  const [formEnd, setFormEnd] = useState(() => dateFromMinutes(10 * 60));
  const [showStartPicker, setShowStartPicker] = useState(false);
  const [showEndPicker, setShowEndPicker] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [topicRows, setTopicRows] = useState<UserTopicRow[]>([]);
  const [formTopicId, setFormTopicId] = useState<number | null>(null);
  const [topicModalOpen, setTopicModalOpen] = useState(false);

  const calYear = calendarMonth.getFullYear();
  const calMonth = calendarMonth.getMonth();
  const daysInVisibleMonth = useMemo(
    () => new Date(calYear, calMonth + 1, 0).getDate(),
    [calYear, calMonth]
  );

  useEffect(() => {
    setSelectedDayOfMonth((d) => Math.min(d, daysInVisibleMonth));
  }, [daysInVisibleMonth]);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        centered: { flex: 1, justifyContent: 'center', alignItems: 'center' },
        bannerError: { color: colors.errorText, padding: rs(12, scale), backgroundColor: colors.errorBg },
        headerRow: {
          flexDirection: 'row',
          marginHorizontal: rs(16, scale),
          marginTop: rs(8, scale),
          marginBottom: rs(12, scale),
          gap: rs(8, scale),
        },
        seg: {
          flex: 1,
          paddingVertical: rvs(10, vScale),
          borderRadius: rs(10, scale),
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.chip,
          alignItems: 'center',
        },
        segOn: { backgroundColor: colors.segmentOn, borderColor: colors.segmentOn },
        segText: { fontSize: rs(14, scale), fontWeight: '600', color: colors.chipText },
        segTextOn: { color: colors.segmentTextOn },
        listContent: { paddingHorizontal: rs(16, scale) },
        sectionHeader: {
          fontSize: rs(13, scale),
          fontWeight: '700',
          color: colors.textMuted,
          textTransform: 'uppercase',
          letterSpacing: 0.6,
          marginTop: rs(16, scale),
          marginBottom: rs(8, scale),
        },
        timelineRow: {
          flexDirection: 'row',
          alignItems: 'stretch',
          marginBottom: rs(10, scale),
        },
        timelineTimeCol: {
          width: rs(58, scale),
          paddingRight: rs(6, scale),
          justifyContent: 'flex-start',
          paddingTop: rs(2, scale),
        },
        timelineTime: {
          fontSize: rs(20, scale),
          fontWeight: '800',
          color: colors.text,
          letterSpacing: -0.5,
        },
        timelineBarCol: { width: rs(14, scale), alignItems: 'center' },
        timelineBar: {
          width: rs(3, scale),
          flex: 1,
          borderRadius: rs(2, scale),
          minHeight: rs(44, scale),
        },
        timelineCard: {
          flex: 1,
          borderRadius: rs(12, scale),
          borderWidth: 1,
          paddingVertical: rs(10, scale),
          paddingLeft: rs(12, scale),
          paddingRight: rs(36, scale),
        },
        timelineCardTop: {
          flexDirection: 'row',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
        },
        timelineTitle: { fontSize: rs(16, scale), fontWeight: '700', color: colors.text, flex: 1, paddingRight: rs(4, scale) },
        timelineSub: { fontSize: rs(13, scale), color: colors.textMuted, marginTop: rs(4, scale) },
        menuHit: {
          position: 'absolute',
          top: rs(8, scale),
          right: rs(8, scale),
          padding: rs(4, scale),
        },
        monthNav: {
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: rs(12, scale),
        },
        monthNavBtn: {
          padding: rs(8, scale),
          borderRadius: rs(8, scale),
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.surface,
        },
        monthTitle: { fontSize: rs(17, scale), fontWeight: '700', color: colors.text },
        calGrid: { marginBottom: rs(8, scale) },
        calWeekRow: { flexDirection: 'row', marginBottom: rs(4, scale) },
        calWeekCell: {
          flex: 1,
          alignItems: 'center',
          paddingVertical: rs(4, scale),
        },
        calWeekLabel: { fontSize: rs(11, scale), fontWeight: '600', color: colors.textMuted },
        calDayRow: { flexDirection: 'row', flexWrap: 'wrap' },
        calDayCell: {
          width: `${100 / 7}%`,
          aspectRatio: 1,
          maxHeight: rs(48, scale),
          padding: rs(2, scale),
        },
        calDayInner: {
          flex: 1,
          borderRadius: rs(8, scale),
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.surface,
          alignItems: 'center',
          justifyContent: 'center',
        },
        calDayInnerSelected: {
          borderColor: colors.primary,
          borderWidth: 2,
          backgroundColor: colors.surfaceMuted,
        },
        calDayInnerToday: { borderColor: colors.sectionAccent },
        calDayNum: { fontSize: rs(15, scale), fontWeight: '700', color: colors.text },
        calDayDot: {
          width: rs(5, scale),
          height: rs(5, scale),
          borderRadius: rs(3, scale),
          marginTop: rs(2, scale),
        },
        dayDetailHint: {
          fontSize: rs(13, scale),
          color: colors.textMuted,
          marginBottom: rs(8, scale),
        },
        empty: { color: colors.textMuted, textAlign: 'center', marginTop: rs(24, scale), paddingHorizontal: rs(24, scale) },
        modalBackdrop: { flex: 1, backgroundColor: colors.overlay, justifyContent: 'flex-end' },
        modalSheet: {
          backgroundColor: colors.surface,
          borderTopLeftRadius: rs(16, scale),
          borderTopRightRadius: rs(16, scale),
          maxHeight: '88%',
          borderWidth: 1,
          borderColor: colors.border,
        },
        modalHeader: {
          flexDirection: 'row',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: rs(16, scale),
          borderBottomWidth: StyleSheet.hairlineWidth,
          borderBottomColor: colors.border,
        },
        modalTitle: { fontSize: rs(18, scale), fontWeight: '700', color: colors.text },
        modalBody: { padding: rs(16, scale) },
        label: { fontSize: rs(13, scale), color: colors.textMuted, marginBottom: rs(6, scale) },
        input: {
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingHorizontal: rs(12, scale),
          paddingVertical: rvs(10, vScale),
          fontSize: rs(16, scale),
          marginBottom: rs(12, scale),
          backgroundColor: colors.inputBg,
          color: colors.text,
        },
        dayChipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: rs(8, scale), marginBottom: rs(12, scale) },
        dayChip: {
          paddingVertical: rs(8, scale),
          paddingHorizontal: rs(10, scale),
          borderRadius: rs(8, scale),
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.chip,
        },
        dayChipOn: { backgroundColor: colors.segmentOn, borderColor: colors.segmentOn },
        dayChipText: { fontSize: rs(12, scale), fontWeight: '600', color: colors.chipText },
        dayChipTextOn: { color: colors.segmentTextOn },
        timeBtn: {
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(12, vScale),
          paddingHorizontal: rs(12, scale),
          marginBottom: rs(12, scale),
          backgroundColor: colors.surfaceMuted,
        },
        timeBtnText: { fontSize: rs(16, scale), color: colors.text },
        formError: { color: colors.errorText, marginBottom: rs(8, scale) },
        saveBtn: {
          backgroundColor: colors.sectionAccent,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(14, vScale),
          alignItems: 'center',
          marginTop: rs(8, scale),
        },
        saveBtnText: { color: colors.onPrimary, fontWeight: '700', fontSize: rs(16, scale) },
        cancelBtn: { paddingVertical: rs(12, scale), alignItems: 'center' },
        cancelText: { color: colors.textMuted, fontSize: rs(15, scale) },
        topicPickRow: {
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(12, vScale),
          paddingHorizontal: rs(12, scale),
          marginBottom: rs(12, scale),
          backgroundColor: colors.surfaceMuted,
        },
        topicPickText: { fontSize: rs(15, scale), color: colors.text },
        topicPickHint: { fontSize: rs(12, scale), color: colors.textMuted, marginTop: rs(4, scale) },
        fab: {
          position: 'absolute',
          width: rs(56, scale),
          height: rs(56, scale),
          borderRadius: rs(28, scale),
          backgroundColor: colors.primary,
          alignItems: 'center',
          justifyContent: 'center',
          elevation: 4,
          shadowColor: '#000',
          shadowOffset: { width: 0, height: 2 },
          shadowOpacity: 0.25,
          shadowRadius: 4,
        },
      }),
    [scale, vScale, colors]
  );

  const listContentPad = useMemo(
    () => [styles.listContent, { paddingBottom: rvs(100, vScale) + insets.bottom }],
    [styles.listContent, vScale, insets.bottom]
  );

  const categoryAccent = useCallback(
    (title: string) => {
      const cat = getScheduleCategory(title);
      switch (cat) {
        case 'numeric':
          return { accent: '#1d4ed8', bg: 'rgba(29, 78, 216, 0.09)' };
        case 'verbal':
          return { accent: '#b91c1c', bg: 'rgba(185, 28, 28, 0.09)' };
        case 'fen':
          return { accent: '#15803d', bg: 'rgba(21, 128, 61, 0.09)' };
        default:
          return { accent: colors.primary, bg: colors.surfaceMuted };
      }
    },
    [colors.primary, colors.surfaceMuted]
  );

  const load = useCallback(async () => {
    setError(null);
    const res = await apiGet<ScheduleListResponse>('/schedule/list');
    setItems(res.items);
  }, []);

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

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        await load();
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : 'Program yüklenemedi.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [load]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await load();
      await loadUserTopics();
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Program yüklenemedi.');
    } finally {
      setRefreshing(false);
    }
  }, [load, loadUserTopics]);

  const weeklySections = useMemo(() => {
    const list = items.filter((e) => isWeekly(e));
    const map = new Map<number, ScheduleEntryDto[]>();
    for (const e of list) {
      if (e.dayOfWeek == null) continue;
      if (!map.has(e.dayOfWeek)) map.set(e.dayOfWeek, []);
      map.get(e.dayOfWeek)!.push(e);
    }
    return DOW_ORDER_MON_FIRST.map((dow) => ({
      title: DOW_TR[dow],
      data: (map.get(dow) || []).sort((a, b) => a.startMinute - b.startMinute),
    })).filter((s) => s.data.length > 0);
  }, [items]);

  const monthlyItems = useMemo(
    () => items.filter((e) => e.recurrence.toLowerCase() === 'monthly'),
    [items]
  );

  const monthlyByDay = useMemo(() => {
    const m = new Map<number, number>();
    for (const e of monthlyItems) {
      if (e.dayOfMonth == null) continue;
      m.set(e.dayOfMonth, (m.get(e.dayOfMonth) ?? 0) + 1);
    }
    return m;
  }, [monthlyItems]);

  const monthlyForSelectedDay = useMemo(
    () =>
      monthlyItems
        .filter((e) => e.dayOfMonth === selectedDayOfMonth)
        .sort((a, b) => a.startMinute - b.startMinute),
    [monthlyItems, selectedDayOfMonth]
  );

  const scheduleTopicLabel = useMemo(() => {
    if (formTopicId == null) return null;
    const r = topicRows.find((t) => t.topicId === formTopicId);
    return r ? `${r.category} · ${r.name}` : null;
  }, [formTopicId, topicRows]);

  const calendarCells = useMemo(() => getCalendarCells(calYear, calMonth), [calYear, calMonth]);

  const applyChainSuggestionForAdd = useCallback(
    (recurrence: 'Weekly' | 'Monthly', dayOfWeek: number, dayOfMonth: number) => {
      const { startM, endM } = computeChainedSlotMinutes(items, recurrence, dayOfWeek, dayOfMonth, null);
      setFormStart(dateFromMinutes(startM));
      setFormEnd(dateFromMinutes(endM));
    },
    [items]
  );

  function showEntryMenu(e: ScheduleEntryDto) {
    Alert.alert(
      e.title,
      `${formatMinutes(e.startMinute)} – ${formatMinutes(e.endMinute)}`,
      [
        { text: 'Düzenle', onPress: () => openEdit(e) },
        { text: 'Sil', style: 'destructive', onPress: () => confirmDelete(e) },
        { text: 'İptal', style: 'cancel' },
      ],
      { cancelable: true }
    );
  }

  function openAdd() {
    setEditingId(null);
    if (viewTab === 'weekly') {
      const rec = 'Weekly';
      const dow = 1;
      setFormRecurrence(rec);
      setFormDayOfWeek(dow);
      setFormDayOfMonth(1);
      setFormTitle('');
      setFormTopicId(null);
      applyChainSuggestionForAdd(rec, dow, formDayOfMonth);
    } else {
      const rec = 'Monthly';
      setFormRecurrence(rec);
      setFormDayOfWeek(1);
      setFormDayOfMonth(selectedDayOfMonth);
      setFormTitle('');
      setFormTopicId(null);
      applyChainSuggestionForAdd(rec, formDayOfWeek, selectedDayOfMonth);
    }
    setFormError(null);
    setModalOpen(true);
  }

  function openEdit(e: ScheduleEntryDto) {
    setEditingId(e.id);
    setFormRecurrence(isWeekly(e) ? 'Weekly' : 'Monthly');
    setFormDayOfWeek(e.dayOfWeek ?? 1);
    setFormDayOfMonth(e.dayOfMonth ?? 1);
    setFormTitle(e.title);
    setFormTopicId(e.topicId ?? null);
    setFormStart(dateFromMinutes(e.startMinute));
    setFormEnd(dateFromMinutes(e.endMinute));
    setFormError(null);
    setModalOpen(true);
  }

  async function onSave() {
    setFormError(null);
    const title = formTitle.trim();
    if (!title) {
      setFormError('Başlık girin (ör. Matematik).');
      return;
    }
    const startM = minutesFromDate(formStart);
    const endM = minutesFromDate(formEnd);
    if (endM <= startM) {
      setFormError('Bitiş saati başlangıçtan sonra olmalı.');
      return;
    }
    const body =
      formRecurrence === 'Weekly'
        ? {
            recurrence: 'Weekly',
            dayOfWeek: formDayOfWeek,
            dayOfMonth: null,
            startMinute: startM,
            endMinute: endM,
            title,
            topicId: formTopicId,
          }
        : {
            recurrence: 'Monthly',
            dayOfWeek: null,
            dayOfMonth: formDayOfMonth,
            startMinute: startM,
            endMinute: endM,
            title,
            topicId: formTopicId,
          };

    setSubmitting(true);
    try {
      if (editingId != null) {
        await apiPut(`/schedule/${editingId}`, body);
      } else {
        await apiPost<ScheduleEntryDto>('/schedule/add', body);
      }
      setModalOpen(false);
      await load();
    } catch (e) {
      setFormError(e instanceof Error ? e.message : 'Kaydedilemedi.');
    } finally {
      setSubmitting(false);
    }
  }

  function confirmDelete(e: ScheduleEntryDto) {
    Alert.alert('Silinsin mi?', `"${e.title}" programdan kaldırılacak.`, [
      { text: 'İptal', style: 'cancel' },
      {
        text: 'Sil',
        style: 'destructive',
        onPress: () => void deleteEntry(e.id),
      },
    ]);
  }

  async function deleteEntry(id: number) {
    try {
      await apiDelete(`/schedule/${id}`);
      await load();
    } catch (err) {
      Alert.alert('Hata', err instanceof Error ? err.message : 'Silinemedi.');
    }
  }

  function subtitleWeekly(e: ScheduleEntryDto): string {
    return `${formatMinutes(e.startMinute)} – ${formatMinutes(e.endMinute)}`;
  }

  function subtitleMonthly(e: ScheduleEntryDto): string {
    return `${formatMinutes(e.startMinute)} – ${formatMinutes(e.endMinute)}`;
  }

  const renderTimelineItem = (
    item: ScheduleEntryDto,
    mode: 'weekly' | 'monthly'
  ) => {
    const { accent, bg } = categoryAccent(item.title);
    const sub = mode === 'weekly' ? subtitleWeekly(item) : subtitleMonthly(item);
    return (
      <Pressable
        style={styles.timelineRow}
        onLongPress={() => showEntryMenu(item)}
        delayLongPress={350}
      >
        <View style={styles.timelineTimeCol}>
          <Text style={styles.timelineTime}>{formatMinutes(item.startMinute)}</Text>
        </View>
        <View style={styles.timelineBarCol}>
          <View style={[styles.timelineBar, { backgroundColor: accent }]} />
        </View>
        <View style={[styles.timelineCard, { borderColor: accent, backgroundColor: bg }]}>
          <View style={styles.timelineCardTop}>
            <Text style={styles.timelineTitle} numberOfLines={2}>
              {item.title}
            </Text>
          </View>
          <Text style={styles.timelineSub}>{sub}</Text>
          <Pressable
            style={styles.menuHit}
            onPress={() => showEntryMenu(item)}
            hitSlop={12}
          >
            <Ionicons name="ellipsis-horizontal" size={rs(22, scale)} color={colors.textMuted} />
          </Pressable>
        </View>
      </Pressable>
    );
  };

  const now = new Date();
  const isTodayVisible =
    now.getFullYear() === calYear && now.getMonth() === calMonth && now.getDate() === selectedDayOfMonth;

  const monthListHeader = (
    <View style={{ marginBottom: rs(4, scale) }}>
      <View style={styles.monthNav}>
        <Pressable
          style={styles.monthNavBtn}
          onPress={() => setCalendarMonth(new Date(calYear, calMonth - 1, 1))}
          accessibilityLabel="Önceki ay"
        >
          <Ionicons name="chevron-back" size={22} color={colors.text} />
        </Pressable>
        <Text style={styles.monthTitle}>
          {MONTHS_TR[calMonth]} {calYear}
        </Text>
        <Pressable
          style={styles.monthNavBtn}
          onPress={() => setCalendarMonth(new Date(calYear, calMonth + 1, 1))}
          accessibilityLabel="Sonraki ay"
        >
          <Ionicons name="chevron-forward" size={22} color={colors.text} />
        </Pressable>
      </View>
      <View style={styles.calGrid}>
        <View style={styles.calWeekRow}>
          {WEEK_LABELS_MON.map((w) => (
            <View key={w} style={styles.calWeekCell}>
              <Text style={styles.calWeekLabel}>{w}</Text>
            </View>
          ))}
        </View>
        <View style={styles.calDayRow}>
          {calendarCells.map((d, idx) => {
            if (d == null) {
              return <View key={`pad-${idx}`} style={styles.calDayCell} />;
            }
            const count = monthlyByDay.get(d) ?? 0;
            const selected = selectedDayOfMonth === d;
            const isToday =
              now.getFullYear() === calYear && now.getMonth() === calMonth && now.getDate() === d;
            return (
              <View key={d} style={styles.calDayCell}>
                <Pressable
                  onPress={() => setSelectedDayOfMonth(d)}
                  style={[
                    styles.calDayInner,
                    selected && styles.calDayInnerSelected,
                    isToday && !selected && styles.calDayInnerToday,
                  ]}
                >
                  <Text style={styles.calDayNum}>{d}</Text>
                  {count > 0 ? (
                    <View style={[styles.calDayDot, { backgroundColor: colors.primary }]} />
                  ) : (
                    <View style={{ height: rs(7, scale) }} />
                  )}
                </Pressable>
              </View>
            );
          })}
        </View>
      </View>
      <Text style={styles.dayDetailHint}>
        {selectedDayOfMonth}. gün
        {isTodayVisible ? ' · Bugün' : ''}
        {' · '}
        {monthlyForSelectedDay.length} slot
      </Text>
    </View>
  );

  if (loading && items.length === 0) {
    return (
      <View style={[styles.centered, { backgroundColor: colors.bg }]}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  const fabBottom = insets.bottom + rs(16, scale);

  return (
    <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
      {error ? <Text style={styles.bannerError}>{error}</Text> : null}

      <View style={styles.headerRow}>
        <Pressable
          style={[styles.seg, viewTab === 'weekly' && styles.segOn]}
          onPress={() => setViewTab('weekly')}
        >
          <Text style={[styles.segText, viewTab === 'weekly' && styles.segTextOn]}>Haftalık</Text>
        </Pressable>
        <Pressable
          style={[styles.seg, viewTab === 'monthly' && styles.segOn]}
          onPress={() => setViewTab('monthly')}
        >
          <Text style={[styles.segText, viewTab === 'monthly' && styles.segTextOn]}>Aylık</Text>
        </Pressable>
      </View>

      {viewTab === 'weekly' ? (
        <SectionList
          sections={weeklySections}
          keyExtractor={(item) => String(item.id)}
          contentContainerStyle={listContentPad}
          stickySectionHeadersEnabled={false}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          renderSectionHeader={({ section }) => <Text style={styles.sectionHeader}>{section.title}</Text>}
          renderItem={({ item }) => renderTimelineItem(item, 'weekly')}
          ListEmptyComponent={
            <Text style={styles.empty}>
              Henüz haftalık slot yok. Haftanın günü ve saat aralığı ekleyerek planını oluştur.
            </Text>
          }
        />
      ) : (
        <FlatList
          data={monthlyForSelectedDay}
          keyExtractor={(item) => String(item.id)}
          ListHeaderComponent={monthListHeader}
          contentContainerStyle={listContentPad}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          renderItem={({ item }) => renderTimelineItem(item, 'monthly')}
          ListEmptyComponent={
            <Text style={styles.empty}>
              {monthlyItems.length === 0
                ? 'Henüz aylık slot yok. Ayın belirli bir günü için tekrarlayan ders ekleyebilirsin.'
                : 'Bu gün için slot yok. Takvimden başka bir gün seç veya + ile ekle. Başlıktaki kelimelere göre renk (ör. matematik, türkçe) önerilir.'}
            </Text>
          }
        />
      )}

      <Pressable
        style={[styles.fab, { bottom: fabBottom, right: rs(16, scale) }]}
        onPress={openAdd}
        accessibilityLabel="Slot ekle"
      >
        <Ionicons name="add" size={rs(30, scale)} color={colors.onPrimary} />
      </Pressable>

      <Modal visible={modalOpen} animationType="slide" transparent onRequestClose={() => setModalOpen(false)}>
        <KeyboardAvoidingView
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          style={{ flex: 1 }}
        >
          <Pressable style={styles.modalBackdrop} onPress={() => setModalOpen(false)}>
            <Pressable style={styles.modalSheet} onPress={(ev) => ev.stopPropagation()}>
              <View style={styles.modalHeader}>
                <Text style={styles.modalTitle}>{editingId != null ? 'Düzenle' : 'Yeni slot'}</Text>
                <Pressable onPress={() => setModalOpen(false)}>
                  <Text style={{ color: colors.primary, fontWeight: '600', fontSize: rs(16, scale) }}>Kapat</Text>
                </Pressable>
              </View>
              <ScrollView style={styles.modalBody} keyboardShouldPersistTaps="handled">
                <Text style={styles.label}>Tür</Text>
                <View style={styles.headerRow}>
                  <Pressable
                    style={[styles.seg, formRecurrence === 'Weekly' && styles.segOn]}
                    onPress={() => {
                      setFormRecurrence('Weekly');
                      if (editingId == null) {
                        applyChainSuggestionForAdd('Weekly', formDayOfWeek, formDayOfMonth);
                      }
                    }}
                  >
                    <Text style={[styles.segText, formRecurrence === 'Weekly' && styles.segTextOn]}>Haftalık</Text>
                  </Pressable>
                  <Pressable
                    style={[styles.seg, formRecurrence === 'Monthly' && styles.segOn]}
                    onPress={() => {
                      setFormRecurrence('Monthly');
                      if (editingId == null) {
                        applyChainSuggestionForAdd('Monthly', formDayOfWeek, formDayOfMonth);
                      }
                    }}
                  >
                    <Text style={[styles.segText, formRecurrence === 'Monthly' && styles.segTextOn]}>Aylık</Text>
                  </Pressable>
                </View>

                {formRecurrence === 'Weekly' ? (
                  <>
                    <Text style={styles.label}>Gün</Text>
                    <View style={styles.dayChipRow}>
                      {DOW_ORDER_MON_FIRST.map((d) => (
                        <Pressable
                          key={d}
                          style={[styles.dayChip, formDayOfWeek === d && styles.dayChipOn]}
                          onPress={() => {
                            setFormDayOfWeek(d);
                            if (editingId == null) {
                              applyChainSuggestionForAdd('Weekly', d, formDayOfMonth);
                            }
                          }}
                        >
                          <Text style={[styles.dayChipText, formDayOfWeek === d && styles.dayChipTextOn]}>
                            {DOW_TR[d].slice(0, 3)}
                          </Text>
                        </Pressable>
                      ))}
                    </View>
                  </>
                ) : (
                  <>
                    <Text style={styles.label}>Ayın günü (1–31)</Text>
                    <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ marginBottom: rs(12, scale) }}>
                      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: rs(8, scale), maxWidth: width - rs(32, scale) }}>
                        {Array.from({ length: 31 }, (_, i) => i + 1).map((d) => (
                          <Pressable
                            key={d}
                            style={[styles.dayChip, formDayOfMonth === d && styles.dayChipOn]}
                            onPress={() => {
                              setFormDayOfMonth(d);
                              if (editingId == null) {
                                applyChainSuggestionForAdd('Monthly', formDayOfWeek, d);
                              }
                            }}
                          >
                            <Text style={[styles.dayChipText, formDayOfMonth === d && styles.dayChipTextOn]}>{d}</Text>
                          </Pressable>
                        ))}
                      </View>
                    </ScrollView>
                  </>
                )}

                {editingId == null ? (
                  <Text style={{ fontSize: rs(12, scale), color: colors.textMuted, marginBottom: rs(10, scale), lineHeight: rs(17, scale) }}>
                    Aynı gün için yeni slot eklerken başlangıç saati, o gündeki en son biten slota göre önerilir (ör. 09:00–10:00 sonrası 10:00).
                  </Text>
                ) : null}

                <Text style={styles.label}>Konu (Konular listesi)</Text>
                <Pressable style={styles.topicPickRow} onPress={() => setTopicModalOpen(true)}>
                  <Text style={styles.topicPickText}>
                    {scheduleTopicLabel ? `Konu: ${scheduleTopicLabel}` : 'Konu seç (isteğe bağlı)'}
                  </Text>
                  <Text style={styles.topicPickHint}>
                    Seçince başlık konu adıyla doldurulur; veritabanında konu ile ilişkilendirilir.
                  </Text>
                </Pressable>
                {formTopicId != null ? (
                  <Pressable onPress={() => setFormTopicId(null)}>
                    <Text style={{ color: colors.primary, fontSize: rs(13, scale), marginBottom: rs(8, scale) }}>
                      Konu bağlantısını kaldır
                    </Text>
                  </Pressable>
                ) : null}

                <Text style={styles.label}>Başlık</Text>
                <TextInput
                  style={styles.input}
                  placeholder="Örn. TYT Matematik"
                  placeholderTextColor={colors.textMuted}
                  keyboardAppearance={colors.keyboardAppearance}
                  value={formTitle}
                  onChangeText={setFormTitle}
                />

                <Text style={styles.label}>Başlangıç</Text>
                <Pressable style={styles.timeBtn} onPress={() => setShowStartPicker(true)}>
                  <Text style={styles.timeBtnText}>{formatMinutes(minutesFromDate(formStart))}</Text>
                </Pressable>
                {showStartPicker ? (
                  <DateTimePicker
                    value={formStart}
                    mode="time"
                    display={Platform.OS === 'ios' ? 'spinner' : 'default'}
                    onChange={(_, d) => {
                      setShowStartPicker(Platform.OS === 'ios');
                      if (d) setFormStart(d);
                    }}
                  />
                ) : null}

                <Text style={styles.label}>Bitiş</Text>
                <Pressable style={styles.timeBtn} onPress={() => setShowEndPicker(true)}>
                  <Text style={styles.timeBtnText}>{formatMinutes(minutesFromDate(formEnd))}</Text>
                </Pressable>
                {showEndPicker ? (
                  <DateTimePicker
                    value={formEnd}
                    mode="time"
                    display={Platform.OS === 'ios' ? 'spinner' : 'default'}
                    onChange={(_, d) => {
                      setShowEndPicker(Platform.OS === 'ios');
                      if (d) setFormEnd(d);
                    }}
                  />
                ) : null}

                {formError ? <Text style={styles.formError}>{formError}</Text> : null}

                <Pressable
                  style={[styles.saveBtn, submitting && { opacity: 0.75 }]}
                  onPress={() => void onSave()}
                  disabled={submitting}
                >
                  <Text style={styles.saveBtnText}>{submitting ? 'Kaydediliyor…' : 'Kaydet'}</Text>
                </Pressable>
                <Pressable style={styles.cancelBtn} onPress={() => setModalOpen(false)}>
                  <Text style={styles.cancelText}>Vazgeç</Text>
                </Pressable>
              </ScrollView>
            </Pressable>
          </Pressable>
        </KeyboardAvoidingView>
      </Modal>

      <TopicPickerModal
        visible={topicModalOpen}
        onClose={() => setTopicModalOpen(false)}
        topics={topicRows}
        onSelect={(row) => {
          setFormTopicId(row.topicId);
          setFormTitle(row.name);
        }}
        title="Program için konu seç"
      />
    </SafeAreaView>
  );
}
