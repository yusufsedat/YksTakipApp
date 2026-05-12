import { router } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { createGoal, getGoalStatus, skipGoal } from '../../src/services/goals';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import { useTheme } from '../../src/theme';

function parseOptionalNet(raw: string, max: number): { ok: true; value: number | null } | { ok: false; message: string } {
  const t = raw.trim();
  if (!t) return { ok: true, value: null };
  const n = Number(t.replace(',', '.'));
  if (!Number.isFinite(n)) return { ok: false, message: 'Net değeri geçersiz.' };
  if (n < 0 || n > max) return { ok: false, message: `Net 0–${max} arasında olmalıdır.` };
  return { ok: true, value: Math.round(n * 100) / 100 };
}

export default function GoalOnboardingScreen() {
  const { colors } = useTheme();
  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);

  const [university, setUniversity] = useState('');
  const [department, setDepartment] = useState('');
  const [tytTxt, setTytTxt] = useState('');
  const [aytTxt, setAytTxt] = useState('');
  const [dailyMinutesTxt, setDailyMinutesTxt] = useState('120');
  const [canSkip, setCanSkip] = useState(false);
  const [loadingStatus, setLoadingStatus] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [skipping, setSkipping] = useState(false);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        scrollContent: { padding: rs(24, scale), paddingBottom: rs(40, scale) },
        title: { fontSize: rs(22, scale), fontWeight: '800', color: colors.text, marginBottom: rs(8, scale) },
        sub: { fontSize: rs(14, scale), color: colors.textMuted, lineHeight: rs(20, scale), marginBottom: rs(20, scale) },
        label: { fontSize: rs(13, scale), fontWeight: '700', color: colors.textMuted, marginBottom: rs(6, scale) },
        input: {
          backgroundColor: colors.inputBg,
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingHorizontal: rs(14, scale),
          paddingVertical: rvs(12, vScale),
          fontSize: rs(16, scale),
          marginBottom: rs(14, scale),
          color: colors.text,
        },
        error: { color: colors.errorText, marginBottom: rs(12, scale) },
        primaryBtn: {
          backgroundColor: colors.primary,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(14, vScale),
          alignItems: 'center',
          marginTop: rs(8, scale),
        },
        primaryBtnDisabled: { opacity: 0.7 },
        primaryBtnText: { color: colors.onPrimary, fontSize: rs(16, scale), fontWeight: '600' },
        secondaryBtn: {
          marginTop: rs(12, scale),
          paddingVertical: rvs(12, vScale),
          alignItems: 'center',
        },
        secondaryBtnText: { color: colors.link, fontSize: rs(15, scale), fontWeight: '600' },
      }),
    [colors, scale, vScale]
  );

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoadingStatus(true);
      setError(null);
      try {
        const s = await getGoalStatus();
        if (cancelled) return;
        setCanSkip(s.canSkip);
        const g = s.currentGoal;
        if (g) {
          setUniversity(g.targetUniversity);
          setDepartment(g.targetDepartment);
          setTytTxt(g.targetTytNet != null ? String(g.targetTytNet).replace('.', ',') : '');
          setAytTxt(g.targetAytNet != null ? String(g.targetAytNet).replace('.', ',') : '');
          setDailyMinutesTxt(String(g.dailyAvailableMinutes));
        }
      } catch (e) {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : 'Durum alınamadı.');
        }
      } finally {
        if (!cancelled) setLoadingStatus(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  async function onSave() {
    setError(null);
    const u = university.trim();
    const d = department.trim();
    if (!u || !d) {
      setError('Üniversite ve bölüm zorunludur.');
      return;
    }
    const tyt = parseOptionalNet(tytTxt, 120);
    if (!tyt.ok) {
      setError(tyt.message);
      return;
    }
    const ayt = parseOptionalNet(aytTxt, 80);
    if (!ayt.ok) {
      setError(ayt.message);
      return;
    }
    const dailyMinutes = Number(dailyMinutesTxt.trim());
    if (!Number.isFinite(dailyMinutes) || dailyMinutes < 30) {
      setError('Günlük çalışma kapasitesi en az 30 dakika olmalıdır.');
      return;
    }

    setSubmitting(true);
    try {
      await createGoal({
        targetUniversity: u,
        targetDepartment: d,
        targetTytNet: tyt.value,
        targetAytNet: ayt.value,
        dailyAvailableMinutes: Math.round(dailyMinutes),
      });
      router.replace('/(app)/smart-plan');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Hedef kaydedilemedi.');
    } finally {
      setSubmitting(false);
    }
  }

  async function onSkip() {
    if (!canSkip) return;
    setError(null);
    setSkipping(true);
    try {
      await skipGoal();
      router.replace('/(app)/dynamic-plan');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'İşlem yapılamadı.');
    } finally {
      setSkipping(false);
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.scrollContent}>
          <Text style={styles.title}>Hedefini belirle</Text>
          <Text style={styles.sub}>
            Hedef üniversite ve bölümünü kaydet; TYT/AYT net hedeflerin isteğe bağlıdır. Kaydettiğinde Akıllı Plan akışına
            yönlendirilirsin.
          </Text>

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <Text style={styles.label}>Hedef üniversite</Text>
          <TextInput
            style={styles.input}
            value={university}
            onChangeText={setUniversity}
            placeholder="Örn. İstanbul Üniversitesi"
            placeholderTextColor={colors.textMuted}
            maxLength={200}
            editable={!loadingStatus && !submitting}
            keyboardAppearance={colors.keyboardAppearance}
          />

          <Text style={styles.label}>Hedef bölüm</Text>
          <TextInput
            style={styles.input}
            value={department}
            onChangeText={setDepartment}
            placeholder="Örn. Bilgisayar Mühendisliği"
            placeholderTextColor={colors.textMuted}
            maxLength={200}
            editable={!loadingStatus && !submitting}
            keyboardAppearance={colors.keyboardAppearance}
          />

          <Text style={styles.label}>Hedef TYT net (isteğe bağlı)</Text>
          <TextInput
            style={styles.input}
            value={tytTxt}
            onChangeText={setTytTxt}
            placeholder="0–120"
            placeholderTextColor={colors.textMuted}
            keyboardType="decimal-pad"
            editable={!loadingStatus && !submitting}
            keyboardAppearance={colors.keyboardAppearance}
          />

          <Text style={styles.label}>Hedef AYT net (isteğe bağlı)</Text>
          <TextInput
            style={styles.input}
            value={aytTxt}
            onChangeText={setAytTxt}
            placeholder="0–80"
            placeholderTextColor={colors.textMuted}
            keyboardType="decimal-pad"
            editable={!loadingStatus && !submitting}
            keyboardAppearance={colors.keyboardAppearance}
          />

          <Text style={styles.label}>Günlük çalışma kapasitesi (dk)</Text>
          <TextInput
            style={styles.input}
            value={dailyMinutesTxt}
            onChangeText={setDailyMinutesTxt}
            placeholder="En az 30"
            placeholderTextColor={colors.textMuted}
            keyboardType="number-pad"
            editable={!loadingStatus && !submitting}
            keyboardAppearance={colors.keyboardAppearance}
          />

          <Pressable
            style={[styles.primaryBtn, submitting || loadingStatus ? styles.primaryBtnDisabled : null]}
            onPress={() => void onSave()}
            disabled={submitting || loadingStatus}
          >
            <Text style={styles.primaryBtnText}>{submitting ? 'Kaydediliyor…' : 'Kaydet ve Başla'}</Text>
          </Pressable>

          {canSkip ? (
            <Pressable
              style={styles.secondaryBtn}
              onPress={() => void onSkip()}
              disabled={skipping || loadingStatus}
              hitSlop={10}
            >
              <Text style={styles.secondaryBtnText}>{skipping ? 'İşleniyor…' : 'Şimdilik Geç'}</Text>
            </Pressable>
          ) : null}
        </ScrollView>
      </SafeAreaView>
    </KeyboardAvoidingView>
  );
}
