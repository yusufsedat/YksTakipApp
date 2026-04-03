import type { Edge } from 'react-native-safe-area-context';

/**
 * Alt sekme + Stack başlığı kullanılan ekranlarda: üst güvenli alan başlıkta
 * hesaplanır; içerikte SafeAreaView ile tekrar üst inset uygulanırsa çift boşluk oluşur.
 * Alt inset scroll içinde `useSafeAreaInsets().bottom` ile eklenir (tab bar üstü).
 */
export const TAB_SCREEN_EDGES: ReadonlyArray<Edge> = ['left', 'right'];
