import { Stack } from 'expo-router';

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
        contentStyle: { backgroundColor: colors.bg },
      }}
    />
  );
}
