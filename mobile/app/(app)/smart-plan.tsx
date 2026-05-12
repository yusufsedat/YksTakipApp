import { router } from 'expo-router';
import { useMemo } from 'react';
import { Pressable, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { TAB_SCREEN_EDGES } from '../../src/navigation/tabScreenSafeArea';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import { useTheme } from '../../src/theme';

export default function SmartPlanScreen() {
  const { colors } = useTheme();
  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        inner: { flex: 1, padding: rs(24, scale), justifyContent: 'center' },
        title: { fontSize: rs(22, scale), fontWeight: '800', color: colors.text, marginBottom: rs(10, scale) },
        sub: { fontSize: rs(15, scale), color: colors.textMuted, lineHeight: rs(22, scale), marginBottom: rs(24, scale) },
        btn: {
          backgroundColor: colors.primary,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(14, vScale),
          alignItems: 'center',
        },
        btnText: { color: colors.onPrimary, fontSize: rs(16, scale), fontWeight: '600' },
      }),
    [colors, scale, vScale]
  );

  return (
    <SafeAreaView style={styles.container} edges={TAB_SCREEN_EDGES}>
      <View style={styles.inner}>
        <Text style={styles.title}>Akıllı Plan</Text>
        <Text style={styles.sub}>Bu bölüm yakında eklenecek. Şimdilik ana ekrana dönerek uygulamayı kullanmaya devam edebilirsin.</Text>
        <Pressable style={styles.btn} onPress={() => router.replace('/(app)')}>
          <Text style={styles.btnText}>Özet’e dön</Text>
        </Pressable>
      </View>
    </SafeAreaView>
  );
}
