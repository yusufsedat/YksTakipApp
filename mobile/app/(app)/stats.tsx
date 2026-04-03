import AsyncStorage from '@react-native-async-storage/async-storage';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Modal,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';

import { apiGet } from '../../src/lib/api';
import { NetTrendColumnChart, SubjectAverageBar, WeeklyMinutesChart } from '../../src/components/statsVisuals';
import type {
  AytStats,
  BransStats,
  ExamStatRow,
  StatsProgress,
  StatsSubjectWin,
  StatsSummary,
  StatsWeeklyRow,
  StatsWins,
  TytStats,
} from '../../src/types/api';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import { type ThemeColors, useTheme } from '../../src/theme';

const TABS = ['TYT', 'AYT', 'Branş'] as const;
type Tab = (typeof TABS)[number];

const NET_TARGETS_KEY = '@yks_stats_net_targets_v1';
type NetTargets = { tyt: number; ayt: number; brans: number };
const DEFAULT_NET_TARGETS: NetTargets = { tyt: 32, ayt: 30, brans: 8 };

/** AYT / branş ve bilinmeyen dersler için varsayılan üst sınır. */
const MAX_NET_BAR_DEFAULT = 40;

/**
 * TYT genel denemede soru sayısı derse göre değişir: Türkçe & Matematik 40; Fen & Sosyal 20.
 */
function maxNetForTytSubject(subject: string): number {
  const s = subject.trim().toLocaleLowerCase('tr-TR');
  if (s.includes('fen')) return 20;
  if (s.includes('sosyal')) return 20;
  if (s.includes('türkçe') || s.includes('turkce')) return 40;
  if (s.includes('matematik') || s.includes('geometri')) return 40;
  return MAX_NET_BAR_DEFAULT;
}

function formatDay(isoDate: string) {
  try {
    const [y, m, d] = isoDate.split('-').map(Number);
    const dt = new Date(y, m - 1, d);
    return dt.toLocaleDateString('tr-TR', { weekday: 'short', day: 'numeric', month: 'short' });
  } catch {
    return isoDate;
  }
}

function formatDate(iso: string) {
  try {
    return new Date(iso).toLocaleDateString('tr-TR');
  } catch {
    return iso;
  }
}

function examStreakCopy(days: number): string {
  if (days >= 2) return `${days} gündür üst üste deneme çözüyorsun!`;
  if (days === 1) return 'Serin başladı — üst üste günler ekledikçe burada parlayacak.';
  return '';
}

function subjectWinHeadline(sw: StatsSubjectWin): string {
  if (sw.completed <= 0) {
    return `${sw.subject}: listende, ilk zaferini bekliyoruz.`;
  }
  return `${sw.subject}'ten ${sw.completed} konu devirdin!`;
}

function filterExamRows(rows: ExamStatRow[], mode: 'all' | 'general' | 'branch'): ExamStatRow[] {
  if (mode === 'all') return rows;
  const t = (s: string) => s.toLowerCase();
  return rows.filter((e) => {
    const n = t(e.examName);
    const looksBranch = /branş|brans|alan/.test(n);
    if (mode === 'branch') return looksBranch;
    return !looksBranch;
  });
}

function WinsSection({ data, s }: { data: StatsWins; s: ReturnType<typeof makeStyles> }) {
  const { width } = useWindowDimensions();
  const scale = getScale(width);
  const [detail, setDetail] = useState<StatsSubjectWin | null>(null);

  const streak = examStreakCopy(data.examStreakDays);
  const rows = data.subjectWins.filter((x) => x.tracked > 0);

  return (
    <View style={s.section}>
      <Text style={s.sectionTitle}>Küçük zaferler</Text>
      <Text style={s.winsSub}>Büyük müfredat küçük adımlarla yenilir — her tamamlanan konu bir zafer.</Text>
      {streak ? (
        <View style={s.streakBanner}>
          <Text style={s.streakEmoji}>🔥</Text>
          <Text style={s.streakText}>{streak}</Text>
        </View>
      ) : rows.length === 0 ? (
        <View style={s.winsCard}>
          <Text style={s.winsMuted}>
            Listene konu ekle, deneme kaydet; tamamladıkça ve seri oluştukça burası dolar.
          </Text>
        </View>
      ) : null}
      {rows.map((sw) => {
        const ratio = sw.tracked > 0 ? Math.min(100, (sw.completed / sw.tracked) * 100) : 0;
        const pctLabel = `${Math.round(ratio)}%`;
        return (
          <Pressable
            key={`${sw.category}-${sw.subject}`}
            style={s.winRow}
            onPress={() => (sw.completed > 0 ? setDetail(sw) : undefined)}
            disabled={sw.completed <= 0}
          >
            <Text style={s.winBadge}>{sw.category}</Text>
            <Text style={s.winRowTitle}>{subjectWinHeadline(sw)}</Text>
            <Text style={s.winRowHint}>
              Bu derste listende {sw.tracked} konu var · {sw.completed} tanesini tamamladın (listedeki ilerlemen)
            </Text>
            <View style={s.winBarRow}>
              <View style={s.winBarTrack}>
                <View style={[s.winBarFill, { width: `${ratio}%` }]} />
              </View>
              <Text style={s.winPct}>{pctLabel}</Text>
            </View>
            {sw.completed > 0 ? <Text style={s.winTapHint}>Tamamlanan konuları görmek için dokun</Text> : null}
          </Pressable>
        );
      })}

      <Modal visible={detail != null} transparent animationType="slide" onRequestClose={() => setDetail(null)}>
        <Pressable style={s.winModalBackdrop} onPress={() => setDetail(null)}>
          <Pressable style={s.winModalSheet} onPress={(e) => e.stopPropagation()}>
            <Text style={s.winModalTitle}>
              {detail ? `${detail.category} · ${detail.subject}` : ''}
            </Text>
            <Text style={s.winModalSub}>
              Tamamladığın konular — küçük zaferlerinin listesi
            </Text>
            {detail && (detail.completedTopicNames?.length ?? 0) > 0 ? (
              <FlatList
                data={detail.completedTopicNames!}
                keyExtractor={(item, i) => `${item}-${i}`}
                style={{ maxHeight: rs(320, scale) }}
                renderItem={({ item }) => (
                  <View style={s.winModalItem}>
                    <Text style={s.winModalItemText}>✓ {item}</Text>
                  </View>
                )}
              />
            ) : (
              <Text style={s.winsMuted}>Konu adları bu sürümde yok; uygulamayı güncelle veya yenile.</Text>
            )}
            <Pressable style={s.winModalClose} onPress={() => setDetail(null)}>
              <Text style={s.winModalCloseText}>Kapat</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>
    </View>
  );
}

function TargetNetModal({
  visible,
  title,
  value,
  onClose,
  onSave,
  s,
}: {
  visible: boolean;
  title: string;
  value: number;
  onClose: () => void;
  onSave: (n: number) => void;
  s: ReturnType<typeof makeStyles>;
}) {
  const { colors } = useTheme();
  const [txt, setTxt] = useState(String(value));
  useEffect(() => {
    if (visible) setTxt(String(value));
  }, [visible, value]);

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={s.winModalBackdrop} onPress={onClose}>
        <Pressable style={s.targetModalSheet} onPress={(e) => e.stopPropagation()}>
          <Text style={s.winModalTitle}>{title}</Text>
          <TextInput
            style={s.targetInput}
            keyboardType="decimal-pad"
            keyboardAppearance={colors.keyboardAppearance}
            value={txt}
            onChangeText={setTxt}
            placeholder="Örn. 32"
            placeholderTextColor={colors.textMuted}
          />
          <Pressable
            style={s.winModalClose}
            onPress={() => {
              const n = Number(txt.replace(',', '.'));
              if (Number.isFinite(n) && n >= 0 && n <= 120) {
                onSave(n);
                onClose();
              }
            }}
          >
            <Text style={s.winModalCloseText}>Kaydet</Text>
          </Pressable>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

export default function StatsScreen() {
  const { colors } = useTheme();
  const [summary, setSummary] = useState<StatsSummary | null>(null);
  const [weekly, setWeekly] = useState<StatsWeeklyRow[]>([]);
  const [progress, setProgress] = useState<StatsProgress | null>(null);
  const [tytStats, setTytStats] = useState<TytStats | null>(null);
  const [aytStats, setAytStats] = useState<AytStats | null>(null);
  const [bransStats, setBransStats] = useState<BransStats | null>(null);
  const [wins, setWins] = useState<StatsWins | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>('TYT');

  const [netTargets, setNetTargets] = useState<NetTargets>(DEFAULT_NET_TARGETS);
  const [targetModal, setTargetModal] = useState<null | keyof NetTargets>(null);

  const [tytExamFilter, setTytExamFilter] = useState<'all' | 'general' | 'branch'>('all');
  const [aytExamFilter, setAytExamFilter] = useState<'all' | 'general' | 'branch'>('all');
  const [bransSubjectFilter, setBransSubjectFilter] = useState<string | 'all'>('all');

  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);
  const insets = useSafeAreaInsets();
  const s = useMemo(() => makeStyles(scale, vScale, colors), [scale, vScale, colors]);
  const scrollContentStyle = useMemo(
    () => [s.content, { paddingBottom: rvs(40, vScale) + insets.bottom }],
    [s.content, vScale, insets.bottom]
  );

  useEffect(() => {
    void AsyncStorage.getItem(NET_TARGETS_KEY).then((raw) => {
      if (!raw) return;
      try {
        const p = JSON.parse(raw) as Partial<NetTargets>;
        setNetTargets((prev) => ({
          tyt: typeof p.tyt === 'number' ? p.tyt : prev.tyt,
          ayt: typeof p.ayt === 'number' ? p.ayt : prev.ayt,
          brans: typeof p.brans === 'number' ? p.brans : prev.brans,
        }));
      } catch {
        /* ignore */
      }
    });
  }, []);

  const persistTargets = useCallback((next: NetTargets) => {
    setNetTargets(next);
    void AsyncStorage.setItem(NET_TARGETS_KEY, JSON.stringify(next));
  }, []);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [sm, w, p, tyt, ayt, brans, win] = await Promise.all([
        apiGet<StatsSummary>('/stats/summary'),
        apiGet<StatsWeeklyRow[]>('/stats/weekly'),
        apiGet<StatsProgress>('/stats/progress'),
        apiGet<TytStats>('/exam/stats/tyt'),
        apiGet<AytStats>('/exam/stats/ayt'),
        apiGet<BransStats>('/exam/stats/brans'),
        apiGet<StatsWins>('/stats/wins'),
      ]);
      setSummary(sm);
      setWeekly(Array.isArray(w) ? w : []);
      setProgress(p);
      setTytStats(tyt);
      setAytStats(ayt);
      setBransStats(brans);
      setWins(win);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'İstatistikler yüklenemedi.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }, [load]);

  if (loading && !summary) {
    return (
      <SafeAreaView style={s.centered} edges={TAB_SCREEN_EDGES}>
        <ActivityIndicator size="large" color={colors.primary} />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={s.container} edges={TAB_SCREEN_EDGES}>
      <ScrollView
        style={s.container}
        contentContainerStyle={scrollContentStyle}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
      >
        {error ? <Text style={s.error}>{error}</Text> : null}

        {wins ? <WinsSection data={wins} s={s} /> : null}

        {summary && (
          <View style={s.section}>
            <Text style={s.sectionTitle}>Özet (son 7 gün)</Text>
            <View style={s.grid}>
              <View style={s.cell}>
                <Text style={s.cellLabel}>Çalışma</Text>
                <Text style={s.cellValue}>{summary.totalMinutesLast7Days} dk</Text>
              </View>
              <View style={s.cell}>
                <Text style={s.cellLabel}>Tamamlanan küçük adım</Text>
                <Text style={s.cellValue}>{summary.completedTopics}</Text>
              </View>
              <View style={s.cell}>
                <Text style={s.cellLabel}>Ort. TYT net</Text>
                <Text style={s.cellValue}>{summary.avgTyt.toFixed(2)}</Text>
              </View>
              <View style={s.cell}>
                <Text style={s.cellLabel}>Ort. AYT net</Text>
                <Text style={s.cellValue}>{summary.avgAyt.toFixed(2)}</Text>
              </View>
            </View>
          </View>
        )}

        {progress && (
          <View style={s.section}>
            <Text style={s.sectionTitle}>Haftalık karşılaştırma</Text>
            <View style={s.card}>
              <Text style={s.line}>Bu hafta: {progress.thisWeekMinutes} dk</Text>
              <Text style={s.line}>Geçen hafta: {progress.lastWeekMinutes} dk</Text>
              <Text
                style={[
                  s.line,
                  s.highlight,
                  progress.changePercent > 0
                    ? s.changePositive
                    : progress.changePercent < 0
                      ? s.changeNegative
                      : { color: colors.textSecondary },
                ]}
              >
                Değişim: {progress.changePercent >= 0 ? '+' : ''}
                {progress.changePercent}%
              </Text>
            </View>
          </View>
        )}

        <View style={s.section}>
          <Text style={s.sectionTitle}>Son 7 gün (günlük dk)</Text>
          {weekly.length === 0 ? (
            <Text style={s.muted}>Bu aralıkta çalışma kaydı yok.</Text>
          ) : (
            <>
              <WeeklyMinutesChart rows={weekly} colors={colors} scale={scale} />
              <Text style={[s.muted, { marginTop: rs(14, scale), marginBottom: rs(6, scale) }]}>Günlük detay</Text>
              {weekly.map((row) => (
                <View key={row.date} style={s.weekRow}>
                  <Text style={s.weekDay}>{formatDay(row.date)}</Text>
                  <Text style={s.weekMin}>{row.totalMinutes} dk</Text>
                </View>
              ))}
            </>
          )}
        </View>

        <View style={s.section}>
          <Text style={s.sectionTitle}>Deneme İstatistikleri</Text>
          <View style={s.tabBar}>
            {TABS.map((t) => (
              <Pressable key={t} style={[s.tabBtn, tab === t && s.tabBtnActive]} onPress={() => setTab(t)}>
                <Text style={[s.tabText, tab === t && s.tabTextActive]}>{t === 'Branş' ? 'Branş' : `${t} İst.`}</Text>
              </Pressable>
            ))}
          </View>

          {tab === 'TYT' && (
            <TytPanel
              data={tytStats}
              s={s}
              scale={scale}
              netTarget={netTargets.tyt}
              onEditTarget={() => setTargetModal('tyt')}
              examFilter={tytExamFilter}
              setExamFilter={setTytExamFilter}
            />
          )}
          {tab === 'AYT' && (
            <AytPanel
              data={aytStats}
              s={s}
              scale={scale}
              netTarget={netTargets.ayt}
              onEditTarget={() => setTargetModal('ayt')}
              examFilter={aytExamFilter}
              setExamFilter={setAytExamFilter}
            />
          )}
          {tab === 'Branş' && (
            <BransPanel
              data={bransStats}
              s={s}
              scale={scale}
              netTarget={netTargets.brans}
              onEditTarget={() => setTargetModal('brans')}
              subjectFilter={bransSubjectFilter}
              setSubjectFilter={setBransSubjectFilter}
            />
          )}
        </View>
      </ScrollView>

      <TargetNetModal
        visible={targetModal != null}
        title={
          targetModal === 'tyt'
            ? 'TYT hedef net (çizgi)'
            : targetModal === 'ayt'
              ? 'AYT hedef net (çizgi)'
              : 'Branş hedef net (çizgi)'
        }
        value={targetModal ? netTargets[targetModal] : 0}
        onClose={() => setTargetModal(null)}
        onSave={(n) => {
          if (!targetModal) return;
          persistTargets({ ...netTargets, [targetModal]: n });
        }}
        s={s}
      />
    </SafeAreaView>
  );
}

function TytPanel({
  data,
  s,
  scale,
  netTarget,
  onEditTarget,
  examFilter,
  setExamFilter,
}: {
  data: TytStats | null;
  s: ReturnType<typeof makeStyles>;
  scale: number;
  netTarget: number;
  onEditTarget: () => void;
  examFilter: 'all' | 'general' | 'branch';
  setExamFilter: (v: 'all' | 'general' | 'branch') => void;
}) {
  const { colors } = useTheme();
  if (!data || data.examCount === 0) return <Text style={s.muted}>Henüz TYT denemesi yok.</Text>;

  const trend = data.netTrend ?? [];
  const last5 = filterExamRows(data.last5 ?? [], examFilter);

  return (
    <View style={s.panelWrap}>
      {trend.length > 0 ? (
        <View style={s.subPanel}>
          <Text style={s.subPanelTitle}>Net eğilimi (son denemeler, eski → yeni)</Text>
          <NetTrendColumnChart points={trend} colors={colors} scale={scale} />
        </View>
      ) : null}

      <View style={s.statRow}>
        <StatBox label="Deneme Sayısı" value={String(data.examCount)} s={s} />
        <StatBox label="Ort. Net" value={data.avgNet.toFixed(2)} s={s} color={colors.statAccent} />
        <StatBox label="Hız (net/saat)" value={data.speedMetric.toFixed(1)} s={s} color={colors.statAccent2} />
      </View>

      {data.subjectAverages.length > 0 && (
        <View style={s.subPanel}>
          <Text style={s.subPanelTitle}>Ders ortalamaları (hedef çizgisi)</Text>
          {data.subjectAverages.map((sa) => (
            <SubjectAverageBar
              key={sa.subject}
              label={sa.subject}
              avgNet={sa.avgNet}
              targetNet={netTarget}
              maxNet={maxNetForTytSubject(sa.subject)}
              colors={colors}
              scale={scale}
              onPressTargetHint={onEditTarget}
            />
          ))}
        </View>
      )}

      {data.last5 && data.last5.length > 0 ? (
        <View style={s.subPanel}>
          <Text style={s.subPanelTitle}>Son denemeler</Text>
          <ExamFilterChips mode={examFilter} onChange={setExamFilter} s={s} />
          {last5.length === 0 ? (
            <Text style={s.muted}>Bu filtrede kayıt yok.</Text>
          ) : (
            last5.map((e) => (
              <View key={e.id} style={s.examRow}>
                <View style={{ flex: 1 }}>
                  <Text style={s.examRowName}>{e.examName}</Text>
                  <Text style={s.examRowDate}>
                    {formatDate(e.date)}
                    {e.durationMinutes ? ` · ${e.durationMinutes} dk` : ''}
                  </Text>
                </View>
                <Text style={s.examRowNet}>{e.totalNet.toFixed(2)}</Text>
              </View>
            ))
          )}
        </View>
      ) : null}
    </View>
  );
}

function AytPanel({
  data,
  s,
  scale,
  netTarget,
  onEditTarget,
  examFilter,
  setExamFilter,
}: {
  data: AytStats | null;
  s: ReturnType<typeof makeStyles>;
  scale: number;
  netTarget: number;
  onEditTarget: () => void;
  examFilter: 'all' | 'general' | 'branch';
  setExamFilter: (v: 'all' | 'general' | 'branch') => void;
}) {
  const { colors } = useTheme();
  if (!data || data.examCount === 0) return <Text style={s.muted}>Henüz AYT denemesi yok.</Text>;

  const trend = data.netTrend ?? [];
  const last5 = filterExamRows(data.last5 ?? [], examFilter);

  return (
    <View style={s.panelWrap}>
      {trend.length > 0 ? (
        <View style={s.subPanel}>
          <Text style={s.subPanelTitle}>Net eğilimi (son denemeler, eski → yeni)</Text>
          <NetTrendColumnChart points={trend} colors={colors} scale={scale} />
        </View>
      ) : null}

      <View style={s.statRow}>
        <StatBox label="Deneme Sayısı" value={String(data.examCount)} s={s} />
        <StatBox label="Ort. Net" value={data.avgNet.toFixed(2)} s={s} color={colors.statAccent} />
      </View>

      {data.subjectAverages.length > 0 && (
        <View style={s.subPanel}>
          <Text style={s.subPanelTitle}>Ders ortalamaları (hedef çizgisi)</Text>
          {data.subjectAverages.map((sa) => (
            <SubjectAverageBar
              key={sa.subject}
              label={sa.subject}
              avgNet={sa.avgNet}
              targetNet={netTarget}
              maxNet={MAX_NET_BAR_DEFAULT}
              colors={colors}
              scale={scale}
              onPressTargetHint={onEditTarget}
            />
          ))}
        </View>
      )}

      {data.last5 && data.last5.length > 0 ? (
        <View style={s.subPanel}>
          <Text style={s.subPanelTitle}>Son denemeler</Text>
          <ExamFilterChips mode={examFilter} onChange={setExamFilter} s={s} />
          {last5.length === 0 ? (
            <Text style={s.muted}>Bu filtrede kayıt yok.</Text>
          ) : (
            last5.map((e) => (
              <View key={e.id} style={s.examRow}>
                <View style={{ flex: 1 }}>
                  <Text style={s.examRowName}>{e.examName}</Text>
                  <Text style={s.examRowDate}>
                    {formatDate(e.date)}
                    {e.durationMinutes ? ` · ${e.durationMinutes} dk` : ''}
                  </Text>
                </View>
                <Text style={s.examRowNet}>{e.totalNet.toFixed(2)}</Text>
              </View>
            ))
          )}
        </View>
      ) : null}
    </View>
  );
}

function ExamFilterChips({
  mode,
  onChange,
  s,
}: {
  mode: 'all' | 'general' | 'branch';
  onChange: (v: 'all' | 'general' | 'branch') => void;
  s: ReturnType<typeof makeStyles>;
}) {
  const chips: { key: 'all' | 'general' | 'branch'; label: string }[] = [
    { key: 'all', label: 'Tümü' },
    { key: 'general', label: 'Genel' },
    { key: 'branch', label: 'Branş / alan' },
  ];
  return (
    <View style={s.filterRow}>
      {chips.map((c) => (
        <Pressable key={c.key} style={[s.filterChip, mode === c.key && s.filterChipOn]} onPress={() => onChange(c.key)}>
          <Text style={[s.filterChipText, mode === c.key && s.filterChipTextOn]}>{c.label}</Text>
        </Pressable>
      ))}
    </View>
  );
}

function BransPanel({
  data,
  s,
  scale,
  netTarget,
  onEditTarget,
  subjectFilter,
  setSubjectFilter,
}: {
  data: BransStats | null;
  s: ReturnType<typeof makeStyles>;
  scale: number;
  netTarget: number;
  onEditTarget: () => void;
  subjectFilter: string | 'all';
  setSubjectFilter: (v: string | 'all') => void;
}) {
  const { colors } = useTheme();
  if (!data || data.examCount === 0) return <Text style={s.muted}>Henüz branş denemesi yok.</Text>;

  const trend = data.netTrend ?? [];
  const subjects = data.subjects ?? [];
  const visibleSubjects = subjectFilter === 'all' ? subjects : subjects.filter((x) => x.subject === subjectFilter);

  return (
    <View style={s.panelWrap}>
      {trend.length > 0 ? (
        <View style={s.subPanel}>
          <Text style={s.subPanelTitle}>Branş net eğilimi (tüm branşlar, eski → yeni)</Text>
          <NetTrendColumnChart points={trend} colors={colors} scale={scale} />
        </View>
      ) : null}

      <Text style={s.panelSummary}>Toplam {data.examCount} branş denemesi</Text>

      <View style={s.filterRow}>
        <Pressable style={[s.filterChip, subjectFilter === 'all' && s.filterChipOn]} onPress={() => setSubjectFilter('all')}>
          <Text style={[s.filterChipText, subjectFilter === 'all' && s.filterChipTextOn]}>Tümü</Text>
        </Pressable>
        {subjects.map((sub) => (
          <Pressable
            key={sub.subject}
            style={[s.filterChip, subjectFilter === sub.subject && s.filterChipOn]}
            onPress={() => setSubjectFilter(sub.subject)}
          >
            <Text style={[s.filterChipText, subjectFilter === sub.subject && s.filterChipTextOn]} numberOfLines={1}>
              {sub.subject}
            </Text>
          </Pressable>
        ))}
      </View>

      {visibleSubjects.map((sub) => (
        <View key={sub.subject} style={s.bransCard}>
          <View style={s.bransHeader}>
            <Text style={s.bransSubject}>{sub.subject}</Text>
            <Text style={s.bransCount}>{sub.examCount} deneme</Text>
          </View>

          <SubjectAverageBar
            label="Ortalama net"
            avgNet={sub.avgNet}
            targetNet={netTarget}
            maxNet={MAX_NET_BAR_DEFAULT}
            colors={colors}
            scale={scale}
            onPressTargetHint={onEditTarget}
          />

          {sub.difficultyDistribution.length > 0 && (
            <View style={s.diffDist}>
              <Text style={s.diffDistTitle}>Zorluk Dağılımı</Text>
              <View style={s.diffDistRow}>
                {sub.difficultyDistribution.map((d) => (
                  <View key={d.difficulty} style={s.diffDistItem}>
                    <Text style={s.diffDistLevel}>{d.difficulty}</Text>
                    <Text style={s.diffDistCount}>{d.count}</Text>
                  </View>
                ))}
              </View>
            </View>
          )}

          {sub.recentExams.length > 0 && (
            <View style={{ marginTop: 8 }}>
              <Text style={s.miniTitle}>Son Denemeler</Text>
              {sub.recentExams.map((e) => (
                <View key={e.id} style={s.examRow}>
                  <View style={{ flex: 1 }}>
                    <Text style={s.examRowName}>{e.examName}</Text>
                    <Text style={s.examRowDate}>{formatDate(e.date)}</Text>
                  </View>
                  <Text style={s.examRowNet}>{e.totalNet.toFixed(2)}</Text>
                </View>
              ))}
            </View>
          )}
        </View>
      ))}
    </View>
  );
}

function StatBox({ label, value, s, color }: { label: string; value: string; s: ReturnType<typeof makeStyles>; color?: string }) {
  return (
    <View style={s.statBox}>
      <Text style={s.statBoxLabel}>{label}</Text>
      <Text style={[s.statBoxValue, color ? { color } : undefined]}>{value}</Text>
    </View>
  );
}

function makeStyles(scale: number, vScale: number, c: ThemeColors) {
  return StyleSheet.create({
    container: { flex: 1, backgroundColor: c.bg },
    content: { padding: rs(16, scale) },
    centered: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: c.bg },
    error: { color: c.errorText, marginBottom: rs(12, scale) },

    section: { marginBottom: rs(24, scale) },
    sectionTitle: { fontSize: rs(17, scale), fontWeight: '700', color: c.text, marginBottom: rs(8, scale) },
    winsSub: { fontSize: rs(13, scale), color: c.textMuted, lineHeight: rs(19, scale), marginBottom: rs(12, scale) },
    winsCard: {
      backgroundColor: c.surface,
      borderRadius: rs(12, scale),
      padding: rs(14, scale),
      borderWidth: 1,
      borderColor: c.border,
    },
    winsMuted: { fontSize: rs(14, scale), color: c.textMuted, lineHeight: rs(20, scale) },
    streakBanner: {
      flexDirection: 'row',
      alignItems: 'center',
      gap: rs(10, scale),
      backgroundColor: c.streakBg,
      borderRadius: rs(12, scale),
      padding: rs(14, scale),
      borderWidth: 1,
      borderColor: c.streakBorder,
      marginBottom: rs(14, scale),
    },
    streakEmoji: { fontSize: rs(22, scale) },
    streakText: { flex: 1, fontSize: rs(15, scale), fontWeight: '600', color: c.streakText, lineHeight: rs(21, scale) },
    winRow: {
      backgroundColor: c.surface,
      borderRadius: rs(12, scale),
      padding: rs(14, scale),
      borderWidth: 1,
      borderColor: c.border,
      marginBottom: rs(10, scale),
    },
    winBadge: {
      alignSelf: 'flex-start',
      fontSize: rs(11, scale),
      fontWeight: '700',
      color: c.sectionAccent,
      backgroundColor: c.chipActive,
      paddingHorizontal: rs(8, scale),
      paddingVertical: rs(3, scale),
      borderRadius: rs(6, scale),
      marginBottom: rs(8, scale),
    },
    winRowTitle: { fontSize: rs(16, scale), fontWeight: '700', color: c.text, lineHeight: rs(22, scale) },
    winRowHint: { fontSize: rs(13, scale), color: c.textMuted, marginTop: rs(6, scale), lineHeight: rs(18, scale) },
    winBarRow: { flexDirection: 'row', alignItems: 'center', gap: rs(10, scale), marginTop: rs(10, scale) },
    winBarTrack: {
      flex: 1,
      height: rs(10, scale),
      backgroundColor: c.barTrack,
      borderRadius: rs(5, scale),
      overflow: 'hidden',
    },
    winBarFill: { height: '100%' as any, backgroundColor: c.barFillWin, borderRadius: rs(5, scale) },
    winPct: { fontSize: rs(14, scale), fontWeight: '800', color: c.statAccent, minWidth: rs(44, scale), textAlign: 'right' },
    winTapHint: { fontSize: rs(11, scale), color: c.primary, marginTop: rs(8, scale), fontWeight: '600' },

    winModalBackdrop: { flex: 1, backgroundColor: c.overlay, justifyContent: 'flex-end' },
    winModalSheet: {
      backgroundColor: c.surface,
      borderTopLeftRadius: rs(16, scale),
      borderTopRightRadius: rs(16, scale),
      padding: rs(20, scale),
      maxHeight: '70%',
      borderWidth: 1,
      borderColor: c.border,
    },
    winModalTitle: { fontSize: rs(18, scale), fontWeight: '700', color: c.text },
    winModalSub: { fontSize: rs(13, scale), color: c.textMuted, marginTop: rs(6, scale), marginBottom: rs(12, scale) },
    winModalItem: { paddingVertical: rs(10, scale), borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: c.border },
    winModalItemText: { fontSize: rs(15, scale), color: c.text },
    winModalClose: { marginTop: rs(16, scale), padding: rs(12, scale), alignItems: 'center' },
    winModalCloseText: { color: c.primary, fontWeight: '700', fontSize: rs(16, scale) },

    targetModalSheet: {
      backgroundColor: c.surface,
      marginHorizontal: rs(24, scale),
      borderRadius: rs(14, scale),
      padding: rs(20, scale),
      borderWidth: 1,
      borderColor: c.border,
    },
    targetInput: {
      borderWidth: 1,
      borderColor: c.border,
      borderRadius: rs(10, scale),
      padding: rs(12, scale),
      fontSize: rs(16, scale),
      color: c.text,
      marginTop: rs(12, scale),
      backgroundColor: c.inputBg,
    },

    grid: { flexDirection: 'row', flexWrap: 'wrap', gap: rs(10, scale) },
    cell: {
      width: '47%' as any,
      backgroundColor: c.surface,
      borderRadius: rs(12, scale),
      padding: rs(14, scale),
      borderWidth: 1,
      borderColor: c.border,
    },
    cellLabel: { fontSize: rs(12, scale), color: c.textMuted },
    cellValue: { fontSize: rs(20, scale), fontWeight: '700', color: c.text, marginTop: rs(6, scale) },

    card: { backgroundColor: c.surface, borderRadius: rs(12, scale), padding: rs(16, scale), borderWidth: 1, borderColor: c.border },
    line: { fontSize: rs(15, scale), color: c.textSecondary, marginBottom: rs(6, scale) },
    highlight: { fontWeight: '700', marginTop: rs(4, scale) },
    changePositive: { color: c.success },
    changeNegative: { color: '#c2410c' },

    weekRow: {
      flexDirection: 'row',
      justifyContent: 'space-between',
      paddingVertical: rvs(12, vScale),
      borderBottomWidth: StyleSheet.hairlineWidth,
      borderBottomColor: c.border,
    },
    weekDay: { fontSize: rs(15, scale), color: c.text },
    weekMin: { fontSize: rs(15, scale), fontWeight: '600', color: c.statAccent },
    muted: { color: c.textMuted, paddingVertical: rvs(8, vScale) },

    tabBar: { flexDirection: 'row', borderRadius: rs(10, scale), overflow: 'hidden', borderWidth: 1, borderColor: c.border, marginBottom: rs(16, scale) },
    tabBtn: { flex: 1, paddingVertical: rvs(10, vScale), alignItems: 'center', backgroundColor: c.bgSubtle },
    tabBtnActive: { backgroundColor: c.segmentOn },
    tabText: { fontSize: rs(13, scale), fontWeight: '600', color: c.textMuted },
    tabTextActive: { color: c.segmentTextOn },

    filterRow: { flexDirection: 'row', flexWrap: 'wrap', gap: rs(8, scale), marginBottom: rs(12, scale) },
    filterChip: {
      paddingHorizontal: rs(12, scale),
      paddingVertical: rs(8, scale),
      borderRadius: rs(20, scale),
      borderWidth: 1,
      borderColor: c.border,
      backgroundColor: c.chip,
    },
    filterChipOn: { backgroundColor: c.segmentOn, borderColor: c.segmentOn },
    filterChipText: { fontSize: rs(12, scale), fontWeight: '600', color: c.chipText },
    filterChipTextOn: { color: c.segmentTextOn },

    panelWrap: { gap: rs(12, scale) },
    panelSummary: { fontSize: rs(14, scale), color: c.textSecondary, fontWeight: '500' },

    statRow: { flexDirection: 'row', gap: rs(8, scale), flexWrap: 'wrap' },
    statBox: {
      flex: 1,
      minWidth: rs(90, scale),
      backgroundColor: c.surface,
      borderRadius: rs(10, scale),
      padding: rs(12, scale),
      borderWidth: 1,
      borderColor: c.border,
      alignItems: 'center',
    },
    statBoxLabel: { fontSize: rs(11, scale), color: c.textMuted, marginBottom: rs(4, scale) },
    statBoxValue: { fontSize: rs(18, scale), fontWeight: '700', color: c.text },

    subPanel: { backgroundColor: c.surface, borderRadius: rs(12, scale), padding: rs(14, scale), borderWidth: 1, borderColor: c.border },
    subPanelTitle: { fontSize: rs(14, scale), fontWeight: '600', color: c.textSecondary, marginBottom: rs(10, scale) },

    examRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: rvs(8, vScale), borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: c.border },
    examRowName: { fontSize: rs(14, scale), fontWeight: '500', color: c.text },
    examRowDate: { fontSize: rs(12, scale), color: c.textMuted, marginTop: rs(2, scale) },
    examRowNet: { fontSize: rs(15, scale), fontWeight: '700', color: c.netPositive },

    bransCard: { backgroundColor: c.surface, borderRadius: rs(12, scale), padding: rs(14, scale), borderWidth: 1, borderColor: c.border },
    bransHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: rs(10, scale) },
    bransSubject: { fontSize: rs(16, scale), fontWeight: '700', color: c.text },
    bransCount: { fontSize: rs(12, scale), color: c.textMuted, fontWeight: '500' },

    diffDist: { marginTop: rs(8, scale) },
    diffDistTitle: { fontSize: rs(12, scale), fontWeight: '600', color: c.textSecondary, marginBottom: rs(6, scale) },
    diffDistRow: { flexDirection: 'row', gap: rs(6, scale) },
    diffDistItem: {
      alignItems: 'center',
      backgroundColor: c.bgSubtle,
      borderRadius: rs(8, scale),
      paddingHorizontal: rs(10, scale),
      paddingVertical: rvs(4, vScale),
      borderWidth: 1,
      borderColor: c.border,
    },
    diffDistLevel: { fontSize: rs(12, scale), fontWeight: '600', color: c.difficultyOn },
    diffDistCount: { fontSize: rs(11, scale), color: c.textMuted },

    miniTitle: { fontSize: rs(12, scale), fontWeight: '600', color: c.textSecondary, marginBottom: rs(4, scale) },
  });
}
