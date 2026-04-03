import { Ionicons } from '@expo/vector-icons';
import DateTimePicker from '@react-native-community/datetimepicker';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
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

import { apiDelete, apiGet, apiPost } from '../../src/lib/api';
import { dateToApiIso, parseYmd, todayYmd } from '../../src/lib/date';
import type { ExamDto, Paginated } from '../../src/types/api';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import { type ThemeColors, useTheme } from '../../src/theme';

const EXAM_TYPES = ['TYT', 'AYT', 'YDT', 'BRANS'] as const;
type ExamType = (typeof EXAM_TYPES)[number];
const AYT_TRACKS = ['SAYISAL', 'SOZEL', 'EA'] as const;
type AytTrack = (typeof AYT_TRACKS)[number];

const TYT_SUBJECTS = ['Türkçe', 'Matematik', 'Fen Bilimleri', 'Sosyal Bilimler'];
const AYT_TRACK_SUBJECTS: Record<AytTrack, string[]> = {
  SAYISAL: ['Matematik', 'Fen Bilimleri'],
  SOZEL: ['Sosyal Bilimler', 'Türk Dili ve Edebiyatı'],
  EA: ['Matematik', 'Türk Dili ve Edebiyatı'],
};
/** Branş denemesi: TYT ve AYT dersleri ayrı gruplarda, yatay kaydırmalı seçim. */
const BRANS_GROUPS: { title: string; subjects: string[] }[] = [
  {
    title: 'TYT (Temel Yeterlilik)',
    subjects: [
      'Türkçe',
      'Matematik (Temel Matematik)',
      'Geometri',
      'Fizik',
      'Kimya',
      'Biyoloji',
      'Tarih',
      'Coğrafya',
      'Felsefe',
      'Din Kültürü',
    ],
  },
  {
    title: 'AYT (Alan Yeterlilik)',
    subjects: [
      'Matematik-2 (İleri Matematik)',
      'Geometri (AYT Seviyesi)',
      'Türk Dili ve Edebiyatı',
      'Fizik-2',
      'Kimya-2',
      'Biyoloji-2',
      'Tarih-1 (Edebiyat-Sosyal-1)',
      'Tarih-2 (Sosyal-2)',
      'Coğrafya-1',
      'Coğrafya-2',
      'Felsefe Grubu (Mantık, Psikoloji, Sosyoloji)',
      'Din Kültürü (Sosyal-2)',
    ],
  },
];
const ERROR_REASONS = ['Bilgi eksikliği', 'Süre yetmedi', 'Dikkat hatası'];

function getDefaultDuration(examType: ExamType): number {
  if (examType === 'TYT') return 135;
  if (examType === 'AYT') return 180;
  if (examType === 'YDT') return 120;
  return 0;
}

type SubjectRow = { subject: string; correct: string; wrong: string };

function calcNet(c: string, w: string): number {
  const correct = Number(c) || 0;
  const wrong = Number(w) || 0;
  return Math.round((correct - wrong / 4) * 100) / 100;
}

function getTotalQuestions(subject: string, examType: ExamType): number {
  if (examType === 'TYT') {
    if (subject === 'Türkçe') return 40;
    if (subject === 'Matematik') return 40;
    if (subject === 'Fen Bilimleri') return 20;
    if (subject === 'Sosyal Bilimler') return 20;
    return 40;
  }
  if (examType === 'AYT') {
    if (subject === 'Türk Dili ve Edebiyatı') return 40;
    if (subject === 'Matematik') return 40;
    if (subject === 'Fen Bilimleri') return 40;
    if (subject === 'Sosyal Bilimler') return 40;
    return 40;
  }
  if (examType === 'YDT') {
    return 80;
  }
  return 40;
}

function formatDateLabel(iso: string) {
  try {
    return new Date(iso).toLocaleDateString('tr-TR');
  } catch {
    return iso;
  }
}

function typeLabel(t: string) {
  if (t === 'BRANS') return 'Branş';
  if (t === 'YDT') return 'YDT';
  if (t === 'TYT' || t === 'AYT') return t;
  return t.trim() || 'Diğer';
}

const EXAM_TYPE_SECTION_ORDER = ['TYT', 'AYT', 'YDT', 'BRANS'] as const;

function stripCalendar(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}

function isCalendarAfterToday(d: Date): boolean {
  return stripCalendar(d) > stripCalendar(new Date());
}

function clampDateToTodayMidnight(d: Date): Date {
  if (!isCalendarAfterToday(d)) return d;
  const t = new Date();
  return new Date(t.getFullYear(), t.getMonth(), t.getDate(), 12, 0, 0, 0);
}

function groupExamsByType(exams: ExamDto[]): { title: string; data: ExamDto[]; key: string }[] {
  const byType = new Map<string, ExamDto[]>();
  const known = new Set<string>(EXAM_TYPE_SECTION_ORDER);
  for (const it of exams) {
    const key = (it.examType || 'TYT').trim().toUpperCase() || 'TYT';
    if (!byType.has(key)) byType.set(key, []);
    byType.get(key)!.push(it);
  }
  const orderedKeys = [
    ...EXAM_TYPE_SECTION_ORDER.filter((k) => byType.has(k)),
    ...[...byType.keys()].filter((k) => !known.has(k)).sort(),
  ];
  return orderedKeys.map((key) => {
    const data = [...(byType.get(key) || [])].sort(
      (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime()
    );
    return {
      key,
      title: typeLabel(key),
      data,
    };
  });
}

export default function ExamsScreen() {
  const { colors } = useTheme();
  const [items, setItems] = useState<ExamDto[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [examType, setExamType] = useState<ExamType>('TYT');
  const [aytTrack, setAytTrack] = useState<AytTrack>('SAYISAL');
  const [bransSubject, setBransSubject] = useState<string | null>(null);
  const [examName, setExamName] = useState('');
  const [dateStr, setDateStr] = useState(todayYmd());
  const [dateVal, setDateVal] = useState(() => parseYmd(todayYmd()) ?? new Date());
  const [showPicker, setShowPicker] = useState(false);
  const [duration, setDuration] = useState('');
  const [difficulty, setDifficulty] = useState<number>(0);
  const [bransQuestionCount, setBransQuestionCount] = useState('');
  const [selectedErrors, setSelectedErrors] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [formModalOpen, setFormModalOpen] = useState(false);
  /** Aynı anda yalnızca bir ders genişletilir; diğerleri özet satırı. */
  const [expandedSubjectIdx, setExpandedSubjectIdx] = useState<number | null>(0);

  const subjectsForType = useMemo(() => {
    if (examType === 'TYT') return TYT_SUBJECTS;
    if (examType === 'AYT') return AYT_TRACK_SUBJECTS[aytTrack];
    if (examType === 'YDT') return ['YDT'];
    if (examType === 'BRANS' && bransSubject) return [bransSubject];
    return [];
  }, [examType, aytTrack, bransSubject]);

  const [rows, setRows] = useState<SubjectRow[]>([]);

  useEffect(() => {
    const next = subjectsForType.map((s) => ({ subject: s, correct: '', wrong: '' }));
    setRows(next);
    setExpandedSubjectIdx(next.length > 0 ? 0 : null);
  }, [subjectsForType]);

  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);
  const insets = useSafeAreaInsets();

  const s = useMemo(() => makeStyles(scale, vScale, colors), [scale, vScale, colors]);
  const listContentPad = useMemo(
    () => [s.listContent, { paddingBottom: rvs(88, vScale) + insets.bottom }],
    [s.listContent, vScale, insets.bottom]
  );

  const examSections = useMemo(() => groupExamsByType(items), [items]);

  const fabBottom = insets.bottom + rs(16, scale);

  function openFormModal() {
    setFormError(null);
    setShowPicker(false);
    setFormModalOpen(true);
    setExpandedSubjectIdx(rows.length > 0 ? 0 : null);
    setDateVal((prev) => {
      const next = clampDateToTodayMidnight(prev);
      if (stripCalendar(next) !== stripCalendar(prev)) {
        const y = next.getFullYear();
        const m = String(next.getMonth() + 1).padStart(2, '0');
        const d = String(next.getDate()).padStart(2, '0');
        setDateStr(`${y}-${m}-${d}`);
      }
      return next;
    });
  }

  // --- data fetching ---
  const fetchPage = useCallback(async (p: number, append: boolean) => {
    const res = await apiGet<Paginated<ExamDto>>(`/exam/list?page=${p}&pageSize=20&sort=-date`);
    setTotal(res.meta.total);
    if (append) setItems((prev) => [...prev, ...res.items]);
    else setItems(res.items);
    setPage(p);
  }, []);

  const loadFirst = useCallback(async () => {
    setError(null);
    setLoading(true);
    try { await fetchPage(1, false); }
    catch (e) { setError(e instanceof Error ? e.message : 'Liste yüklenemedi.'); }
    finally { setLoading(false); }
  }, [fetchPage]);

  useEffect(() => { void loadFirst(); }, [loadFirst]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try { await fetchPage(1, false); setError(null); }
    catch (e) { setError(e instanceof Error ? e.message : 'Liste yüklenemedi.'); }
    finally { setRefreshing(false); }
  }, [fetchPage]);

  async function loadMore() {
    if (loadingMore || items.length >= total) return;
    setLoadingMore(true);
    try {
      const next = page + 1;
      const res = await apiGet<Paginated<ExamDto>>(`/exam/list?page=${next}&pageSize=20&sort=-date`);
      setItems((prev) => [...prev, ...res.items]);
      setPage(next);
      setTotal(res.meta.total);
    } catch { /* ignore */ }
    finally { setLoadingMore(false); }
  }

  // --- form helpers ---
  function getRowTotalQuestions(subject: string): number {
    if (examType === 'BRANS') {
      const userTotal = Number(bransQuestionCount);
      return Number.isFinite(userTotal) && userTotal > 0 ? Math.floor(userTotal) : 0;
    }
    return getTotalQuestions(subject, examType);
  }

  function updateRow(idx: number, field: 'correct' | 'wrong', val: string) {
    const onlyDigits = val.replace(/[^0-9]/g, '');
    setRows((prev) =>
      prev.map((r, i) => {
        if (i !== idx) return r;
        const total = getRowTotalQuestions(r.subject);
        const nextNum = onlyDigits === '' ? 0 : Number(onlyDigits);
        const otherNum = field === 'correct' ? (Number(r.wrong) || 0) : (Number(r.correct) || 0);
        const maxAllowed = Math.max(0, total - otherNum);
        const clamped = Math.min(nextNum, maxAllowed);
        return { ...r, [field]: onlyDigits === '' ? '' : String(clamped) };
      })
    );
  }

  function toggleError(reason: string) {
    setSelectedErrors((prev) =>
      prev.includes(reason) ? prev.filter((r) => r !== reason) : [...prev, reason]
    );
  }

  async function onSubmit() {
    setFormError(null);
    const name = examName.trim();
    if (name.length < 2) { setFormError('Deneme adı en az 2 karakter olmalı.'); return; }
    if (examType === 'BRANS' && !bransSubject) { setFormError('Branş denemesi için ders seçmelisiniz.'); return; }
    if (examType === 'AYT' && !aytTrack) { setFormError('AYT alanı seçmelisiniz.'); return; }

    const parsed = parseYmd(dateStr);
    if (!parsed) { setFormError('Tarih YYYY-AA-GG formatında olmalı.'); return; }
    if (isCalendarAfterToday(parsed)) {
      setFormError('Deneme tarihi bugünden ileri olamaz.');
      return;
    }
    if (examType === 'BRANS') {
      const q = Number(bransQuestionCount);
      if (!Number.isFinite(q) || q <= 0) {
        setFormError('Branş denemesi için toplam soru sayısını giriniz.');
        return;
      }
    }

    const details = rows.map((r) => {
      const correct = Number(r.correct) || 0;
      const wrong = Number(r.wrong) || 0;
      const total = getTotalQuestions(r.subject, examType);
      const blank = Math.max(0, total - correct - wrong);
      return { subject: r.subject, correct, wrong, blank };
    });

    const hasInvalidRow = rows.some((r) => {
      const total = getTotalQuestions(r.subject, examType);
      const correct = Number(r.correct) || 0;
      const wrong = Number(r.wrong) || 0;
      return correct + wrong > total;
    });
    if (hasInvalidRow) {
      setFormError('Bazı derslerde Doğru + Yanlış, toplam soru sayısını aşıyor.');
      return;
    }

    const totalNet = details.reduce((sum, d) => sum + (d.correct - d.wrong / 4), 0);

    setSubmitting(true);
    try {
      await apiPost('/exam/add', {
        examName: name,
        examType,
        subject: examType === 'BRANS' ? bransSubject : null,
        date: dateToApiIso(parsed),
        netTyt: examType === 'TYT' ? Math.round(totalNet * 100) / 100 : 0,
        netAyt: examType === 'AYT' ? Math.round(totalNet * 100) / 100 : 0,
        durationMinutes: duration ? Number(duration) : getDefaultDuration(examType),
        difficulty: difficulty > 0 ? difficulty : null,
        errorReasons: selectedErrors.length > 0 ? selectedErrors.join(',') : null,
        details,
      });
      resetForm();
      setFormModalOpen(false);
      await fetchPage(1, false);
    } catch (e) {
      setFormError(e instanceof Error ? e.message : 'Kaydedilemedi.');
    } finally {
      setSubmitting(false);
    }
  }

  function resetForm() {
    setExamName('');
    setDuration('');
    setDifficulty(0);
    setBransQuestionCount('');
    setSelectedErrors([]);
    setDateStr(todayYmd());
    setDateVal(parseYmd(todayYmd()) ?? new Date());
    setRows(subjectsForType.map((sub) => ({ subject: sub, correct: '', wrong: '' })));
    setExpandedSubjectIdx(subjectsForType.length > 0 ? 0 : null);
  }

  function confirmDelete(id: number, name: string) {
    Alert.alert('Sil', `"${name}" sonucunu silmek istiyor musun?`, [
      { text: 'Vazgeç', style: 'cancel' },
      {
        text: 'Sil', style: 'destructive',
        onPress: async () => {
          try { await apiDelete(`/exam/delete/${id}`); await fetchPage(1, false); }
          catch (e) { Alert.alert('Hata', e instanceof Error ? e.message : 'Silinemedi.'); }
        },
      },
    ]);
  }

  // --- render ---
  if (loading && items.length === 0) {
    return <View style={s.centered}><ActivityIndicator size="large" color={colors.primary} /></View>;
  }

  const listHeader = (
    <View>
      {error ? <Text style={s.bannerError}>{error}</Text> : null}
      <Text style={s.listHeading}>Geçmiş Denemeler</Text>
    </View>
  );

  const examFormFields = (
    <>
      <View style={s.segment}>
        {EXAM_TYPES.map((t) => (
          <Pressable
            key={t}
            style={[s.segBtn, examType === t && s.segBtnActive]}
            onPress={() => {
              setExamType(t);
              if (t !== 'BRANS') setBransSubject(null);
            }}
          >
            <Text style={[s.segText, examType === t && s.segTextActive]}>{typeLabel(t)}</Text>
          </Pressable>
        ))}
      </View>

      {examType === 'AYT' && (
        <View style={s.trackWrap}>
          {AYT_TRACKS.map((track) => (
            <Pressable
              key={track}
              style={[s.trackBtn, aytTrack === track && s.trackBtnActive]}
              onPress={() => setAytTrack(track)}
            >
              <Text style={[s.trackText, aytTrack === track && s.trackTextActive]}>
                {track === 'SAYISAL' ? 'Sayısal' : track === 'SOZEL' ? 'Sözel' : 'Eşit Ağırlık'}
              </Text>
            </Pressable>
          ))}
        </View>
      )}

      {examType === 'BRANS' && (
        <>
          {BRANS_GROUPS.map((g) => (
            <View key={g.title} style={s.bransGroup}>
              <Text style={s.bransGroupTitle}>{g.title}</Text>
              <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                style={s.bransChipScroll}
                contentContainerStyle={s.bransChipScrollContent}
              >
                {g.subjects.map((sub) => (
                  <Pressable
                    key={sub}
                    style={[s.chip, s.bransChip, bransSubject === sub && s.chipActive]}
                    onPress={() => setBransSubject(sub)}
                  >
                    <Text style={[s.chipText, s.bransChipText, bransSubject === sub && s.chipTextActive]} numberOfLines={2}>
                      {sub}
                    </Text>
                  </Pressable>
                ))}
              </ScrollView>
            </View>
          ))}
          <TextInput
            style={s.input}
            placeholder="Branş toplam soru sayısı"
            placeholderTextColor={colors.textMuted}
            keyboardAppearance={colors.keyboardAppearance}
            keyboardType="number-pad"
            value={bransQuestionCount}
            onChangeText={setBransQuestionCount}
          />
        </>
      )}

      <TextInput
        style={s.input}
        placeholder="Deneme adı"
        placeholderTextColor={colors.textMuted}
        keyboardAppearance={colors.keyboardAppearance}
        value={examName}
        onChangeText={setExamName}
      />

      {rows.length > 0 && (
        <View style={s.subjectSection}>
          <Text style={s.subSectionTitle}>Dersler — dokun: aç/kapa (tek satırda özet)</Text>
          {rows.map((r, idx) => {
            const net = calcNet(r.correct, r.wrong);
            const totalQuestions = getRowTotalQuestions(r.subject);
            const cNum = Number(r.correct) || 0;
            const wNum = Number(r.wrong) || 0;
            const blank = Math.max(0, totalQuestions - cNum - wNum);
            const expanded = expandedSubjectIdx === idx;
            return (
              <View key={r.subject} style={s.subjectCardCompact}>
                <Pressable
                  style={[s.subjectRowHeader, expanded && s.subjectRowHeaderOpen]}
                  onPress={() => setExpandedSubjectIdx(expanded ? null : idx)}
                >
                  <View style={{ flex: 1, marginRight: rs(8, scale) }}>
                    <Text style={s.subjectNameCompact} numberOfLines={1}>
                      {r.subject}
                    </Text>
                    {expanded ? (
                      <Text style={s.subjectTotalInline}>Soru: {totalQuestions || '—'}</Text>
                    ) : (
                      <Text style={s.subjectSummary} numberOfLines={1}>
                        D{cNum} Y{wNum} B{blank} · N{net.toFixed(2)}
                        {totalQuestions ? ` /${totalQuestions}` : ''}
                      </Text>
                    )}
                  </View>
                  <Ionicons name={expanded ? 'chevron-up' : 'chevron-down'} size={rs(20, scale)} color={colors.textMuted} />
                </Pressable>
                {expanded ? (
                  <View style={s.subjectInputsCompact}>
                    <View style={s.subColCompact}>
                      <Text style={s.subLabelCompact}>D</Text>
                      <TextInput
                        style={s.subInputCompact}
                        keyboardType="number-pad"
                        placeholderTextColor={colors.textMuted}
                        keyboardAppearance={colors.keyboardAppearance}
                        value={r.correct}
                        onChangeText={(v) => updateRow(idx, 'correct', v)}
                        placeholder="0"
                      />
                    </View>
                    <View style={s.subColCompact}>
                      <Text style={s.subLabelCompact}>Y</Text>
                      <TextInput
                        style={s.subInputCompact}
                        keyboardType="number-pad"
                        placeholderTextColor={colors.textMuted}
                        keyboardAppearance={colors.keyboardAppearance}
                        value={r.wrong}
                        onChangeText={(v) => updateRow(idx, 'wrong', v)}
                        placeholder="0"
                      />
                    </View>
                    <View style={s.subColCompact}>
                      <Text style={s.subLabelCompact}>B</Text>
                      <View style={[s.subInputCompact, s.netBox]}>
                        <Text style={s.netTextCompact}>{blank}</Text>
                      </View>
                    </View>
                    <View style={s.subColCompact}>
                      <Text style={s.subLabelCompact}>N</Text>
                      <View style={[s.subInputCompact, s.netBox]}>
                        <Text style={s.netTextCompact}>{net}</Text>
                      </View>
                    </View>
                  </View>
                ) : null}
              </View>
            );
          })}
          <View style={s.totalRow}>
            <Text style={s.totalLabel}>Toplam Net</Text>
            <Text style={s.totalValue}>
              {rows.reduce((sum, row) => sum + calcNet(row.correct, row.wrong), 0).toFixed(2)}
            </Text>
          </View>
        </View>
      )}

      {Platform.OS === 'web' ? (
        <>
          <TextInput
            style={s.input}
            placeholder="Tarih YYYY-AA-GG"
            placeholderTextColor={colors.textMuted}
            keyboardAppearance={colors.keyboardAppearance}
            value={dateStr}
            onChangeText={setDateStr}
          />
          <Text style={s.dateHint}>Gelecek tarih kaydedilemez; en geç bugünü seçin.</Text>
        </>
      ) : (
        <>
          <Pressable style={s.dateBtn} onPress={() => setShowPicker(true)}>
            <Text style={s.dateBtnText}>Tarih: {dateStr}</Text>
          </Pressable>
          {showPicker && formModalOpen ? (
            <DateTimePicker
              value={clampDateToTodayMidnight(dateVal)}
              mode="date"
              display={Platform.OS === 'ios' ? 'spinner' : 'default'}
              maximumDate={new Date()}
              onChange={(_, selected) => {
                if (Platform.OS === 'android') setShowPicker(false);
                if (selected) {
                  const safe = clampDateToTodayMidnight(selected);
                  setDateVal(safe);
                  const y = safe.getFullYear();
                  const m = String(safe.getMonth() + 1).padStart(2, '0');
                  const d = String(safe.getDate()).padStart(2, '0');
                  setDateStr(`${y}-${m}-${d}`);
                }
              }}
            />
          ) : null}
        </>
      )}

      <TextInput
        style={s.input}
        placeholder="Süre (dakika)"
        placeholderTextColor={colors.textMuted}
        keyboardAppearance={colors.keyboardAppearance}
        keyboardType="number-pad"
        value={duration}
        onChangeText={setDuration}
      />

      <Text style={s.fieldLabel}>Zorluk</Text>
      <View style={s.diffRow}>
        {[1, 2, 3, 4, 5].map((n) => (
          <Pressable
            key={n}
            style={[s.diffBtn, difficulty === n && s.diffBtnActive]}
            onPress={() => setDifficulty(difficulty === n ? 0 : n)}
          >
            <Text style={[s.diffText, difficulty === n && s.diffTextActive]}>{n}</Text>
          </Pressable>
        ))}
      </View>

      <Text style={s.fieldLabel}>Hata nedenleri</Text>
      <View style={s.chipWrap}>
        {ERROR_REASONS.map((reason) => {
          const active = selectedErrors.includes(reason);
          return (
            <Pressable key={reason} style={[s.chip, active && s.chipActive]} onPress={() => toggleError(reason)}>
              <Text style={[s.chipText, active && s.chipTextActive]}>{reason}</Text>
            </Pressable>
          );
        })}
      </View>

      {formError ? <Text style={s.fieldError}>{formError}</Text> : null}
      <Pressable style={[s.submitBtn, submitting && s.submitDisabled]} onPress={() => void onSubmit()} disabled={submitting}>
        <Text style={s.submitText}>{submitting ? 'Kaydediliyor…' : 'Kaydet'}</Text>
      </Pressable>
    </>
  );

  return (
    <SafeAreaView style={s.container} edges={TAB_SCREEN_EDGES}>
      <SectionList
        sections={examSections}
        keyExtractor={(it) => String(it.id)}
        stickySectionHeadersEnabled={false}
        ListHeaderComponent={listHeader}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        onEndReached={() => void loadMore()}
        onEndReachedThreshold={0.3}
        ListFooterComponent={
          loadingMore ? <ActivityIndicator style={{ margin: rs(16, scale) }} color={colors.primary} /> : null
        }
        ListEmptyComponent={<Text style={s.empty}>Henüz deneme yok.</Text>}
        renderSectionHeader={({ section }) => (
          <View style={s.listSectionHeader}>
            <Text style={s.listSectionTitle}>{section.title}</Text>
            <Text style={s.listSectionCount}>{section.data.length} kayıt</Text>
          </View>
        )}
        renderItem={({ item }) => (
          <View style={s.card}>
            <View style={s.cardTop}>
              <View style={{ flex: 1 }}>
                <View style={s.cardTitleRow}>
                  <Text style={s.examName}>{item.examName}</Text>
                  <View style={[s.typeBadge, item.examType === 'TYT' && s.badgeTyt, item.examType === 'AYT' && s.badgeAyt, item.examType === 'YDT' && s.badgeYdt, item.examType === 'BRANS' && s.badgeBrans]}>
                    <Text style={s.typeBadgeText}>{typeLabel(item.examType)}</Text>
                  </View>
                </View>
                <Text style={s.examDate}>
                  {formatDateLabel(item.date)}
                  {item.subject ? ` · ${item.subject}` : ''}
                  {item.durationMinutes ? ` · ${item.durationMinutes} dk` : ''}
                </Text>
              </View>
              <Pressable onPress={() => confirmDelete(item.id, item.examName)} style={s.delBtn}>
                <Text style={s.delText}>Sil</Text>
              </Pressable>
            </View>
            {item.details && item.details.length > 0 ? (
              <View style={s.detailsWrap}>
                {item.details.map((d) => (
                  <View key={d.id ?? d.subject} style={s.detailRow}>
                    <Text style={s.detailSubject}>{d.subject}</Text>
                    <Text style={s.detailNums}>D:{d.correct} Y:{d.wrong} B:{d.blank}</Text>
                    <Text style={s.detailNet}>Net: {d.net.toFixed(2)}</Text>
                  </View>
                ))}
                <View style={s.detailTotal}>
                  <Text style={s.detailTotalLabel}>Toplam Net</Text>
                  <Text style={s.detailTotalValue}>{item.details.reduce((sum, d) => sum + d.net, 0).toFixed(2)}</Text>
                </View>
              </View>
            ) : (
              <Text style={s.nets}>TYT: {item.netTyt} · AYT: {item.netAyt}</Text>
            )}
          </View>
        )}
        contentContainerStyle={listContentPad}
      />

      <Pressable
        style={[s.fab, { bottom: fabBottom, right: rs(16, scale) }]}
        onPress={openFormModal}
        accessibilityLabel="Deneme ekle"
      >
        <Ionicons name="add" size={rs(30, scale)} color={colors.onPrimary} />
      </Pressable>

      <Modal
        visible={formModalOpen}
        animationType="slide"
        transparent
        onRequestClose={() => {
          setFormModalOpen(false);
          setShowPicker(false);
        }}
      >
        <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
          <Pressable
            style={s.modalBackdrop}
            onPress={() => {
              setFormModalOpen(false);
              setShowPicker(false);
            }}
          >
            <Pressable style={s.modalSheet} onPress={(e) => e.stopPropagation()}>
              <View style={s.modalFormHeader}>
                <Text style={s.formTitle}>Yeni deneme</Text>
                <Pressable
                  onPress={() => {
                    setFormModalOpen(false);
                    setShowPicker(false);
                  }}
                >
                  <Text style={s.modalCloseText}>Kapat</Text>
                </Pressable>
              </View>
              <ScrollView
                style={{ maxHeight: Math.min(height * 0.72, 560) }}
                contentContainerStyle={s.modalFormScroll}
                keyboardShouldPersistTaps="handled"
              >
                <View style={s.formInModal}>{examFormFields}</View>
              </ScrollView>
            </Pressable>
          </Pressable>
        </KeyboardAvoidingView>
      </Modal>
    </SafeAreaView>
  );
}

function makeStyles(scale: number, vScale: number, c: ThemeColors) {
  return StyleSheet.create({
    container: { flex: 1, backgroundColor: c.bg },
    centered: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    bannerError: { color: c.errorText, padding: rs(12, scale), backgroundColor: c.errorBg },

    form: {
      margin: rs(16, scale),
      padding: rs(16, scale),
      backgroundColor: c.surface,
      borderRadius: rs(14, scale),
      borderWidth: 1,
      borderColor: c.border,
    },
    formTitle: { fontSize: rs(18, scale), fontWeight: '700', color: c.text, marginBottom: rs(14, scale) },

    segment: { flexDirection: 'row', marginBottom: rs(14, scale), borderRadius: rs(10, scale), overflow: 'hidden', borderWidth: 1, borderColor: c.border },
    segBtn: { flex: 1, paddingVertical: rvs(10, vScale), alignItems: 'center', backgroundColor: c.bgSubtle },
    segBtnActive: { backgroundColor: c.segmentOn },
    segText: { fontSize: rs(14, scale), fontWeight: '600', color: c.textMuted },
    segTextActive: { color: c.segmentTextOn },

    chipWrap: { flexDirection: 'row', flexWrap: 'wrap', gap: rs(8, scale), marginBottom: rs(12, scale) },
    chip: { paddingHorizontal: rs(14, scale), paddingVertical: rvs(7, vScale), borderRadius: rs(20, scale), backgroundColor: c.chip, borderWidth: 1, borderColor: c.border },
    chipActive: { backgroundColor: c.segmentOn, borderColor: c.segmentOn },
    chipText: { fontSize: rs(13, scale), color: c.textSecondary, fontWeight: '500' },
    chipTextActive: { color: c.segmentTextOn },
    bransGroup: { marginBottom: rs(10, scale) },
    bransGroupTitle: { fontSize: rs(12, scale), fontWeight: '700', color: c.textSecondary, marginBottom: rs(6, scale) },
    bransChipScroll: { flexGrow: 0, maxHeight: 72 },
    bransChipScrollContent: {
      flexDirection: 'row',
      gap: rs(8, scale),
      paddingRight: rs(8, scale),
      alignItems: 'center',
      minHeight: 48,
    },
    bransChip: { alignSelf: 'flex-start', maxWidth: rs(248, scale), minHeight: 48, justifyContent: 'center' },
    bransChipText: { lineHeight: rs(17, scale), textAlign: 'center' as const },
    trackWrap: { flexDirection: 'row', gap: rs(8, scale), marginBottom: rs(12, scale) },
    trackBtn: {
      flex: 1,
      paddingVertical: rvs(9, vScale),
      borderRadius: rs(10, scale),
      alignItems: 'center',
      borderWidth: 1,
      borderColor: c.border,
      backgroundColor: c.bgSubtle,
    },
    trackBtnActive: { backgroundColor: c.segmentOn, borderColor: c.segmentOn },
    trackText: { fontSize: rs(12, scale), color: c.textSecondary, fontWeight: '600' },
    trackTextActive: { color: c.segmentTextOn },

    input: {
      borderWidth: 1,
      borderColor: c.border,
      borderRadius: rs(10, scale),
      paddingHorizontal: rs(12, scale),
      paddingVertical: rvs(10, vScale),
      fontSize: rs(15, scale),
      marginBottom: rs(10, scale),
      backgroundColor: c.surfaceMuted,
      color: c.text,
    },

    subjectSection: { marginBottom: rs(10, scale) },
    subSectionTitle: { fontSize: rs(14, scale), fontWeight: '600', color: c.textSecondary, marginBottom: rs(8, scale) },
    subjectCard: { backgroundColor: c.bgSubtle, borderRadius: rs(10, scale), padding: rs(10, scale), marginBottom: rs(8, scale), borderWidth: 1, borderColor: c.border },
    subjectName: { fontSize: rs(14, scale), fontWeight: '600', color: c.text, marginBottom: rs(6, scale) },
    subjectTotal: { fontSize: rs(12, scale), color: c.textMuted, marginBottom: rs(6, scale) },
    subjectInputs: { flexDirection: 'row', gap: rs(6, scale) },
    subCol: { flex: 1, alignItems: 'center' },
    subLabel: { fontSize: rs(11, scale), fontWeight: '600', color: c.textMuted, marginBottom: rs(3, scale) },
    subInput: {
      borderWidth: 1,
      borderColor: c.border,
      borderRadius: rs(8, scale),
      paddingHorizontal: rs(6, scale),
      paddingVertical: rvs(7, vScale),
      fontSize: rs(14, scale),
      textAlign: 'center' as const,
      width: '100%' as any,
      backgroundColor: c.inputBg,
      color: c.text,
    },
    netBox: { backgroundColor: c.successBg, borderColor: c.successBorder, justifyContent: 'center', alignItems: 'center' },
    netText: { fontSize: rs(14, scale), fontWeight: '700', color: c.netPositive },

    totalRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingTop: rs(8, scale), borderTopWidth: 1, borderTopColor: c.border, marginTop: rs(4, scale) },
    totalLabel: { fontSize: rs(14, scale), fontWeight: '600', color: c.textSecondary },
    totalValue: { fontSize: rs(16, scale), fontWeight: '700', color: c.statAccent },

    dateBtn: {
      borderWidth: 1,
      borderColor: c.border,
      borderRadius: rs(10, scale),
      paddingVertical: rvs(12, vScale),
      paddingHorizontal: rs(12, scale),
      marginBottom: rs(10, scale),
      backgroundColor: c.surfaceMuted,
    },
    dateBtnText: { fontSize: rs(15, scale), color: c.text },
    dateHint: {
      fontSize: rs(12, scale),
      color: c.textMuted,
      marginTop: rs(-4, scale),
      marginBottom: rs(10, scale),
      lineHeight: rs(17, scale),
    },

    fieldLabel: { fontSize: rs(13, scale), fontWeight: '600', color: c.textSecondary, marginBottom: rs(6, scale), marginTop: rs(4, scale) },

    diffRow: { flexDirection: 'row', gap: rs(8, scale), marginBottom: rs(12, scale) },
    diffBtn: { width: rs(40, scale), height: rs(40, scale), borderRadius: rs(20, scale), justifyContent: 'center', alignItems: 'center', backgroundColor: c.chip, borderWidth: 1, borderColor: c.border },
    diffBtnActive: { backgroundColor: c.difficultyOn, borderColor: c.difficultyOn },
    diffText: { fontSize: rs(15, scale), fontWeight: '700', color: c.textMuted },
    diffTextActive: { color: c.onPrimary },

    fieldError: { color: c.errorText, marginBottom: rs(8, scale), fontSize: rs(13, scale) },
    submitBtn: {
      backgroundColor: c.admin,
      borderRadius: rs(10, scale),
      paddingVertical: rvs(13, vScale),
      alignItems: 'center',
      marginTop: rs(4, scale),
    },
    submitDisabled: { opacity: 0.7 },
    submitText: { color: c.onAdmin, fontWeight: '600', fontSize: rs(16, scale) },

    listHeading: {
      fontSize: rs(15, scale),
      fontWeight: '600',
      color: c.textMuted,
      marginHorizontal: rs(16, scale),
      marginBottom: rs(8, scale),
    },
    listSectionHeader: {
      flexDirection: 'row',
      alignItems: 'baseline',
      justifyContent: 'space-between',
      paddingHorizontal: rs(16, scale),
      paddingTop: rs(12, scale),
      paddingBottom: rs(6, scale),
      marginTop: rs(4, scale),
      borderBottomWidth: StyleSheet.hairlineWidth,
      borderBottomColor: c.border,
      backgroundColor: c.bg,
    },
    listSectionTitle: {
      fontSize: rs(13, scale),
      fontWeight: '800',
      color: c.textSecondary,
      letterSpacing: 0.4,
    },
    listSectionCount: { fontSize: rs(12, scale), color: c.textMuted, fontWeight: '600' },
    listContent: { paddingHorizontal: rs(16, scale) },
    card: {
      backgroundColor: c.surface,
      borderRadius: rs(12, scale),
      padding: rs(14, scale),
      marginBottom: rs(10, scale),
      borderWidth: 1,
      borderColor: c.border,
    },
    cardTop: { flexDirection: 'row', alignItems: 'flex-start' },
    cardTitleRow: { flexDirection: 'row', alignItems: 'center', gap: rs(8, scale), flexWrap: 'wrap' },
    examName: { fontSize: rs(16, scale), fontWeight: '600', color: c.text },
    typeBadge: { paddingHorizontal: rs(8, scale), paddingVertical: rvs(2, vScale), borderRadius: rs(6, scale) },
    badgeTyt: { backgroundColor: c.badgeTyt },
    badgeAyt: { backgroundColor: c.badgeAyt },
    badgeYdt: { backgroundColor: c.badgeYdt },
    badgeBrans: { backgroundColor: c.badgeBrans },
    typeBadgeText: { fontSize: rs(11, scale), fontWeight: '700', color: c.badgeText },
    examDate: { fontSize: rs(13, scale), color: c.textMuted, marginTop: rs(4, scale) },
    nets: { marginTop: rs(10, scale), fontSize: rs(15, scale), color: c.textSecondary },
    delBtn: { paddingHorizontal: rs(10, scale), paddingVertical: rvs(6, vScale) },
    delText: { color: c.dangerText, fontWeight: '600' },

    detailsWrap: { marginTop: rs(10, scale), gap: rs(4, scale) },
    detailRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: rvs(4, vScale), borderBottomWidth: 1, borderBottomColor: c.border },
    detailSubject: { flex: 2, fontSize: rs(13, scale), fontWeight: '500', color: c.textSecondary },
    detailNums: { flex: 2, fontSize: rs(12, scale), color: c.textMuted, textAlign: 'center' as const },
    detailNet: { flex: 1, fontSize: rs(13, scale), fontWeight: '600', color: c.netPositive, textAlign: 'right' as const },
    detailTotal: { flexDirection: 'row', justifyContent: 'space-between', paddingTop: rs(6, scale), marginTop: rs(4, scale), borderTopWidth: 1, borderTopColor: c.border },
    detailTotalLabel: { fontSize: rs(13, scale), fontWeight: '600', color: c.textSecondary },
    detailTotalValue: { fontSize: rs(14, scale), fontWeight: '700', color: c.statAccent },

    empty: { color: c.textMuted, textAlign: 'center', marginTop: rs(12, scale) },

    fab: {
      position: 'absolute' as const,
      width: rs(56, scale),
      height: rs(56, scale),
      borderRadius: rs(28, scale),
      backgroundColor: c.primary,
      alignItems: 'center',
      justifyContent: 'center',
      elevation: 4,
      shadowColor: '#000',
      shadowOffset: { width: 0, height: 2 },
      shadowOpacity: 0.25,
      shadowRadius: 4,
    },
    modalBackdrop: { flex: 1, backgroundColor: c.overlay, justifyContent: 'flex-end' },
    modalSheet: {
      backgroundColor: c.bg,
      borderTopLeftRadius: rs(16, scale),
      borderTopRightRadius: rs(16, scale),
      maxHeight: '90%',
      borderTopWidth: 1,
      borderColor: c.border,
    },
    modalFormScroll: { paddingBottom: rvs(32, vScale) },
    modalFormHeader: {
      flexDirection: 'row',
      justifyContent: 'space-between',
      alignItems: 'center',
      paddingHorizontal: rs(16, scale),
      paddingVertical: rvs(14, vScale),
      borderBottomWidth: StyleSheet.hairlineWidth,
      borderBottomColor: c.border,
      backgroundColor: c.surface,
    },
    modalCloseText: { color: c.primary, fontWeight: '600', fontSize: rs(16, scale) },
    formInModal: { paddingHorizontal: rs(16, scale), paddingTop: rs(12, scale) },

    subjectCardCompact: {
      backgroundColor: c.bgSubtle,
      borderRadius: rs(10, scale),
      marginBottom: rs(6, scale),
      borderWidth: 1,
      borderColor: c.border,
      overflow: 'hidden',
    },
    subjectRowHeader: {
      flexDirection: 'row',
      alignItems: 'center',
      paddingHorizontal: rs(10, scale),
      paddingVertical: rvs(8, vScale),
    },
    subjectRowHeaderOpen: { backgroundColor: c.surfaceMuted },
    subjectNameCompact: { fontSize: rs(13, scale), fontWeight: '700', color: c.text },
    subjectSummary: { fontSize: rs(11, scale), color: c.textMuted, marginTop: rs(2, scale) },
    subjectTotalInline: { fontSize: rs(11, scale), color: c.textMuted, marginTop: rs(2, scale) },
    subjectInputsCompact: {
      flexDirection: 'row',
      gap: rs(4, scale),
      paddingHorizontal: rs(8, scale),
      paddingBottom: rs(8, scale),
    },
    subColCompact: { flex: 1, minWidth: 0, alignItems: 'center' },
    subLabelCompact: { fontSize: rs(9, scale), fontWeight: '700', color: c.textMuted, marginBottom: rs(2, scale) },
    subInputCompact: {
      borderWidth: 1,
      borderColor: c.border,
      borderRadius: rs(6, scale),
      paddingHorizontal: rs(2, scale),
      paddingVertical: rvs(5, vScale),
      fontSize: rs(12, scale),
      textAlign: 'center' as const,
      width: '100%' as any,
      minHeight: rs(34, scale),
      backgroundColor: c.inputBg,
      color: c.text,
    },
    netTextCompact: { fontSize: rs(12, scale), fontWeight: '700', color: c.netPositive },
  });
}

