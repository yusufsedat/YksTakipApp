import { Ionicons } from '@expo/vector-icons';
import { Tabs } from 'expo-router';
import { View } from 'react-native';

import { BrandIconSmall } from '../../src/components/BrandIconSmall';
import { getCompactHeaderScreenOptions } from '../../src/navigation/headerScreenOptions';
import { useTheme } from '../../src/theme';

export default function AppTabLayout() {
  const { colors } = useTheme();
  return (
    <Tabs
      screenOptions={{
        ...getCompactHeaderScreenOptions(colors),
        headerRight: () => (
          <View style={{ marginRight: 14 }}>
            <BrandIconSmall size={36} />
          </View>
        ),
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.tabInactive,
        tabBarStyle: {
          backgroundColor: colors.tabBg,
          borderTopColor: colors.tabBorder,
        },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: 'Özet',
          tabBarIcon: ({ color, size }) => <Ionicons name="home-outline" size={size} color={color} />,
        }}
      />
      <Tabs.Screen
        name="topics"
        options={{
          title: 'Konular',
          tabBarIcon: ({ color, size }) => <Ionicons name="book-outline" size={size} color={color} />,
        }}
      />
      <Tabs.Screen
        name="tools"
        options={{
          title: 'Araçlar',
          tabBarIcon: ({ color, size }) => <Ionicons name="apps-outline" size={size} color={color} />,
        }}
      />
      <Tabs.Screen
        name="exams"
        options={{
          title: 'Denemeler',
          tabBarIcon: ({ color, size }) => <Ionicons name="document-text-outline" size={size} color={color} />,
        }}
      />
      <Tabs.Screen
        name="stats"
        options={{
          title: 'İstatistik',
          tabBarIcon: ({ color, size }) => <Ionicons name="stats-chart-outline" size={size} color={color} />,
        }}
      />
      {/* Araçlar menüsünden açılır; alt sekmede gösterme (Expo Router tüm dosyaları tab yapıyor) */}
      <Tabs.Screen name="study" options={{ href: null, title: 'Çalışmalarım' }} />
      <Tabs.Screen name="notebook" options={{ href: null }} />
      <Tabs.Screen name="schedule" options={{ href: null }} />
      <Tabs.Screen name="settings" options={{ href: null }} />
    </Tabs>
  );
}
