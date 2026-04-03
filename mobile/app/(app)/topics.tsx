import AsyncStorage from '@react-native-async-storage/async-storage';
import { Ionicons } from '@expo/vector-icons';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  Modal,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  useWindowDimensions,
} from 'react-native';
import { GestureHandlerRootView, Swipeable } from 'react-native-gesture-handler';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiDelete, apiGet, apiPost } from '../../src/lib/api';
import { useAuth } from '../../src/lib/auth';
import type { Paginated, TopicDto, UserTopicDto } from '../../src/types/api';
import {
  TopicStatus,
  topicStatusLabel,
  topicStatusOptions,
  type TopicStatusValue,
} from '../../src/constants/topicStatus';
import { CURRICULUM_SECTION_TITLE, sortSubjectsForCategory } from '../../src/constants/curriculumSubjects';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import {
  isLikelyTestTopicName,
  mergeUserTopicsWithCatalog,
  type UserTopicRow,
} from '../../src/lib/userTopicRows';
import { type ThemeColors, useTheme } from '../../src/theme';

const TOPICS_INTRO_COMPACT_KEY = '@yks_topics_intro_compact';

type Row = UserTopicRow;

type AddListItem =
  | { kind: 'header'; key: string; title: string }
  | { kind: 'topic'; key: string; topic: TopicDto };

function statusColorsFor(c: ThemeColors): Record<number, string> {
  return {
    0: c.statusIdle,
    1: c.statusProgress,
    2: c.statusDone,
    3: c.statusReview,
  };
}

function buildTopicStyleSheet(c: ThemeColors) {
  return {
    list: { flex: 1 },
    container: { flex: 1, backgroundColor: c.bg },
    centered: { flex: 1, justifyContent: 'center' as const, alignItems: 'center' as const, backgroundColor: c.bg },
    bannerError: { color: c.errorText, padding: 12, backgroundColor: c.errorBg },
    celebrateBanner: {
      backgroundColor: c.celebrateBg,
      borderWidth: 1,
      borderColor: c.celebrateBorder,
      borderRadius: 12,
      paddingVertical: 12,
      paddingHorizontal: 14,
    },
    celebrateText: { color: c.celebrateText, fontSize: 15, fontWeight: '600' as const, lineHeight: 21 },
    motivationTitle: { fontSize: 17, fontWeight: '800' as const, color: c.text },
    motivationSub: { fontSize: 13, color: c.textMuted, marginTop: 6, lineHeight: 19 },
    addBtn: {
      margin: 16,
      backgroundColor: c.primary,
      paddingVertical: 12,
      borderRadius: 10,
      alignItems: 'center' as const,
    },
    addBtnText: { color: c.onPrimary, fontWeight: '600' as const, fontSize: 15 },
    searchInput: {
      borderWidth: 1,
      borderColor: c.inputBorder,
      borderRadius: 10,
      backgroundColor: c.inputBg,
      paddingHorizontal: 12,
      paddingVertical: 10,
      fontSize: 15,
      color: c.text,
    },
    listContent: { paddingHorizontal: 16, paddingBottom: 32 },
    card: {
      flexDirection: 'row' as const,
      alignItems: 'stretch' as const,
      backgroundColor: c.surface,
      borderRadius: 12,
      marginBottom: 10,
      borderWidth: 1,
      borderColor: c.border,
      overflow: 'hidden' as const,
    },
    cardDone: {
      borderLeftWidth: 4,
      borderLeftColor: c.statusDone,
    },
    cardMain: { flex: 1, padding: 14 },
    cardTitleRow: { flexDirection: 'row' as const, alignItems: 'flex-start' as const, gap: 8 },
    cardCheck: { marginTop: 2 },
    swipeDelete: {
      width: 72,
      justifyContent: 'center' as const,
      alignItems: 'center' as const,
      backgroundColor: 'rgba(185, 61, 74, 0.92)',
    },
    cardTitle: { fontSize: 17, fontWeight: '700' as const, color: c.text },
    cardCat: { fontSize: 14, color: c.textMuted, marginTop: 5 },
    cardStatus: { fontSize: 15, color: c.primaryMuted, marginTop: 9, fontWeight: '600' as const },
    cardHint: { fontSize: 13, color: c.textMuted, marginTop: 6 },
    empty: { color: c.textMuted, textAlign: 'center' as const, marginTop: 24, paddingHorizontal: 16 },
    modalOverlay: {
      flex: 1,
      backgroundColor: c.overlay,
      justifyContent: 'center' as const,
      padding: 14,
    },
    modalBox: {
      backgroundColor: c.modalSurface,
      borderRadius: 14,
      padding: 16,
    },
    modalBoxTall: {
      backgroundColor: c.modalSurface,
      borderRadius: 14,
      padding: 16,
      width: '100%' as const,
      alignSelf: 'stretch' as const,
      overflow: 'hidden' as const,
    },
    addList: { marginBottom: 6 },
    modalTitle: { fontSize: 18, fontWeight: '700' as const, color: c.text },
    modalHint: { fontSize: 13, color: c.textMuted, marginBottom: 12, lineHeight: 18 },
    modalSub: { fontSize: 14, color: c.textMuted, marginBottom: 12 },
    segRow: { flexDirection: 'row' as const, gap: 8, marginBottom: 12 },
    segBtn: {
      flex: 1,
      paddingVertical: 10,
      borderRadius: 10,
      backgroundColor: c.segmentOff,
      alignItems: 'center' as const,
    },
    segBtnOn: { backgroundColor: c.segmentOn },
    segText: { fontSize: 15, fontWeight: '600' as const, color: c.segmentText },
    segTextOn: { color: c.segmentTextOn, fontWeight: '800' as const },
    curriculumSectionTitle: {
      fontSize: 13,
      fontWeight: '700' as const,
      color: c.sectionAccent,
      marginBottom: 8,
    },
    branchesLabel: { fontSize: 12, fontWeight: '600' as const, color: c.textMuted, marginBottom: 6 },
    chipWrap: { flexDirection: 'row' as const, flexWrap: 'wrap' as const, gap: 8, marginBottom: 10 },
    chipScroll: { marginBottom: 10, flexGrow: 0, maxHeight: 46 },
    chipScrollContent: {
      flexDirection: 'row' as const,
      gap: 8,
      paddingRight: 8,
      alignItems: 'center' as const,
      minHeight: 42,
    },
    subjectFilterScroll: { flexGrow: 0, maxHeight: 46 },
    subjectFilterScrollContent: {
      flexDirection: 'row' as const,
      gap: 8,
      paddingRight: 8,
      alignItems: 'center' as const,
      minHeight: 42,
    },
    chip: {
      alignSelf: 'flex-start' as const,
      paddingHorizontal: 14,
      paddingVertical: 10,
      borderRadius: 20,
      backgroundColor: c.chip,
    },
    chipOn: { backgroundColor: c.chipActive },
    chipText: { fontSize: 14, color: c.chipText, lineHeight: 18 },
    chipTextOn: { color: c.chipTextActive, fontWeight: '800' as const },
    emptySmall: { color: c.textMuted, textAlign: 'center' as const, paddingVertical: 16 },
    groupHeader: {
      fontSize: 13,
      fontWeight: '700' as const,
      color: c.groupHeaderText,
      backgroundColor: c.groupHeaderBg,
      paddingVertical: 6,
      paddingHorizontal: 8,
      borderRadius: 6,
      marginTop: 8,
    },
    modalRow: {
      paddingVertical: 12,
      borderBottomWidth: StyleSheet.hairlineWidth,
      borderBottomColor: c.border,
    },
    modalRowText: { fontSize: 16, color: c.text },
    modalRowDanger: {
      marginTop: 8,
      paddingVertical: 12,
      alignItems: 'center' as const,
      borderTopWidth: StyleSheet.hairlineWidth,
      borderTopColor: c.border,
    },
    modalRowDangerText: { color: c.dangerText, fontSize: 16, fontWeight: '600' as const },
    modalCancel: {
      marginTop: 12,
      alignItems: 'center' as const,
      justifyContent: 'center' as const,
      paddingVertical: 12,
      width: '100%' as const,
      borderRadius: 10,
      backgroundColor: c.segmentOff,
      borderWidth: 1,
      borderColor: c.border,
    },
    modalCancelText: { color: c.text, fontSize: 16, fontWeight: '700' as const },
    listMetaText: { fontSize: 12, color: c.textMuted, marginBottom: 8 },
    adminBtn: {
      backgroundColor: c.admin,
      borderRadius: 10,
      alignItems: 'center' as const,
      justifyContent: 'center' as const,
    },
    adminBtnText: { color: c.onAdmin, fontWeight: '800' as const, fontSize: 15 },
    adminInput: {
      marginTop: 12,
      borderWidth: 1,
      borderColor: c.border,
      borderRadius: 10,
      paddingHorizontal: 14,
      paddingVertical: 10,
      fontSize: 16,
      backgroundColor: c.inputBg,
      color: c.text,
    },
    adminSubmitBtn: {
      marginTop: 14,
      backgroundColor: c.primary,
      borderRadius: 10,
      paddingVertical: 12,
      alignItems: 'center' as const,
      justifyContent: 'center' as const,
    },
    adminSubmitText: { color: c.onPrimary, fontWeight: '800' as const, fontSize: 16 },
    submitDisabled: { opacity: 0.7 },
    addRow: {
      paddingVertical: 12,
      borderBottomWidth: StyleSheet.hairlineWidth,
      borderBottomColor: c.border,
    },
    addRowTitle: { fontSize: 16, color: c.text },
    addRowCat: { fontSize: 13, color: c.textMuted, marginTop: 2 },
    stickySection: {
      backgroundColor: c.bg,
      paddingTop: 4,
      paddingBottom: 10,
      borderBottomWidth: StyleSheet.hairlineWidth,
      borderBottomColor: c.border,
    },
  };
}

function detectSubGroup(subject: string, topicName: string): string {
  if (subject === 'Fizik' || subject === 'Kimya' || subject === 'Biyoloji') return subject;
  if (subject !== 'Fen Bilimleri') return subject;
  const n = topicName.toLocaleLowerCase('tr');
  if (n.includes('fizik')) return 'Fizik';
  if (n.includes('kimya')) return 'Kimya';
  if (n.includes('biyoloji')) return 'Biyoloji';
  return 'Fen Bilimleri';
}

function isLikelyTestSubjectName(subject: string): boolean {
  const s = subject.trim().toLocaleLowerCase('tr');
  if (!s) return true;
  return s.includes('dadsa') || s.includes('asdf') || s === 'x' || s === 'qwe';
}

export default function TopicsScreen() {
  const { colors } = useTheme();
  const statusColorByValue = useMemo(() => statusColorsFor(colors), [colors]);
  const styles = useMemo(() => StyleSheet.create(buildTopicStyleSheet(colors)), [colors]);

  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);
  const insets = useSafeAreaInsets();

  const { user } = useAuth();
  const isAdmin = (user?.role ?? '').trim().toLowerCase() === 'admin';

  const [rows, setRows] = useState<Row[]>([]);
  const [catalog, setCatalog] = useState<TopicDto[]>([]);
  const [userTopicIds, setUserTopicIds] = useState<Set<number>>(new Set());
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [statusModal, setStatusModal] = useState<Row | null>(null);
  const [addOpen, setAddOpen] = useState(false);
  const [addTabCat, setAddTabCat] = useState<'TYT' | 'AYT'>('TYT');
  const [addTabSubject, setAddTabSubject] = useState('');
  const [listTabCat, setListTabCat] = useState<'TYT' | 'AYT'>('TYT');
  const [listTabSubject, setListTabSubject] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);
  const [searchText, setSearchText] = useState('');
  const [scrollY, setScrollY] = useState(0);
  const [introCompactPersisted, setIntroCompactPersisted] = useState(false);
  const introPersistGateRef = useRef(false);
  const [addSearchText, setAddSearchText] = useState('');
  const [celebrateMsg, setCelebrateMsg] = useState<string | null>(null);
  const celebrateTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Admin: global kataloğa konu ekleme
  const [adminOpen, setAdminOpen] = useState(false);
  const [adminName, setAdminName] = useState('');
  const [adminCategory, setAdminCategory] = useState<'TYT' | 'AYT'>('TYT');
  const [adminSubject, setAdminSubject] = useState('');
  const [adminCustomSubject, setAdminCustomSubject] = useState('');
  const [adminSubmitting, setAdminSubmitting] = useState(false);

  // Ekrana göre responsive: küçük ekranda modal daralsın, büyük ekranda taşmasın.
  const modalMaxHeight = Math.min(Math.round(height * 0.86), 680);
  const addListMaxHeight = Math.min(Math.round(height * 0.46), 420);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [catRes, userRes] = await Promise.all([
        apiGet<Paginated<TopicDto>>('/topics?page=1&pageSize=500'),
        apiGet<UserTopicDto[]>('/user/topics'),
      ]);
      const cleanedCatalog = catRes.items.filter((t) => !isLikelyTestTopicName(t.name));
      setCatalog(cleanedCatalog);
      const idSet = new Set(userRes.map((u) => u.topicId));
      setUserTopicIds(idSet);
      setRows(mergeUserTopicsWithCatalog(catRes.items, userRes));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Konular yüklenemedi.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    return () => {
      if (celebrateTimerRef.current) clearTimeout(celebrateTimerRef.current);
    };
  }, []);

  useEffect(() => {
    void AsyncStorage.getItem(TOPICS_INTRO_COMPACT_KEY).then((v) => {
      if (v === '1') {
        setIntroCompactPersisted(true);
        introPersistGateRef.current = true;
      }
    });
  }, []);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }, [load]);

  const availableToAdd = useMemo(() => {
    return catalog.filter((t) => {
      if (userTopicIds.has(t.id)) return false;
      const sub = t.subject?.trim() || 'Diğer';
      if (isLikelyTestSubjectName(sub)) return false;
      return true;
    });
  }, [catalog, userTopicIds]);

  const subjectOptions = useMemo(() => {
    const s = new Set<string>();
    for (const t of availableToAdd) {
      if (t.category !== addTabCat) continue;
      const sub = t.subject?.trim() ? t.subject : 'Diğer';
      if (isLikelyTestSubjectName(sub)) continue;
      s.add(sub);
    }
    return sortSubjectsForCategory(addTabCat, [...s]);
  }, [availableToAdd, addTabCat]);

  const filteredAdd = useMemo(() => {
    return availableToAdd.filter((t) => {
      if (t.category !== addTabCat) return false;
      const sub = t.subject?.trim() ? t.subject : 'Diğer';
      if (sub !== addTabSubject) return false;
      if (!addSearchText.trim()) return true;
      return t.name.toLocaleLowerCase('tr').includes(addSearchText.trim().toLocaleLowerCase('tr'));
    });
  }, [availableToAdd, addTabCat, addTabSubject, addSearchText]);

  const filteredRows = useMemo(() => {
    if (!searchText.trim()) return rows;
    const q = searchText.trim().toLocaleLowerCase('tr');
    return rows.filter(
      (r) =>
        r.name.toLocaleLowerCase('tr').includes(q) ||
        r.subject.toLocaleLowerCase('tr').includes(q) ||
        r.category.toLocaleLowerCase('tr').includes(q)
    );
  }, [rows, searchText]);

  const listSubjectOptions = useMemo(() => {
    const s = new Set<string>();
    for (const r of filteredRows) {
      if (r.category !== listTabCat) continue;
      const sub = r.subject?.trim() ? r.subject : 'Diğer';
      s.add(sub);
    }
    return sortSubjectsForCategory(listTabCat, [...s]);
  }, [filteredRows, listTabCat]);

  useEffect(() => {
    if (listSubjectOptions.length === 0) {
      setListTabSubject('');
      return;
    }
    if (!listSubjectOptions.includes(listTabSubject)) {
      setListTabSubject(listSubjectOptions[0] ?? '');
    }
  }, [listSubjectOptions, listTabSubject]);

  const visibleRows = useMemo(() => {
    return filteredRows.filter((r) => {
      if (r.category !== listTabCat) return false;
      if (!listTabSubject) return true;
      const sub = r.subject?.trim() ? r.subject : 'Diğer';
      return sub === listTabSubject;
    });
  }, [filteredRows, listTabCat, listTabSubject]);

  const motivationCompact = scrollY > 28 || introCompactPersisted;

  const sortedFilteredAdd = useMemo(() => {
    return [...filteredAdd].sort((a, b) => {
      const sa = a.subject?.trim() || 'Diğer';
      const sb = b.subject?.trim() || 'Diğer';
      const ga = detectSubGroup(sa, a.name);
      const gb = detectSubGroup(sb, b.name);
      const c = ga.localeCompare(gb, 'tr');
      if (c !== 0) return c;
      return a.name.localeCompare(b.name, 'tr');
    });
  }, [filteredAdd]);

  const addListItems = useMemo<AddListItem[]>(() => {
    const result: AddListItem[] = [];
    let current = '';
    for (const t of sortedFilteredAdd) {
      const subject = t.subject?.trim() || 'Diğer';
      const grp = detectSubGroup(subject, t.name);
      if (grp !== current) {
        current = grp;
        result.push({ kind: 'header', key: `h-${grp}`, title: grp });
      }
      result.push({ kind: 'topic', key: `t-${t.id}`, topic: t });
    }
    return result;
  }, [sortedFilteredAdd]);

  const adminSubjectOptions = useMemo(() => {
    const s = new Set<string>();
    for (const t of catalog) {
      if (t.category !== adminCategory) continue;
      const sub = t.subject?.trim() ? t.subject : 'Diğer';
      if (isLikelyTestSubjectName(sub)) continue;
      s.add(sub);
    }
    return sortSubjectsForCategory(adminCategory, [...s]);
  }, [catalog, adminCategory]);

  useEffect(() => {
    if (!addOpen) return;
    if (subjectOptions.length === 0) return;
    if (!subjectOptions.includes(addTabSubject)) {
      setAddTabSubject(subjectOptions[0] ?? '');
    }
  }, [addOpen, addTabCat, subjectOptions, addTabSubject]);

  useEffect(() => {
    if (!adminOpen) return;
    if (adminSubjectOptions.length === 0) return;
    if (!adminSubjectOptions.includes(adminSubject)) {
      setAdminSubject(adminSubjectOptions[0] ?? '');
    }
  }, [adminOpen, adminSubjectOptions, adminSubject]);

  async function addTopic(topicId: number) {
    setActionError(null);
    try {
      await apiPost('/user/topics/add', { topicId });
      setAddOpen(false);
      setLoading(true);
      await load();
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Eklenemedi.');
    }
  }

  async function updateStatus(topicId: number, status: TopicStatusValue) {
    setActionError(null);
    try {
      await apiPost('/user/topics/update', { topicId, status });
      setStatusModal(null);
      if (status === TopicStatus.Completed) {
        setCelebrateMsg('Harika — bir konu daha tamamlandı! Küçük bir zafer.');
        if (celebrateTimerRef.current) clearTimeout(celebrateTimerRef.current);
        celebrateTimerRef.current = setTimeout(() => {
          setCelebrateMsg(null);
          celebrateTimerRef.current = null;
        }, 4500);
      }
      setLoading(true);
      await load();
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Güncellenemedi.');
    }
  }

  async function addCatalogTopic() {
    if (!adminName.trim()) {
      setActionError('Konu adı gerekli.');
      return;
    }
    if (!isAdmin) {
      setActionError('Admin yetkisi gerekli.');
      return;
    }
    const subjectToSend = adminCustomSubject.trim() || adminSubject.trim();
    if (!subjectToSend) {
      setActionError('Lütfen bir ders seçin.');
      return;
    }
    setAdminSubmitting(true);
    setActionError(null);
    try {
      await apiPost('/topics', {
        name: adminName.trim(),
        category: adminCategory,
        subject: subjectToSend,
      });
      setAdminOpen(false);
      setAdminName('');
      setAdminSubject('');
      setAdminCustomSubject('');
      setAdminCategory('TYT');
      setLoading(true);
      await load();
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Konu eklenemedi.');
    } finally {
      setAdminSubmitting(false);
    }
  }

  function removeFromList(topicId: number, name: string) {
    Alert.alert('Listeden çıkar', `"${name}" konusunu takip listenizden kaldırmak istiyor musunuz?`, [
      { text: 'Vazgeç', style: 'cancel' },
      {
        text: 'Kaldır',
        style: 'destructive',
        onPress: async () => {
          setActionError(null);
          try {
            await apiDelete(`/user/topics/${topicId}`);
            setStatusModal(null);
            setLoading(true);
            await load();
          } catch (e) {
            setActionError(e instanceof Error ? e.message : 'Kaldırılamadı.');
          }
        },
      },
    ]);
  }

  const renderSwipeDelete = useCallback(
    (item: Row) => (
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Listeden çıkar"
        style={styles.swipeDelete}
        onPress={() => removeFromList(item.topicId, item.name)}
      >
        <Ionicons name="trash-outline" size={24} color={colors.onPrimary} />
      </Pressable>
    ),
    [colors.onPrimary, removeFromList, styles.swipeDelete]
  );

  const listHeader = useMemo(
    () => (
      <>
        {celebrateMsg ? (
          <View style={[styles.celebrateBanner, { marginBottom: rvs(8, vScale) }]}>
            <Text style={styles.celebrateText}>{celebrateMsg}</Text>
          </View>
        ) : null}
        <View
          style={{
            marginBottom: motivationCompact ? rvs(4, vScale) : rvs(6, vScale),
          }}
        >
          <Text style={[styles.motivationTitle, motivationCompact && { fontSize: 15 }]}>Küçük zaferler</Text>
          {!motivationCompact ? (
            <Text style={styles.motivationSub}>
              Büyük müfredatı parçala: her konu ayrı bir adım. Bitirdiklerini tamamlandı işaretle — beyne küçük ödüller.
            </Text>
          ) : null}
        </View>
        <Pressable
          style={[
            styles.addBtn,
            {
              marginTop: 0,
              marginHorizontal: 0,
              marginBottom: rvs(12, vScale),
              paddingVertical: rvs(12, vScale),
              borderRadius: rs(10, scale),
            },
          ]}
          onPress={() => {
            setActionError(null);
            setAddTabCat('TYT');
            setAddTabSubject('');
            setAddOpen(true);
          }}
        >
          <Text style={styles.addBtnText}>+ Listeme konu ekle</Text>
        </Pressable>
        <View style={styles.stickySection}>
          <TextInput
            style={[
              styles.searchInput,
              {
                marginBottom: rvs(10, vScale),
              },
            ]}
            placeholder="Konu ara..."
            placeholderTextColor={colors.textMuted}
            keyboardAppearance={colors.keyboardAppearance}
            value={searchText}
            onChangeText={setSearchText}
          />
          <View style={[styles.segRow, { marginBottom: rvs(8, vScale) }]}>
            <Pressable style={[styles.segBtn, listTabCat === 'TYT' && styles.segBtnOn]} onPress={() => setListTabCat('TYT')}>
              <Text style={[styles.segText, listTabCat === 'TYT' && styles.segTextOn]}>TYT</Text>
            </Pressable>
            <Pressable style={[styles.segBtn, listTabCat === 'AYT' && styles.segBtnOn]} onPress={() => setListTabCat('AYT')}>
              <Text style={[styles.segText, listTabCat === 'AYT' && styles.segTextOn]}>AYT</Text>
            </Pressable>
          </View>
          <Text style={[styles.curriculumSectionTitle, { marginBottom: rvs(6, vScale) }]}>
            {CURRICULUM_SECTION_TITLE[listTabCat]}
          </Text>
          <ScrollView
            key={`list-subject-${listTabCat}`}
            horizontal
            showsHorizontalScrollIndicator={false}
            nestedScrollEnabled
            style={[styles.subjectFilterScroll, { marginBottom: rvs(8, vScale) }]}
            contentContainerStyle={styles.subjectFilterScrollContent}
          >
            {listSubjectOptions.map((sub) => (
              <Pressable
                key={sub}
                style={[styles.chip, listTabSubject === sub && styles.chipOn]}
                onPress={() => setListTabSubject(sub)}
              >
                <Text style={[styles.chipText, listTabSubject === sub && styles.chipTextOn]}>{sub}</Text>
              </Pressable>
            ))}
          </ScrollView>
          <Text style={[styles.listMetaText, { marginBottom: rvs(4, vScale) }]}>{visibleRows.length} konu</Text>
          {isAdmin ? (
            <Pressable
              style={[
                styles.adminBtn,
                {
                  marginTop: rvs(6, vScale),
                  paddingVertical: rvs(12, vScale),
                },
              ]}
              onPress={() => {
                setActionError(null);
                setAdminOpen(true);
              }}
            >
              <Text style={styles.adminBtnText}>Admin: Kataloğa konu ekle</Text>
            </Pressable>
          ) : null}
        </View>
      </>
    ),
    [
      celebrateMsg,
      motivationCompact,
      styles,
      rvs,
      vScale,
      scale,
      colors.textMuted,
      colors.keyboardAppearance,
      searchText,
      listTabCat,
      listTabSubject,
      listSubjectOptions,
      visibleRows.length,
      isAdmin,
    ]
  );

  if (loading && rows.length === 0 && !error) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <GestureHandlerRootView style={styles.container}>
      {error ? <Text style={styles.bannerError}>{error}</Text> : null}
      {actionError ? <Text style={styles.bannerError}>{actionError}</Text> : null}
      <FlatList
        style={styles.list}
        data={visibleRows}
        keyExtractor={(item) => String(item.topicId)}
        onScroll={(e) => {
          const y = e.nativeEvent.contentOffset.y;
          setScrollY(y);
          if (y > 28 && !introPersistGateRef.current) {
            introPersistGateRef.current = true;
            setIntroCompactPersisted(true);
            void AsyncStorage.setItem(TOPICS_INTRO_COMPACT_KEY, '1');
          }
        }}
        scrollEventThrottle={16}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        ListHeaderComponent={listHeader}
        ListFooterComponent={
          !loading && visibleRows.length === 0 ? (
            <Text style={styles.empty}>Henüz konu eklemedin. Yukarıdaki butonla ekleyebilirsin.</Text>
          ) : null
        }
        renderItem={({ item: row }) => {
            const done = row.status === TopicStatus.Completed;
            return (
              <Swipeable
                friction={2}
                overshootRight={false}
                renderRightActions={() => renderSwipeDelete(row)}
              >
                <View style={[styles.card, done && styles.cardDone]}>
                  <Pressable
                    style={styles.cardMain}
                    onPress={() => {
                      setActionError(null);
                      setStatusModal(row);
                    }}
                  >
                    <View style={styles.cardTitleRow}>
                      {done ? (
                        <Ionicons
                          name="checkmark-circle"
                          size={22}
                          color={colors.statusDone}
                          style={styles.cardCheck}
                        />
                      ) : null}
                      <Text style={[styles.cardTitle, { flex: 1 }]}>{row.name}</Text>
                    </View>
                    <Text style={styles.cardCat}>
                      {row.category}
                      {row.subject && row.subject !== '—' ? ` · ${row.subject}` : ''}
                    </Text>
                    <Text style={[styles.cardStatus, { color: statusColorByValue[row.status] ?? colors.primary }]}>
                      {topicStatusLabel[row.status] ?? '—'}
                    </Text>
                    <Text style={styles.cardHint}>Durum için dokun · Sola kaydır: listeden çıkar</Text>
                  </Pressable>
                </View>
              </Swipeable>
            );
          }}
          contentContainerStyle={[
            styles.listContent,
            {
              paddingHorizontal: rs(16, scale),
              paddingBottom: rvs(32, vScale) + insets.bottom,
            },
          ]}
        />

      <Modal visible={!!statusModal} transparent animationType="fade">
        <Pressable style={styles.modalOverlay} onPress={() => setStatusModal(null)}>
          <Pressable style={styles.modalBox} onPress={(e) => e.stopPropagation()}>
            <Text style={styles.modalTitle}>Durum</Text>
            <Text style={styles.modalSub}>{statusModal?.name}</Text>
            {topicStatusOptions.map((opt) => (
              <Pressable
                key={opt.value}
                style={styles.modalRow}
                onPress={() => statusModal && updateStatus(statusModal.topicId, opt.value)}
              >
                <Text style={styles.modalRowText}>{opt.label}</Text>
              </Pressable>
            ))}
            <Pressable
              style={styles.modalRowDanger}
              onPress={() => statusModal && removeFromList(statusModal.topicId, statusModal.name)}
            >
              <Text style={styles.modalRowDangerText}>Listeden çıkar</Text>
            </Pressable>
            <Pressable style={styles.modalCancel} onPress={() => setStatusModal(null)}>
              <Text style={styles.modalCancelText}>Vazgeç</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>

      <Modal visible={addOpen} transparent animationType="slide">
        <Pressable style={styles.modalOverlay} onPress={() => setAddOpen(false)}>
          <Pressable
            style={[styles.modalBoxTall, { maxHeight: modalMaxHeight }]}
            onPress={(e) => e.stopPropagation()}
          >
            <Text style={styles.modalTitle}>Konu seç — YKS</Text>
            <Text style={styles.modalHint}>
              Sınav ve branşa göre filtrele. Listene her eklediğin satır küçük bir adım — sonra tek tek tamamlayacaksın.
            </Text>

            {availableToAdd.length === 0 ? (
              <Text style={styles.empty}>Eklenecek konu kalmadı veya katalog boş.</Text>
            ) : (
              <>
                <View style={styles.segRow}>
                  <Pressable
                    style={[styles.segBtn, addTabCat === 'TYT' && styles.segBtnOn]}
                    onPress={() => setAddTabCat('TYT')}
                  >
                    <Text style={[styles.segText, addTabCat === 'TYT' && styles.segTextOn]}>TYT</Text>
                  </Pressable>
                  <Pressable
                    style={[styles.segBtn, addTabCat === 'AYT' && styles.segBtnOn]}
                    onPress={() => setAddTabCat('AYT')}
                  >
                    <Text style={[styles.segText, addTabCat === 'AYT' && styles.segTextOn]}>AYT</Text>
                  </Pressable>
                </View>

                <Text style={styles.curriculumSectionTitle}>{CURRICULUM_SECTION_TITLE[addTabCat]}</Text>
                <Text style={styles.branchesLabel}>Branş</Text>
                <ScrollView
                  key={`add-branch-${addTabCat}`}
                  horizontal
                  showsHorizontalScrollIndicator={false}
                  style={styles.chipScroll}
                  contentContainerStyle={styles.chipScrollContent}
                >
                  {subjectOptions.map((sub) => (
                    <Pressable
                      key={sub}
                      style={[styles.chip, addTabSubject === sub && styles.chipOn]}
                      onPress={() => setAddTabSubject(sub)}
                    >
                      <Text style={[styles.chipText, addTabSubject === sub && styles.chipTextOn]}>{sub}</Text>
                    </Pressable>
                  ))}
                </ScrollView>
                <TextInput
                  style={styles.searchInput}
                  placeholder="Konu ara..."
                  placeholderTextColor={colors.textMuted}
                  value={addSearchText}
                  onChangeText={setAddSearchText}
                />
                <Text style={styles.listMetaText}>
                  {filteredAdd.length} konu bulundu
                </Text>
                <FlatList
                  data={addListItems}
                  keyExtractor={(it) => it.key}
                  style={[styles.addList, { maxHeight: addListMaxHeight }]}
                  ListEmptyComponent={
                    <Text style={styles.emptySmall}>Bu branşta eklenecek konu kalmadı.</Text>
                  }
                  renderItem={({ item }) =>
                    item.kind === 'header' ? (
                      <Text style={styles.groupHeader}>{item.title}</Text>
                    ) : (
                      <Pressable style={styles.addRow} onPress={() => addTopic(item.topic.id)}>
                        <Text style={styles.addRowTitle}>{item.topic.name}</Text>
                        <Text style={styles.addRowCat}>
                          {item.topic.category} · {item.topic.subject?.trim() || 'Diğer'}
                        </Text>
                      </Pressable>
                    )
                  }
                />
              </>
            )}
            <Pressable style={styles.modalCancel} onPress={() => setAddOpen(false)}>
              <Text style={styles.modalCancelText}>Kapat</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>

      <Modal visible={adminOpen} transparent animationType="fade">
        <Pressable style={styles.modalOverlay} onPress={() => setAdminOpen(false)}>
          <Pressable
            style={[styles.modalBoxTall, { maxHeight: modalMaxHeight }]}
            onPress={(e) => e.stopPropagation()}
          >
            <Text style={styles.modalTitle}>Admin: Konu ekle</Text>
            <Text style={styles.modalHint}>Katalog globaline yeni konu eklersin.</Text>

            <TextInput
              style={styles.adminInput}
              placeholder="Konu adı"
              placeholderTextColor={colors.textMuted}
              value={adminName}
              onChangeText={setAdminName}
            />

            <View style={styles.segRow}>
              <Pressable
                style={[styles.segBtn, adminCategory === 'TYT' && styles.segBtnOn]}
                onPress={() => {
                  setAdminCategory('TYT');
                  setAdminSubject('');
                      setAdminCustomSubject('');
                }}
              >
                <Text style={[styles.segText, adminCategory === 'TYT' && styles.segTextOn]}>TYT</Text>
              </Pressable>
              <Pressable
                style={[styles.segBtn, adminCategory === 'AYT' && styles.segBtnOn]}
                onPress={() => {
                  setAdminCategory('AYT');
                  setAdminSubject('');
                      setAdminCustomSubject('');
                }}
              >
                <Text style={[styles.segText, adminCategory === 'AYT' && styles.segTextOn]}>AYT</Text>
              </Pressable>
            </View>

                <Text style={styles.curriculumSectionTitle}>{CURRICULUM_SECTION_TITLE[adminCategory]}</Text>
                <Text style={styles.branchesLabel}>Ders</Text>
                <ScrollView
                  key={`admin-subj-${adminCategory}`}
                  horizontal
                  showsHorizontalScrollIndicator={false}
                  style={styles.chipScroll}
                  contentContainerStyle={styles.chipScrollContent}
                >
                  {adminSubjectOptions.map((sub) => (
                    <Pressable
                      key={sub}
                      style={[styles.chip, adminSubject === sub && styles.chipOn]}
                      onPress={() => setAdminSubject(sub)}
                    >
                      <Text style={[styles.chipText, adminSubject === sub && styles.chipTextOn]}>{sub}</Text>
                    </Pressable>
                  ))}
                </ScrollView>

                <TextInput
                  style={styles.adminInput}
                  placeholder="veya yeni ders yaz (örn. Türk Dili ve Edebiyatı)"
                  placeholderTextColor={colors.textMuted}
                  value={adminCustomSubject}
                  onChangeText={setAdminCustomSubject}
                  autoCapitalize="words"
                />

            <Pressable
              style={[styles.adminSubmitBtn, adminSubmitting && styles.submitDisabled]}
              onPress={() => void addCatalogTopic()}
              disabled={adminSubmitting}
            >
              <Text style={styles.adminSubmitText}>{adminSubmitting ? 'Ekleniyor…' : 'Kataloğa ekle'}</Text>
            </Pressable>

            <Pressable style={styles.modalCancel} onPress={() => setAdminOpen(false)}>
              <Text style={styles.modalCancelText}>Kapat</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>
    </GestureHandlerRootView>
  );
}
