import { Stack } from 'expo-router';
import { View } from 'react-native';

import { BrandIconSmall } from '../../src/components/BrandIconSmall';
import { getCompactHeaderScreenOptions } from '../../src/navigation/headerScreenOptions';
import { useTheme } from '../../src/theme';

export default function AuthLayout() {
  const { colors } = useTheme();
  return (
    <Stack
      screenOptions={{
        ...getCompactHeaderScreenOptions(colors),
        headerShown: true,
        title: 'YksTakip',
        headerBackTitle: 'Geri',
        headerRight: () => (
          <View style={{ marginRight: 14 }}>
            <BrandIconSmall size={36} />
          </View>
        ),
        contentStyle: { backgroundColor: colors.bg },
      }}
    />
  );
}
