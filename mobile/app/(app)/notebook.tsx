import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Animated,
  FlatList,
  Image,
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
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { apiDelete, apiGet, apiPost, apiPut } from '../../src/lib/api';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import type { ProblemNoteDto, ProblemNoteListResponse } from '../../src/types/api';
import { useTheme } from '../../src/theme';

const PRESET_TAGS = [
  'Matematik',
  'Geometri',
  'Türkçe',
  'Fizik',
  'Kimya',
  'Biyoloji',
  'Tarih',
  'Coğrafya',
  'Felsefe',
  'Türev',
  'İntegral',
  'Zor Soru',
  'Tekrar Bak',
  'Yönerge',
  'Grafik',
];

function toImageUri(raw: string): string {
  const t = raw.trim();
  if (t.startsWith('http://') || t.startsWith('https://')) return t;
  if (t.startsWith('data:')) return t;
  return `data:image/jpeg;base64,${t}`;
}

function formatShortDate(iso: string) {
  try {
    return new Date(iso).toLocaleString('tr-TR', {
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

export default function NotebookScreen() {
  const { colors } = useTheme();
  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);
  const insets = useSafeAreaInsets();

  const [items, setItems] = useState<ProblemNoteDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  const [addOpen, setAddOpen] = useState(false);
  const [pendingUri, setPendingUri] = useState<string | null>(null);
  const [addTags, setAddTags] = useState<string[]>([]);
  const [addLearned, setAddLearned] = useState(false);
  const [addCustomTag, setAddCustomTag] = useState('');
  const [savingAdd, setSavingAdd] = useState(false);

  const [detail, setDetail] = useState<ProblemNoteDto | null>(null);
  const [detailTags, setDetailTags] = useState<string[]>([]);
  const [detailLearned, setDetailLearned] = useState(false);
  const [detailCustom, setDetailCustom] = useState('');
  const [savingDetail, setSavingDetail] = useState(false);

  const [lightboxUri, setLightboxUri] = useState<string | null>(null);
  const [hideLearned, setHideLearned] = useState(false);
  const lightboxScale = useRef(new Animated.Value(1)).current;
  const lightboxZoomedRef = useRef(false);
  const lastImageTapRef = useRef(0);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        centered: { flex: 1, justifyContent: 'center', alignItems: 'center' },
        bannerError: { color: colors.errorText, padding: rs(12, scale), backgroundColor: colors.errorBg },
        intro: {
          marginHorizontal: rs(16, scale),
          marginBottom: rs(12, scale),
          padding: rs(14, scale),
          backgroundColor: colors.surface,
          borderRadius: rs(12, scale),
          borderWidth: 1,
          borderColor: colors.border,
        },
        introTitle: { fontSize: rs(16, scale), fontWeight: '700', color: colors.text },
        introBody: { fontSize: rs(14, scale), color: colors.textMuted, marginTop: rs(6, scale), lineHeight: rs(20, scale) },
        actions: { flexDirection: 'row', gap: rs(10, scale), marginHorizontal: rs(16, scale), marginBottom: rs(12, scale) },
        actionBtn: {
          flex: 1,
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'center',
          gap: rs(8, scale),
          backgroundColor: colors.primary,
          paddingVertical: rvs(14, vScale),
          borderRadius: rs(12, scale),
        },
        actionBtnSec: { backgroundColor: colors.sectionAccent },
        actionText: { color: colors.onPrimary, fontWeight: '700', fontSize: rs(15, scale) },
        list: { paddingHorizontal: rs(16, scale) },
        card: {
          flexDirection: 'row',
          alignItems: 'flex-start',
          backgroundColor: colors.surface,
          borderRadius: rs(12, scale),
          borderWidth: 1,
          borderColor: colors.border,
          marginBottom: rs(10, scale),
          overflow: 'hidden',
        },
        cardStripeLearned: { borderLeftWidth: rs(4, scale), borderLeftColor: colors.statusDone },
        cardStripeTodo: { borderLeftWidth: rs(4, scale), borderLeftColor: colors.errorText },
        cardBody: { flex: 1, padding: rs(10, scale) },
        tagRow: { flexDirection: 'row', flexWrap: 'wrap', gap: rs(6, scale), marginBottom: rs(8, scale) },
        tag: {
          paddingHorizontal: rs(8, scale),
          paddingVertical: rs(4, scale),
          borderRadius: rs(8, scale),
          backgroundColor: colors.chip,
          borderWidth: 1,
          borderColor: colors.border,
        },
        tagText: { fontSize: rs(11, scale), color: colors.chipText, fontWeight: '600' },
        meta: { fontSize: rs(12, scale), color: colors.textMuted, marginBottom: rs(6, scale) },
        empty: { textAlign: 'center', color: colors.textMuted, marginTop: rs(24, scale), paddingHorizontal: rs(24, scale), lineHeight: rs(22, scale) },
        modalBackdrop: { flex: 1, backgroundColor: colors.overlay, justifyContent: 'flex-end' },
        sheet: {
          backgroundColor: colors.surface,
          borderTopLeftRadius: rs(16, scale),
          borderTopRightRadius: rs(16, scale),
          maxHeight: '92%',
          borderWidth: 1,
          borderColor: colors.border,
        },
        sheetHeader: {
          flexDirection: 'row',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: rs(16, scale),
          borderBottomWidth: StyleSheet.hairlineWidth,
          borderBottomColor: colors.border,
        },
        sheetTitle: { fontSize: rs(17, scale), fontWeight: '700', color: colors.text },
        sheetBody: { padding: rs(16, scale) },
        previewImg: { width: '100%', height: rs(220, scale), borderRadius: rs(12, scale), backgroundColor: colors.surfaceMuted },
        label: { fontSize: rs(13, scale), color: colors.textMuted, marginTop: rs(12, scale), marginBottom: rs(8, scale) },
        chipWrap: { flexDirection: 'row', flexWrap: 'wrap', gap: rs(8, scale) },
        chip: {
          paddingVertical: rs(8, scale),
          paddingHorizontal: rs(12, scale),
          borderRadius: rs(20, scale),
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.chip,
        },
        chipOn: { backgroundColor: colors.segmentOn, borderColor: colors.segmentOn },
        chipTxt: { fontSize: rs(13, scale), fontWeight: '600', color: colors.chipText },
        chipTxtOn: { color: colors.segmentTextOn },
        customRow: { flexDirection: 'row', gap: rs(8, scale), marginTop: rs(10, scale) },
        customInput: {
          flex: 1,
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingHorizontal: rs(12, scale),
          paddingVertical: rvs(10, vScale),
          fontSize: rs(15, scale),
          color: colors.text,
          backgroundColor: colors.inputBg,
        },
        addTagBtn: {
          justifyContent: 'center',
          paddingHorizontal: rs(14, scale),
          backgroundColor: colors.primaryMuted,
          borderRadius: rs(10, scale),
        },
        addTagBtnText: { color: colors.onPrimary, fontWeight: '700' },
        primaryBtn: {
          marginTop: rs(16, scale),
          backgroundColor: colors.statusProgress,
          paddingVertical: rvs(14, vScale),
          borderRadius: rs(12, scale),
          alignItems: 'center',
        },
        primaryBtnText: { color: colors.onPrimary, fontWeight: '700', fontSize: rs(16, scale) },
        dangerBtn: {
          marginTop: rs(10, scale),
          paddingVertical: rvs(12, vScale),
          alignItems: 'center',
        },
        dangerText: { color: colors.errorText, fontWeight: '600', fontSize: rs(15, scale) },
        detailFullImg: { width: width - rs(32, scale), height: rs(280, scale), borderRadius: rs(12, scale), backgroundColor: colors.surfaceMuted, alignSelf: 'center' },
        statsBanner: {
          marginHorizontal: rs(16, scale),
          marginBottom: rs(10, scale),
          paddingVertical: rs(12, scale),
          paddingHorizontal: rs(14, scale),
          backgroundColor: colors.celebrateBg,
          borderRadius: rs(12, scale),
          borderWidth: 1,
          borderColor: colors.celebrateBorder,
        },
        counterText: { fontSize: rs(15, scale), fontWeight: '700', color: colors.celebrateText, lineHeight: rs(21, scale) },
        filterRow: {
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginHorizontal: rs(16, scale),
          marginBottom: rs(12, scale),
          paddingVertical: rs(10, scale),
          paddingHorizontal: rs(12, scale),
          backgroundColor: colors.surface,
          borderRadius: rs(12, scale),
          borderWidth: 1,
          borderColor: colors.border,
        },
        filterLabel: { fontSize: rs(14, scale), fontWeight: '600', color: colors.text, flex: 1 },
        thumbWrap: {
          position: 'relative' as const,
          width: rs(104, scale),
          height: rs(104, scale),
          backgroundColor: colors.surfaceMuted,
        },
        thumb: { width: '100%', height: '100%' },
        thumbBadges: {
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 0,
          flexDirection: 'row',
          flexWrap: 'wrap',
          gap: rs(4, scale),
          padding: rs(6, scale),
          paddingTop: rs(16, scale),
        },
        thumbBadge: {
          paddingHorizontal: rs(6, scale),
          paddingVertical: rs(3, scale),
          borderRadius: rs(6, scale),
          backgroundColor: 'rgba(0,0,0,0.45)',
          maxWidth: '100%',
        },
        thumbBadgeText: { fontSize: rs(10, scale), fontWeight: '700', color: '#fff' },
        learnBtn: {
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'center',
          gap: rs(8, scale),
          marginTop: rs(6, scale),
          paddingVertical: rs(10, scale),
          paddingHorizontal: rs(12, scale),
          borderRadius: rs(10, scale),
          borderWidth: 2,
        },
        learnBtnOff: { borderColor: colors.border, backgroundColor: colors.surfaceMuted },
        learnBtnOn: { borderColor: colors.statusDone, backgroundColor: 'rgba(34, 197, 94, 0.15)' },
        learnBtnText: { fontSize: rs(13, scale), fontWeight: '700' },
        learnBtnTextOff: { color: colors.textSecondary },
        learnBtnTextOn: { color: colors.statusDone },
        lightboxRoot: { flex: 1, backgroundColor: 'rgba(0,0,0,0.94)' },
        lightboxBackdrop: { ...StyleSheet.absoluteFillObject, zIndex: 0 },
        lightboxCenter: {
          flex: 1,
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1,
          pointerEvents: 'box-none' as const,
        },
        lightboxImg: { width: width * 0.94, maxHeight: height * 0.82 },
        lightboxClose: {
          position: 'absolute',
          top: rvs(48, vScale),
          right: rs(16, scale),
          zIndex: 2,
          padding: rs(10, scale),
          borderRadius: rs(20, scale),
          backgroundColor: 'rgba(255,255,255,0.12)',
        },
        lightboxHint: {
          position: 'absolute',
          bottom: rvs(36, vScale),
          left: rs(16, scale),
          right: rs(16, scale),
          zIndex: 2,
          textAlign: 'center',
          color: 'rgba(255,255,255,0.65)',
          fontSize: rs(13, scale),
        },
      }),
    [colors, scale, vScale, width, height]
  );

  const listPad = useMemo(
    () => [styles.list, { paddingBottom: rvs(32, vScale) + insets.bottom }],
    [styles.list, vScale, insets.bottom]
  );

  const load = useCallback(async () => {
    setError(null);
    const res = await apiGet<ProblemNoteListResponse>('/problem-notes/list');
    setItems(res.items);
  }, []);

  useEffect(() => {
    let c = false;
    (async () => {
      setLoading(true);
      try {
        await load();
      } catch (e) {
        if (!c) setError(e instanceof Error ? e.message : 'Liste yüklenemedi.');
      } finally {
        if (!c) setLoading(false);
      }
    })();
    return () => {
      c = true;
    };
  }, [load]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await load();
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Liste yüklenemedi.');
    } finally {
      setRefreshing(false);
    }
  }, [load]);

  const unresolvedCount = useMemo(() => items.filter((i) => !i.solutionLearned).length, [items]);
  const displayItems = useMemo(() => {
    if (!hideLearned) return items;
    return items.filter((i) => !i.solutionLearned);
  }, [items, hideLearned]);

  const closeLightbox = useCallback(() => {
    lightboxZoomedRef.current = false;
    lightboxScale.setValue(1);
    setLightboxUri(null);
  }, [lightboxScale]);

  const openLightbox = useCallback(
    (uri: string) => {
      lightboxZoomedRef.current = false;
      lightboxScale.setValue(1);
      setLightboxUri(uri);
    },
    [lightboxScale]
  );

  const toggleLightboxZoom = useCallback(() => {
    const next = !lightboxZoomedRef.current;
    lightboxZoomedRef.current = next;
    Animated.spring(lightboxScale, {
      toValue: next ? 2.15 : 1,
      useNativeDriver: true,
      friction: 7,
      tension: 48,
    }).start();
  }, [lightboxScale]);

  const onLightboxImagePress = useCallback(() => {
    const now = Date.now();
    if (now - lastImageTapRef.current < 320) {
      lastImageTapRef.current = 0;
      toggleLightboxZoom();
    } else {
      lastImageTapRef.current = now;
    }
  }, [toggleLightboxZoom]);

  async function pickFromCamera() {
    const perm = await ImagePicker.requestCameraPermissionsAsync();
    if (!perm.granted) {
      Alert.alert('İzin gerekli', 'Soru fotoğrafı çekmek için kamera izni vermelisin.');
      return;
    }
    const r = await ImagePicker.launchCameraAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      quality: 0.55,
      base64: true,
    });
    if (r.canceled || !r.assets?.[0]) return;
    const a = r.assets[0];
    const uri = a.base64 ? `data:image/jpeg;base64,${a.base64}` : a.uri;
    if (!uri || (!a.base64 && !uri.startsWith('data:'))) {
      Alert.alert('Uyarı', 'Görüntü alınamadı. Tekrar dene veya galeriden seç.');
      return;
    }
    setPendingUri(uri);
    setAddTags([]);
    setAddLearned(false);
    setAddCustomTag('');
    setAddOpen(true);
  }

  async function pickFromLibrary() {
    const perm = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!perm.granted) {
      Alert.alert('İzin gerekli', 'Galeriden seçmek için fotoğraf izni gerekir.');
      return;
    }
    const r = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      quality: 0.55,
      base64: true,
    });
    if (r.canceled || !r.assets?.[0]) return;
    const a = r.assets[0];
    const uri = a.base64 ? `data:image/jpeg;base64,${a.base64}` : a.uri;
    if (!uri || (!a.base64 && !uri.startsWith('data:'))) {
      Alert.alert('Uyarı', 'Görüntü alınamadı.');
      return;
    }
    setPendingUri(uri);
    setAddTags([]);
    setAddLearned(false);
    setAddCustomTag('');
    setAddOpen(true);
  }

  function togglePresetAdd(tag: string) {
    setAddTags((prev) => (prev.includes(tag) ? prev.filter((x) => x !== tag) : [...prev, tag]));
  }

  function pushCustomAdd() {
    const t = addCustomTag.trim();
    if (!t || addTags.includes(t)) return;
    setAddTags((prev) => [...prev, t]);
    setAddCustomTag('');
  }

  async function submitAdd() {
    if (!pendingUri) return;
    setSavingAdd(true);
    try {
      await apiPost<ProblemNoteDto>('/problem-notes/add', {
        imageBase64: pendingUri,
        tags: addTags,
        solutionLearned: addLearned,
      });
      setAddOpen(false);
      setPendingUri(null);
      await load();
    } catch (e) {
      Alert.alert('Kayıt', e instanceof Error ? e.message : 'Kaydedilemedi.');
    } finally {
      setSavingAdd(false);
    }
  }

  async function patchLearned(item: ProblemNoteDto, learned: boolean, tagsOverride?: string[]) {
    const tags = tagsOverride ?? item.tags;
    setBusyId(item.id);
    try {
      await apiPut(`/problem-notes/${item.id}`, {
        tags,
        solutionLearned: learned,
      });
      setItems((prev) =>
        prev
          .map((x) => (x.id === item.id ? { ...x, solutionLearned: learned, tags: [...tags] } : x))
          .sort((a, b) => {
            const c = Number(a.solutionLearned) - Number(b.solutionLearned);
            if (c !== 0) return c;
            return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
          })
      );
      if (detail?.id === item.id) {
        setDetailLearned(learned);
        setDetail((d) => (d ? { ...d, solutionLearned: learned, tags } : null));
      }
    } catch (e) {
      Alert.alert('Güncelleme', e instanceof Error ? e.message : 'Başarısız.');
    } finally {
      setBusyId(null);
    }
  }

  function openDetail(item: ProblemNoteDto) {
    setDetail(item);
    setDetailTags([...item.tags]);
    setDetailLearned(item.solutionLearned);
    setDetailCustom('');
  }

  function togglePresetDetail(tag: string) {
    setDetailTags((prev) => (prev.includes(tag) ? prev.filter((x) => x !== tag) : [...prev, tag]));
  }

  function pushCustomDetail() {
    const t = detailCustom.trim();
    if (!t || detailTags.includes(t)) return;
    setDetailTags((prev) => [...prev, t]);
    setDetailCustom('');
  }

  async function saveDetail() {
    if (!detail) return;
    setSavingDetail(true);
    try {
      await apiPut(`/problem-notes/${detail.id}`, {
        tags: detailTags,
        solutionLearned: detailLearned,
      });
      setDetail(null);
      await load();
    } catch (e) {
      Alert.alert('Kayıt', e instanceof Error ? e.message : 'Başarısız.');
    } finally {
      setSavingDetail(false);
    }
  }

  function confirmDelete(item: ProblemNoteDto) {
    Alert.alert('Silinsin mi?', 'Bu soru notu kalıcı olarak silinir.', [
      { text: 'İptal', style: 'cancel' },
      {
        text: 'Sil',
        style: 'destructive',
        onPress: () => void deleteNote(item.id),
      },
    ]);
  }

  async function deleteNote(id: number) {
    try {
      await apiDelete(`/problem-notes/${id}`);
      setDetail(null);
      await load();
    } catch (e) {
      Alert.alert('Sil', e instanceof Error ? e.message : 'Başarısız.');
    }
  }

  if (loading && items.length === 0) {
    return (
      <View style={[styles.centered, { backgroundColor: colors.bg }]}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
      {error ? <Text style={styles.bannerError}>{error}</Text> : null}

      <FlatList
        data={displayItems}
        keyExtractor={(x) => String(x.id)}
        contentContainerStyle={listPad}
        ListHeaderComponent={
          <>
            <View style={styles.intro}>
              <Text style={styles.introTitle}>Çözemediğim sorular</Text>
              <Text style={styles.introBody}>
                Kazanım, çözdüğün kadar değil öğrendiğin kadar. Fotoğraf çek, etiketle; çözümü oturttuğunda işaretle —
                liste yeşile dönsün.
              </Text>
            </View>
            {items.length > 0 ? (
              <View style={styles.statsBanner}>
                <Text style={styles.counterText}>
                  {unresolvedCount === 0
                    ? 'Kumbaranda çözülmemiş soru kalmadı — hepsini öğrendin!'
                    : `Kumbaranda ${unresolvedCount} çözülmemiş soru var. Birer birer erit.`}
                </Text>
              </View>
            ) : null}
            {items.length > 0 ? (
              <Pressable
                style={[styles.filterRow, hideLearned && { borderColor: colors.statusDone, backgroundColor: colors.celebrateBg }]}
                onPress={() => setHideLearned((h) => !h)}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: hideLearned }}
              >
                <Text style={styles.filterLabel}>Öğrendiklerimi gizle</Text>
                <Ionicons
                  name={hideLearned ? 'checkbox' : 'square-outline'}
                  size={24}
                  color={hideLearned ? colors.statusDone : colors.textMuted}
                />
              </Pressable>
            ) : null}
            <View style={styles.actions}>
              <Pressable style={styles.actionBtn} onPress={() => void pickFromCamera()}>
                <Ionicons name="camera" size={22} color={colors.onPrimary} />
                <Text style={styles.actionText}>Kamera</Text>
              </Pressable>
              <Pressable style={[styles.actionBtn, styles.actionBtnSec]} onPress={() => void pickFromLibrary()}>
                <Ionicons name="images-outline" size={22} color={colors.onPrimary} />
                <Text style={styles.actionText}>Galeri</Text>
              </Pressable>
            </View>
          </>
        }
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        ListEmptyComponent={
          items.length === 0 ? (
            <Text style={styles.empty}>
              Henüz not yok. Kamera veya galeri ile ilk sorunu ekle — yazmak zorunda değilsin.
            </Text>
          ) : displayItems.length === 0 ? (
            <Text style={styles.empty}>
              Öğrendiklerin gizli — gösterilecek çözülmemiş soru kalmadı. Filtreyi kapat veya yeni soru ekle.
            </Text>
          ) : null
        }
        renderItem={({ item }) => {
          const uri = toImageUri(item.imageUrl);
          const badgeTags = item.tags.slice(0, 2);
          return (
            <View style={[styles.card, item.solutionLearned ? styles.cardStripeLearned : styles.cardStripeTodo]}>
              <Pressable
                onPress={() => openLightbox(uri)}
                style={styles.thumbWrap}
                accessibilityRole="button"
                accessibilityLabel="Soruyu tam ekran aç"
              >
                <Image source={{ uri }} style={styles.thumb} resizeMode="cover" />
                {badgeTags.length > 0 ? (
                  <View style={styles.thumbBadges} pointerEvents="none">
                    {badgeTags.map((t) => (
                      <View key={t} style={styles.thumbBadge}>
                        <Text style={styles.thumbBadgeText} numberOfLines={1}>
                          {t}
                        </Text>
                      </View>
                    ))}
                    {item.tags.length > 2 ? (
                      <View style={styles.thumbBadge}>
                        <Text style={styles.thumbBadgeText}>+{item.tags.length - 2}</Text>
                      </View>
                    ) : null}
                  </View>
                ) : null}
              </Pressable>
              <View style={styles.cardBody}>
                <Pressable onPress={() => openDetail(item)}>
                  <Text style={styles.meta}>{formatShortDate(item.createdAt)}</Text>
                  <View style={styles.tagRow}>
                    {item.tags.length === 0 ? (
                      <Text style={[styles.tagText, { color: colors.textMuted }]}>Etiket yok — dokun ve ekle</Text>
                    ) : (
                      item.tags.slice(0, 6).map((t) => (
                        <View key={t} style={styles.tag}>
                          <Text style={styles.tagText}>{t}</Text>
                        </View>
                      ))
                    )}
                    {item.tags.length > 6 ? (
                      <Text style={styles.meta}>+{item.tags.length - 6}</Text>
                    ) : null}
                  </View>
                </Pressable>
                <Pressable
                  style={[styles.learnBtn, item.solutionLearned ? styles.learnBtnOn : styles.learnBtnOff]}
                  disabled={busyId === item.id}
                  onPress={() => void patchLearned(item, !item.solutionLearned)}
                  accessibilityRole="button"
                  accessibilityLabel={item.solutionLearned ? 'Öğrenildi olarak işaretli' : 'Çözümü öğrendim'}
                >
                  <Ionicons
                    name={item.solutionLearned ? 'checkmark-circle' : 'ellipse-outline'}
                    size={22}
                    color={item.solutionLearned ? colors.statusDone : colors.textMuted}
                  />
                  <Text
                    style={[
                      styles.learnBtnText,
                      item.solutionLearned ? styles.learnBtnTextOn : styles.learnBtnTextOff,
                    ]}
                  >
                    Çözümü öğrendim
                  </Text>
                </Pressable>
              </View>
            </View>
          );
        }}
      />

      <Modal visible={addOpen} animationType="slide" transparent onRequestClose={() => setAddOpen(false)}>
        <Pressable style={styles.modalBackdrop} onPress={() => !savingAdd && setAddOpen(false)}>
          <Pressable style={styles.sheet} onPress={(e) => e.stopPropagation()}>
            <View style={styles.sheetHeader}>
              <Text style={styles.sheetTitle}>Yeni soru</Text>
              <Pressable onPress={() => !savingAdd && setAddOpen(false)} hitSlop={10}>
                <Text style={{ color: colors.primary, fontWeight: '600' }}>Kapat</Text>
              </Pressable>
            </View>
            <ScrollView style={styles.sheetBody} keyboardShouldPersistTaps="handled">
              {pendingUri ? (
                <Image source={{ uri: pendingUri }} style={styles.previewImg} resizeMode="contain" />
              ) : null}
              <Pressable
                style={[styles.learnBtn, addLearned ? styles.learnBtnOn : styles.learnBtnOff]}
                onPress={() => setAddLearned((v) => !v)}
                accessibilityRole="button"
              >
                <Ionicons
                  name={addLearned ? 'checkmark-circle' : 'ellipse-outline'}
                  size={22}
                  color={addLearned ? colors.statusDone : colors.textMuted}
                />
                <Text style={[styles.learnBtnText, addLearned ? styles.learnBtnTextOn : styles.learnBtnTextOff]}>
                  Çözümü zaten biliyorum
                </Text>
              </Pressable>
              <Text style={styles.label}>Etiketler</Text>
              <View style={styles.chipWrap}>
                {PRESET_TAGS.map((t) => {
                  const on = addTags.includes(t);
                  return (
                    <Pressable key={t} style={[styles.chip, on && styles.chipOn]} onPress={() => togglePresetAdd(t)}>
                      <Text style={[styles.chipTxt, on && styles.chipTxtOn]}>{t}</Text>
                    </Pressable>
                  );
                })}
              </View>
              <View style={styles.customRow}>
                <TextInput
                  style={styles.customInput}
                  placeholder="Kendi etiketin"
                  placeholderTextColor={colors.textMuted}
                  keyboardAppearance={colors.keyboardAppearance}
                  value={addCustomTag}
                  onChangeText={setAddCustomTag}
                  onSubmitEditing={pushCustomAdd}
                />
                <Pressable style={styles.addTagBtn} onPress={pushCustomAdd}>
                  <Text style={styles.addTagBtnText}>Ekle</Text>
                </Pressable>
              </View>
              <Pressable
                style={[styles.primaryBtn, savingAdd && { opacity: 0.8 }]}
                onPress={() => void submitAdd()}
                disabled={savingAdd}
              >
                <Text style={styles.primaryBtnText}>{savingAdd ? 'Kaydediliyor…' : 'Kumbaraya at'}</Text>
              </Pressable>
            </ScrollView>
          </Pressable>
        </Pressable>
      </Modal>

      <Modal visible={detail != null} animationType="slide" transparent onRequestClose={() => setDetail(null)}>
        <Pressable style={styles.modalBackdrop} onPress={() => !savingDetail && setDetail(null)}>
          <Pressable style={styles.sheet} onPress={(e) => e.stopPropagation()}>
            <View style={styles.sheetHeader}>
              <Text style={styles.sheetTitle}>Not</Text>
              <Pressable onPress={() => !savingDetail && setDetail(null)} hitSlop={10}>
                <Text style={{ color: colors.primary, fontWeight: '600' }}>Kapat</Text>
              </Pressable>
            </View>
            <ScrollView style={styles.sheetBody} keyboardShouldPersistTaps="handled">
              {detail ? (
                <Pressable onPress={() => openLightbox(toImageUri(detail.imageUrl))} accessibilityRole="button">
                  <Image
                    source={{ uri: toImageUri(detail.imageUrl) }}
                    style={styles.detailFullImg}
                    resizeMode="contain"
                  />
                </Pressable>
              ) : null}
              <Pressable
                style={[styles.learnBtn, detailLearned ? styles.learnBtnOn : styles.learnBtnOff]}
                disabled={busyId === detail?.id}
                onPress={() => {
                  const next = !detailLearned;
                  setDetailLearned(next);
                  if (detail) void patchLearned(detail, next, detailTags);
                }}
                accessibilityRole="button"
              >
                <Ionicons
                  name={detailLearned ? 'checkmark-circle' : 'ellipse-outline'}
                  size={22}
                  color={detailLearned ? colors.statusDone : colors.textMuted}
                />
                <Text style={[styles.learnBtnText, detailLearned ? styles.learnBtnTextOn : styles.learnBtnTextOff]}>
                  Çözümü öğrendim
                </Text>
              </Pressable>
              <Text style={styles.label}>Etiketler</Text>
              <View style={styles.chipWrap}>
                {PRESET_TAGS.map((t) => {
                  const on = detailTags.includes(t);
                  return (
                    <Pressable key={t} style={[styles.chip, on && styles.chipOn]} onPress={() => togglePresetDetail(t)}>
                      <Text style={[styles.chipTxt, on && styles.chipTxtOn]}>{t}</Text>
                    </Pressable>
                  );
                })}
              </View>
              <View style={styles.customRow}>
                <TextInput
                  style={styles.customInput}
                  placeholder="Etiket ekle"
                  placeholderTextColor={colors.textMuted}
                  keyboardAppearance={colors.keyboardAppearance}
                  value={detailCustom}
                  onChangeText={setDetailCustom}
                  onSubmitEditing={pushCustomDetail}
                />
                <Pressable style={styles.addTagBtn} onPress={pushCustomDetail}>
                  <Text style={styles.addTagBtnText}>Ekle</Text>
                </Pressable>
              </View>
              <Pressable
                style={[styles.primaryBtn, savingDetail && { opacity: 0.8 }]}
                onPress={() => void saveDetail()}
                disabled={savingDetail}
              >
                <Text style={styles.primaryBtnText}>{savingDetail ? 'Kaydediliyor…' : 'Etiketleri kaydet'}</Text>
              </Pressable>
              {detail ? (
                <Pressable style={styles.dangerBtn} onPress={() => confirmDelete(detail)}>
                  <Text style={styles.dangerText}>Notu sil</Text>
                </Pressable>
              ) : null}
            </ScrollView>
          </Pressable>
        </Pressable>
      </Modal>

      <Modal visible={lightboxUri != null} transparent animationType="fade" onRequestClose={closeLightbox}>
        <View style={styles.lightboxRoot}>
          <Pressable style={styles.lightboxBackdrop} onPress={closeLightbox} accessibilityLabel="Kapat" />
          <View style={styles.lightboxCenter}>
            <Animated.View style={{ transform: [{ scale: lightboxScale }] }}>
              <Pressable onPress={onLightboxImagePress}>
                {lightboxUri ? (
                  <Image
                    source={{ uri: lightboxUri }}
                    style={[styles.lightboxImg, { height: height * 0.72 }]}
                    resizeMode="contain"
                  />
                ) : null}
              </Pressable>
            </Animated.View>
          </View>
          <Pressable style={styles.lightboxClose} onPress={closeLightbox} accessibilityRole="button">
            <Ionicons name="close" size={28} color="#fff" />
          </Pressable>
          <Text style={styles.lightboxHint}>Çift dokun: yakınlaştır · Boş alana dokun: kapat</Text>
        </View>
      </Modal>
    </SafeAreaView>
  );
}
