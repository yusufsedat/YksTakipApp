import { useEffect, useMemo, useState } from 'react';
import {
  FlatList,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';

import { getScale, rs } from '../lib/responsive';
import { useTheme } from '../theme';
import type { UserTopicRow } from '../lib/userTopicRows';

type Props = {
  visible: boolean;
  onClose: () => void;
  topics: UserTopicRow[];
  onSelect: (row: UserTopicRow) => void;
  title?: string;
};

export function TopicPickerModal({ visible, onClose, topics, onSelect, title = 'Konu seç' }: Props) {
  const { colors } = useTheme();
  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const [q, setQ] = useState('');

  useEffect(() => {
    if (visible) setQ('');
  }, [visible]);

  const filtered = useMemo(() => {
    const s = q.trim().toLocaleLowerCase('tr');
    if (!s) return topics;
    return topics.filter(
      (t) =>
        t.name.toLocaleLowerCase('tr').includes(s) ||
        t.subject.toLocaleLowerCase('tr').includes(s) ||
        t.category.toLocaleLowerCase('tr').includes(s)
    );
  }, [topics, q]);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        backdrop: { flex: 1, backgroundColor: colors.overlay, justifyContent: 'flex-end' },
        sheet: {
          backgroundColor: colors.surface,
          borderTopLeftRadius: rs(16, scale),
          borderTopRightRadius: rs(16, scale),
          borderWidth: 1,
          borderColor: colors.border,
          maxHeight: '88%',
        },
        header: {
          flexDirection: 'row',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: rs(16, scale),
          borderBottomWidth: StyleSheet.hairlineWidth,
          borderBottomColor: colors.border,
        },
        headerTitle: { fontSize: rs(17, scale), fontWeight: '700', color: colors.text },
        input: {
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingHorizontal: rs(12, scale),
          paddingVertical: rs(10, scale),
          fontSize: rs(16, scale),
          marginHorizontal: rs(16, scale),
          marginBottom: rs(8, scale),
          backgroundColor: colors.inputBg,
          color: colors.text,
        },
        row: {
          flexDirection: 'row',
          alignItems: 'center',
          paddingVertical: rs(12, scale),
          paddingHorizontal: rs(16, scale),
          borderBottomWidth: StyleSheet.hairlineWidth,
          borderBottomColor: colors.border,
        },
        rowMain: { flex: 1 },
        rowTitle: { fontSize: rs(16, scale), fontWeight: '600', color: colors.text },
        rowSub: { fontSize: rs(13, scale), color: colors.textMuted, marginTop: rs(4, scale) },
        empty: { color: colors.textMuted, textAlign: 'center', padding: rs(16, scale) },
      }),
    [colors, scale]
  );

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose}>
        <Pressable style={styles.sheet} onPress={(e) => e.stopPropagation()}>
          <View style={styles.header}>
            <Text style={styles.headerTitle}>{title}</Text>
            <Pressable onPress={onClose}>
              <Text style={{ color: colors.primary, fontWeight: '600' }}>Kapat</Text>
            </Pressable>
          </View>
          <TextInput
            style={styles.input}
            placeholder="Ara..."
            placeholderTextColor={colors.textMuted}
            keyboardAppearance={colors.keyboardAppearance}
            value={q}
            onChangeText={setQ}
          />
          <FlatList
            data={filtered}
            keyExtractor={(t) => String(t.topicId)}
            style={{ maxHeight: height * 0.5 }}
            keyboardShouldPersistTaps="handled"
            ListEmptyComponent={
              <Text style={styles.empty}>
                {topics.length === 0
                  ? 'Listende konu yok. Önce Konular sekmesinden konu ekle.'
                  : 'Eşleşen konu yok.'}
              </Text>
            }
            renderItem={({ item }) => (
              <Pressable
                style={styles.row}
                onPress={() => {
                  onSelect(item);
                  onClose();
                }}
              >
                <View style={styles.rowMain}>
                  <Text style={styles.rowTitle}>{item.name}</Text>
                  <Text style={styles.rowSub}>
                    {item.category} · {item.subject}
                  </Text>
                </View>
              </Pressable>
            )}
          />
        </Pressable>
      </Pressable>
    </Modal>
  );
}
