import AsyncStorage from '@react-native-async-storage/async-storage';
import DateTimePicker from '@react-native-community/datetimepicker';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Animated,
  Modal,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';

import {
  calendarDaysUntil,
  COUNTDOWN_PROGRESS_WINDOW_DAYS,
  countdownProgressRatio,
  formatYmd,
  getCountdownGuidance,
  getDefaultYksExamDate,
  parseYmdLocal,
  YKS_EXAM_DATE_STORAGE_KEY,
} from '../lib/yksCountdown';
import { useTheme } from '../theme';

type Props = {
  /** Kart dış padding ile uyum için */
  compact?: boolean;
};

export function ExamCountdownCard({ compact }: Props) {
  const { colors } = useTheme();
  const [examDate, setExamDate] = useState<Date>(() => getDefaultYksExamDate());
  const [hydrated, setHydrated] = useState(false);
  const [now, setNow] = useState(() => new Date());
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [pickerDate, setPickerDate] = useState(() => getDefaultYksExamDate());
  const [showPicker, setShowPicker] = useState(false);

  const anim = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 60_000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const raw = await AsyncStorage.getItem(YKS_EXAM_DATE_STORAGE_KEY);
        if (cancelled) return;
        if (raw) {
          const parsed = parseYmdLocal(raw);
          if (parsed) setExamDate(parsed);
        }
      } finally {
        if (!cancelled) setHydrated(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const remaining = calendarDaysUntil(examDate, now);
  const ratio = countdownProgressRatio(remaining);
  const guidance = getCountdownGuidance(remaining);

  useEffect(() => {
    Animated.timing(anim, {
      toValue: ratio,
      duration: 700,
      useNativeDriver: false,
    }).start();
  }, [anim, ratio]);

  const barWidth = anim.interpolate({
    inputRange: [0, 1],
    outputRange: ['0%', '100%'],
  });

  const styles = useMemo(
    () =>
      StyleSheet.create({
        card: {
          backgroundColor: colors.surface,
          borderRadius: 12,
          padding: compact ? 14 : 16,
          borderWidth: 1,
          borderColor: colors.border,
        },
        eyebrow: { fontSize: 12, fontWeight: '600', color: colors.statAccent, letterSpacing: 0.3 },
        title: { fontSize: 17, fontWeight: '700', color: colors.text, marginTop: 6, lineHeight: 24 },
        sub: { fontSize: 14, color: colors.textMuted, marginTop: 10, lineHeight: 21 },
        daysRow: { flexDirection: 'row', alignItems: 'baseline', marginTop: 14, gap: 8 },
        daysNum: { fontSize: 36, fontWeight: '800', color: colors.primary },
        daysLabel: { fontSize: 15, color: colors.textSecondary, fontWeight: '600' },
        track: {
          height: 10,
          borderRadius: 5,
          backgroundColor: colors.barTrack,
          marginTop: 14,
          overflow: 'hidden',
        },
        fill: {
          height: '100%',
          borderRadius: 5,
          backgroundColor: colors.statusProgress,
        },
        trackHint: { fontSize: 12, color: colors.textMuted, marginTop: 6 },
        footer: { marginTop: 12, alignSelf: 'flex-start' },
        footerText: { fontSize: 13, color: colors.primary, fontWeight: '600' },
        modalBackdrop: {
          flex: 1,
          backgroundColor: colors.overlay,
          justifyContent: 'center',
          padding: 24,
        },
        modalBox: {
          backgroundColor: colors.surface,
          borderRadius: 14,
          padding: 18,
          borderWidth: 1,
          borderColor: colors.border,
        },
        modalTitle: { fontSize: 17, fontWeight: '700', color: colors.text, marginBottom: 8 },
        modalBody: { fontSize: 14, color: colors.textMuted, lineHeight: 20, marginBottom: 14 },
        modalRow: { flexDirection: 'row', gap: 10, justifyContent: 'flex-end' },
        modalBtn: { paddingVertical: 10, paddingHorizontal: 14 },
        modalBtnText: { fontSize: 15, fontWeight: '600', color: colors.primary },
      }),
    [colors, compact]
  );

  const openSettings = useCallback(() => {
    setPickerDate(examDate);
    setSettingsOpen(true);
  }, [examDate]);

  const saveCustomDate = useCallback(async () => {
    const ymd = formatYmd(pickerDate);
    await AsyncStorage.setItem(YKS_EXAM_DATE_STORAGE_KEY, ymd);
    setExamDate(pickerDate);
    setSettingsOpen(false);
    setShowPicker(false);
  }, [pickerDate]);

  const resetToDefault = useCallback(async () => {
    await AsyncStorage.removeItem(YKS_EXAM_DATE_STORAGE_KEY);
    const d = getDefaultYksExamDate();
    setExamDate(d);
    setPickerDate(d);
    setSettingsOpen(false);
    setShowPicker(false);
  }, []);

  if (!hydrated) {
    return (
      <View style={styles.card}>
        <Text style={styles.sub}>Sınav takvimi yükleniyor…</Text>
      </View>
    );
  }

  return (
    <>
      <View style={styles.card}>
        <Text style={styles.eyebrow}>YKS · Zaman çizelgesi</Text>
        <Text style={styles.title}>{guidance.headline}</Text>
        <Text style={styles.sub}>{guidance.body}</Text>

        <View style={styles.daysRow}>
          <Text style={styles.daysNum}>{remaining < 0 ? '—' : remaining}</Text>
          <Text style={styles.daysLabel}>
            {remaining < 0 ? 'yeni tarih seçebilirsin' : remaining === 0 ? 'bugün' : 'gün kaldı'}
          </Text>
        </View>

        <View style={styles.track}>
          <Animated.View style={[styles.fill, { width: barWidth }]} />
        </View>
        <Text style={styles.trackHint}>
          Çubuk, son {COUNTDOWN_PROGRESS_WINDOW_DAYS} günlük dilimde hedefe yaklaşmanı gösterir — rakam kadar
          önemli değil, tempoyu hissettirmek için.
        </Text>

        <Pressable style={styles.footer} onPress={openSettings} hitSlop={8}>
          <Text style={styles.footerText}>Sınav tarihini ayarla</Text>
        </Pressable>
      </View>

      <Modal visible={settingsOpen} transparent animationType="fade" onRequestClose={() => setSettingsOpen(false)}>
        <Pressable style={styles.modalBackdrop} onPress={() => setSettingsOpen(false)}>
          <Pressable style={styles.modalBox} onPress={(e) => e.stopPropagation()}>
            <Text style={styles.modalTitle}>Hedef sınav günü</Text>
            <Text style={styles.modalBody}>
              Varsayılan: ÖSYM takvimine yakın Haziran tarihi (yılda bir uygulama güncellemesiyle
              düzeltilir). Kendi hedefini seçmek için tarihe dokun.
            </Text>
            <Pressable
              onPress={() => setShowPicker(true)}
              style={{
                borderWidth: 1,
                borderColor: colors.border,
                borderRadius: 10,
                paddingVertical: 12,
                paddingHorizontal: 14,
                marginBottom: 12,
                backgroundColor: colors.surfaceMuted,
              }}
            >
              <Text style={{ fontSize: 16, color: colors.text, fontWeight: '600' }}>
                {formatYmd(pickerDate)}
              </Text>
            </Pressable>
            {showPicker ? (
              <DateTimePicker
                value={pickerDate}
                mode="date"
                display={Platform.OS === 'ios' ? 'spinner' : 'default'}
                onChange={(_, d) => {
                  if (Platform.OS !== 'ios') setShowPicker(false);
                  if (d) setPickerDate(d);
                }}
              />
            ) : null}
            <View style={styles.modalRow}>
              <Pressable style={styles.modalBtn} onPress={resetToDefault}>
                <Text style={[styles.modalBtnText, { color: colors.textMuted }]}>Varsayılana dön</Text>
              </Pressable>
              <Pressable style={styles.modalBtn} onPress={() => void saveCustomDate()}>
                <Text style={styles.modalBtnText}>Kaydet</Text>
              </Pressable>
            </View>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}
