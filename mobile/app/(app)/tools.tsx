import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useMemo } from 'react';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Pressable, ScrollView, StyleSheet, Text, View, useWindowDimensions } from 'react-native';

import { SafeAreaView } from 'react-native-safe-area-context';

import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import { useTheme } from '../../src/theme';

const TOOL_ITEMS = [
  { key: 'study', title: 'Çalışmalarım', icon: 'time-outline', href: '/(app)/study' },
  { key: 'schedule', title: 'Program', icon: 'calendar-outline', href: '/(app)/schedule' },
  { key: 'notebook', title: 'Kumbara', icon: 'archive-outline', href: '/(app)/notebook' },
  { key: 'settings', title: 'Görünüm', icon: 'color-palette-outline', href: '/(app)/settings' },
];

export default function ToolsScreen() {
  const { colors } = useTheme();
  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);
  const insets = useSafeAreaInsets();

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        content: { padding: 24 },
        header: { marginBottom: 14 },
        title: { fontSize: 22, fontWeight: '800', color: colors.text },
        sub: { marginTop: 6, fontSize: 14, color: colors.textMuted, lineHeight: 20 },
        cardGrid: { gap: 12, marginTop: 18 },
        toolCard: {
          backgroundColor: colors.surface,
          borderRadius: 12,
          borderWidth: 1,
          borderColor: colors.border,
          padding: 16,
        },
        toolBtn: {
          flexDirection: 'row',
          alignItems: 'center',
          gap: rs(12, scale),
          paddingVertical: rvs(14, vScale),
          paddingHorizontal: rs(12, scale),
        },
        toolIconWrap: {
          width: rs(42, scale),
          height: rs(42, scale),
          borderRadius: rs(14, scale),
          backgroundColor: colors.surfaceMuted,
          borderWidth: 1,
          borderColor: colors.border,
          justifyContent: 'center',
          alignItems: 'center',
        },
        toolTitle: { fontSize: 16, fontWeight: '800', color: colors.text },
        toolHint: { marginTop: 6, fontSize: 13, color: colors.textMuted, lineHeight: 18 },
        footerHint: { marginTop: 18, fontSize: 13, color: colors.textMuted, lineHeight: 20 },
      }),
    [colors]
  );

  const scrollContent = useMemo(
    () => [styles.content, { paddingBottom: 48 + insets.bottom }],
    [styles.content, insets.bottom]
  );

  return (
    <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
      <ScrollView style={styles.container} contentContainerStyle={scrollContent}>
        <View style={styles.header}>
          <Text style={styles.title}>Araçlar</Text>
          <Text style={styles.sub}>Çalışma, program ve kumbara işlemlerini tek yerde toplayan menü.</Text>
        </View>

        <View style={styles.cardGrid}>
          {TOOL_ITEMS.map((t) => (
            <View key={t.key} style={styles.toolCard}>
              <Pressable style={styles.toolBtn} onPress={() => router.push(t.href)} hitSlop={10}>
                <View style={styles.toolIconWrap}>
                  <Ionicons name={t.icon as any} size={20} color={colors.primary} />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.toolTitle}>{t.title}</Text>
                  <Text style={styles.toolHint}>
                    {t.key === 'study'
                      ? 'Geçen çalışma dakikalarını ekle ve takip et.'
                      : t.key === 'schedule'
                        ? 'Haftalık/aylık ders tekrar slotlarını planla.'
                        : t.key === 'notebook'
                          ? 'Çözülemeyen soruları fotoğrafla not al ve etiketle.'
                          : 'Tema modunu seç ve gece deneyimini ayarla.'}
                  </Text>
                </View>
              </Pressable>
            </View>
          ))}
        </View>

        <Text style={styles.footerHint}>
          İpucu: İhtiyaç olan ekrana buradan tek dokunuşla gidip başparmak stresini azaltabilirsin.
        </Text>
      </ScrollView>
    </SafeAreaView>
  );
}

