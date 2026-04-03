import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useCallback, useMemo } from 'react';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import { SafeAreaView } from 'react-native-safe-area-context';

import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { rs, rvs } from '../../src/lib/responsive';
import { type ThemeMode, useTheme } from '../../src/theme';

export default function SettingsScreen() {
  const { colors, mode, setMode } = useTheme();
  const insets = useSafeAreaInsets();

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        content: { padding: 24 },
        header: { flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 14 },
        headerTitle: { fontSize: 22, fontWeight: '800', color: colors.text },
        hint: { marginTop: 10, fontSize: 14, color: colors.textMuted, lineHeight: 20 },
        card: {
          marginTop: 18,
          backgroundColor: colors.surface,
          borderRadius: 12,
          padding: 16,
          borderWidth: 1,
          borderColor: colors.border,
        },
        cardTitle: { fontSize: 16, fontWeight: '800', color: colors.text, marginBottom: 6 },
        cardSub: { fontSize: 13, color: colors.textMuted, lineHeight: 19, marginBottom: 14 },
        row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
        chip: {
          paddingVertical: rvs(10, 1),
          paddingHorizontal: rs(14, 1),
          borderRadius: 20,
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.chip,
        },
        chipOn: { backgroundColor: colors.segmentOn, borderColor: colors.segmentOn },
        chipText: { fontSize: 14, fontWeight: '600', color: colors.chipText },
        chipTextOn: { color: colors.segmentTextOn },
        backBtnText: { fontSize: 14, fontWeight: '700', color: colors.primary },
      }),
    [colors]
  );

  const scrollContent = useMemo(
    () => [styles.content, { paddingBottom: 48 + insets.bottom }],
    [styles.content, insets.bottom]
  );

  const ThemeChip = useCallback(
    ({ value, label }: { value: ThemeMode; label: string }) => {
      const on = mode === value;
      return (
        <Pressable style={[styles.chip, on && styles.chipOn]} onPress={() => setMode(value)}>
          <Text style={[styles.chipText, on && styles.chipTextOn]}>{label}</Text>
        </Pressable>
      );
    },
    [mode, setMode, styles]
  );

  return (
    <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
      <ScrollView style={styles.container} contentContainerStyle={scrollContent}>
        <View style={styles.header}>
          <Pressable onPress={() => router.back()} hitSlop={10}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
              <Ionicons name="chevron-back-outline" size={20} color={colors.primary} />
              <Text style={styles.backBtnText}>Geri</Text>
            </View>
          </Pressable>
          <Text style={styles.headerTitle}>Ayarlar</Text>
        </View>

        <Text style={styles.hint}>Görünüm ayarlarını burada değiştir. Gece modu göz yorgunluğunu azaltır.</Text>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Görünüm</Text>
          <Text style={styles.cardSub}>Lacivert ve yumuşak yeşil tonlar odak için seçildi. Sarı/kırmızı yalnızca kritik durumlarda kullanılır.</Text>
          <View style={styles.row}>
            <ThemeChip value="system" label="Sistem" />
            <ThemeChip value="light" label="Açık" />
            <ThemeChip value="dark" label="Karanlık" />
          </View>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

