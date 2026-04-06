import { useLocalSearchParams, useNavigation } from 'expo-router';
import { useEffect, useMemo } from 'react';
import { ScrollView, StyleSheet, Text, useWindowDimensions } from 'react-native';

import { ACIK_RIZA_BEYANI, AYDINLATMA_METNI } from '../../src/legal/kvkkTexts';
import { getScale, rs } from '../../src/lib/responsive';
import { useTheme } from '../../src/theme';

type DocType = 'aydinlatma' | 'acik-riza';

export default function KvkkDocumentScreen() {
  const { colors } = useTheme();
  const navigation = useNavigation();
  const { type } = useLocalSearchParams<{ type?: string }>();
  const { width } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);

  const resolvedType: DocType = type === 'acik-riza' ? 'acik-riza' : 'aydinlatma';
  const isAydinlatma = resolvedType === 'aydinlatma';
  const body = isAydinlatma ? AYDINLATMA_METNI : ACIK_RIZA_BEYANI;
  const title = isAydinlatma ? 'KVKK Aydınlatma Metni' : 'Açık Rıza Beyanı';

  useEffect(() => {
    navigation.setOptions({ title, headerBackTitle: 'Geri' });
  }, [navigation, title]);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        scroll: { flex: 1, backgroundColor: colors.bg },
        content: { padding: rs(20, scale), paddingBottom: rs(40, scale) },
        body: { fontSize: rs(15, scale), lineHeight: rs(22, scale), color: colors.text },
      }),
    [colors, scale]
  );

  return (
    <ScrollView style={styles.scroll} contentContainerStyle={styles.content}>
      <Text style={styles.body}>{body}</Text>
    </ScrollView>
  );
}
