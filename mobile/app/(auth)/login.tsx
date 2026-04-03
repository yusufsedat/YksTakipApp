import { Link, router } from 'expo-router';
import { useMemo, useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';

import { SafeAreaView } from 'react-native-safe-area-context';

import { useAuth } from '../../src/lib/auth';
import { getScale, getVScale, rs, rvs } from '../../src/lib/responsive';
import { useTheme } from '../../src/theme';

export default function LoginScreen() {
  const { colors } = useTheme();
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        inner: { flex: 1, padding: rs(24, scale), justifyContent: 'center' },
        title: { fontSize: rs(26, scale), fontWeight: '700', color: colors.text, marginBottom: rs(8, scale) },
        subtitle: { fontSize: rs(16, scale), color: colors.textMuted, marginBottom: rs(28, scale) },
        input: {
          backgroundColor: colors.inputBg,
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: rs(10, scale),
          paddingHorizontal: rs(14, scale),
          paddingVertical: rvs(12, vScale),
          fontSize: rs(16, scale),
          marginBottom: rs(12, scale),
          color: colors.text,
        },
        error: { color: colors.errorText, marginBottom: rs(12, scale) },
        button: {
          backgroundColor: colors.primary,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(14, vScale),
          alignItems: 'center',
          marginTop: rs(8, scale),
        },
        buttonDisabled: { opacity: 0.7 },
        buttonText: { color: colors.onPrimary, fontSize: rs(16, scale), fontWeight: '600' },
        linkWrap: { marginTop: rs(20, scale), alignItems: 'center' },
        link: { color: colors.link, fontSize: rs(15, scale) },
      }),
    [scale, vScale, colors]
  );

  async function onSubmit() {
    setError(null);
    setSubmitting(true);
    try {
      await login(email.trim(), password);
      router.replace('/(app)');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Giriş yapılamadı.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <SafeAreaView style={styles.inner}>
        <Text style={styles.title}>Hoş geldin</Text>
        <Text style={styles.subtitle}>Hesabınla giriş yap</Text>

        <TextInput
          style={styles.input}
          placeholder="E-posta"
          placeholderTextColor={colors.textMuted}
          keyboardAppearance={colors.keyboardAppearance}
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="email-address"
          value={email}
          onChangeText={setEmail}
        />
        <TextInput
          style={styles.input}
          placeholder="Şifre"
          placeholderTextColor={colors.textMuted}
          keyboardAppearance={colors.keyboardAppearance}
          secureTextEntry
          value={password}
          onChangeText={setPassword}
        />

        {error ? <Text style={styles.error}>{error}</Text> : null}

        <Pressable
          style={[styles.button, submitting && styles.buttonDisabled]}
          onPress={onSubmit}
          disabled={submitting}
        >
          <Text style={styles.buttonText}>{submitting ? 'Giriş…' : 'Giriş yap'}</Text>
        </Pressable>

        <Link href="/(auth)/register" asChild>
          <Pressable style={styles.linkWrap}>
            <Text style={styles.link}>Hesabın yok mu? Kayıt ol</Text>
          </Pressable>
        </Link>
      </SafeAreaView>
    </KeyboardAvoidingView>
  );
}
