import type { ThemeColors } from '../theme';

/** Tüm Stack / Tabs ekranlarında tutarlı, daha kompakt üst başlık. */
export function getCompactHeaderScreenOptions(colors: ThemeColors) {
  return {
    headerShadowVisible: false,
    headerStyle: {
      backgroundColor: colors.headerBg,
      minHeight: 46,
    },
    /** paddingTop/Bottom headerStyle'da etkisizdir (uyarı). Dikey sıkılık burada. */
    headerTitleContainerStyle: {
      paddingVertical: 2,
    },
    headerTintColor: colors.text,
    headerTitleStyle: {
      color: colors.text,
      fontWeight: '600' as const,
      fontSize: 17,
    },
  };
}
