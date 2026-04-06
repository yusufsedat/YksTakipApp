import { Ionicons } from '@expo/vector-icons';
import { Link, router } from 'expo-router';
import { useMemo, useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
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

function openAydinlatma() {
  router.push('/(auth)/kvkk-document?type=aydinlatma');
}

function openAcikRiza() {
  router.push('/(auth)/kvkk-document?type=acik-riza');
}

export default function RegisterScreen() {
  const { colors } = useTheme();
  const { register } = useAuth();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [kvkkReadAccepted, setKvkkReadAccepted] = useState(false);
  const [consentAccepted, setConsentAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const { width, height } = useWindowDimensions();
  const scale = useMemo(() => getScale(width), [width]);
  const vScale = useMemo(() => getVScale(height), [height]);

  const styles = useMemo(
    () =>
      StyleSheet.create({
        container: { flex: 1, backgroundColor: colors.bg },
        scrollContent: {
          flexGrow: 1,
          paddingHorizontal: rs(24, scale),
          paddingTop: rs(16, scale),
          paddingBottom: rs(32, scale),
        },
        title: { fontSize: rs(26, scale), fontWeight: '700', color: colors.text, marginBottom: rs(8, scale) },
        subtitle: { fontSize: rs(16, scale), color: colors.textMuted, marginBottom: rs(22, scale) },
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
        legalBlock: { marginTop: rs(4, scale), marginBottom: rs(8, scale) },
        legalRow: { flexDirection: 'row', alignItems: 'flex-start', marginBottom: rs(14, scale), gap: rs(10, scale) },
        checkTouch: { marginTop: rs(2, scale) },
        checkOuter: {
          width: rs(22, scale),
          height: rs(22, scale),
          borderRadius: rs(5, scale),
          borderWidth: 2,
          borderColor: colors.primary,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: colors.inputBg,
        },
        checkOuterOn: { backgroundColor: colors.primary, borderColor: colors.primary },
        legalText: {
          flex: 1,
          fontSize: rs(14, scale),
          color: colors.text,
          lineHeight: rs(21, scale),
        },
        inlineLink: { color: colors.link, fontWeight: '600', textDecorationLine: 'underline' },
        requiredMark: { fontSize: rs(13, scale), color: colors.textMuted, fontWeight: '600' },
        error: { color: colors.errorText, marginBottom: rs(12, scale), marginTop: rs(4, scale) },
        button: {
          backgroundColor: colors.admin,
          borderRadius: rs(10, scale),
          paddingVertical: rvs(14, vScale),
          alignItems: 'center',
          marginTop: rs(12, scale),
        },
        buttonDisabled: { opacity: 0.45 },
        buttonText: { color: colors.onAdmin, fontSize: rs(16, scale), fontWeight: '600' },
        linkWrap: { marginTop: rs(20, scale), alignItems: 'center' },
        link: { color: colors.link, fontSize: rs(15, scale) },
      }),
    [scale, vScale, colors]
  );

  const canSubmit = kvkkReadAccepted && consentAccepted && !submitting;

  async function onSubmit() {
    setError(null);
    if (!kvkkReadAccepted || !consentAccepted) {
      setError('Kayıt olmak için zorunlu onay kutularını işaretlemelisiniz.');
      return;
    }
    setSubmitting(true);
    try {
      await register(name.trim(), email.trim(), password);
      router.replace('/(app)');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Kayıt oluşturulamadı.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <SafeAreaView style={{ flex: 1 }} edges={['bottom']}>
        <ScrollView
          keyboardShouldPersistTaps="handled"
          contentContainerStyle={styles.scrollContent}
          showsVerticalScrollIndicator={false}
        >
          <Text style={styles.title}>Kayıt ol</Text>
          <Text style={styles.subtitle}>YKS takibine başla</Text>

          <TextInput
            style={styles.input}
            placeholder="Ad Soyad"
            placeholderTextColor={colors.textMuted}
            keyboardAppearance={colors.keyboardAppearance}
            autoCapitalize="words"
            value={name}
            onChangeText={setName}
          />
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
            placeholder="Şifre (en az 6 karakter)"
            placeholderTextColor={colors.textMuted}
            keyboardAppearance={colors.keyboardAppearance}
            secureTextEntry
            value={password}
            onChangeText={setPassword}
          />

          <View style={styles.legalBlock}>
            <View style={styles.legalRow}>
              <Pressable
                style={styles.checkTouch}
                onPress={() => setKvkkReadAccepted((v) => !v)}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: kvkkReadAccepted }}
                hitSlop={8}
              >
                <View style={[styles.checkOuter, kvkkReadAccepted && styles.checkOuterOn]}>
                  {kvkkReadAccepted ? (
                    <Ionicons name="checkmark" size={rs(15, scale)} color={colors.onPrimary} />
                  ) : null}
                </View>
              </Pressable>
              <Text style={styles.legalText}>
                <Text onPress={openAydinlatma} style={styles.inlineLink}>
                  KVKK Aydınlatma Metni
                </Text>
                <Text>'ni okudum ve anladım. </Text>
                <Text style={styles.requiredMark}>(Zorunlu)</Text>
              </Text>
            </View>

            <View style={styles.legalRow}>
              <Pressable
                style={styles.checkTouch}
                onPress={() => setConsentAccepted((v) => !v)}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: consentAccepted }}
                hitSlop={8}
              >
                <View style={[styles.checkOuter, consentAccepted && styles.checkOuterOn]}>
                  {consentAccepted ? (
                    <Ionicons name="checkmark" size={rs(15, scale)} color={colors.onPrimary} />
                  ) : null}
                </View>
              </Pressable>
              <Text style={styles.legalText}>
                <Text>Kişisel verilerimin, </Text>
                <Text onPress={openAydinlatma} style={styles.inlineLink}>
                  Aydınlatma Metni
                </Text>
                <Text>'nde belirtilen amaçlarla işlenmesine ve saklanmasına onay veriyorum. </Text>
                <Text onPress={openAcikRiza} style={styles.inlineLink}>
                  Açık Rıza Beyanı
                </Text>
                <Text>'nı kabul ediyorum. </Text>
                <Text style={styles.requiredMark}>(Zorunlu)</Text>
              </Text>
            </View>
          </View>

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <Pressable
            style={[styles.button, !canSubmit && styles.buttonDisabled]}
            onPress={onSubmit}
            disabled={!canSubmit}
          >
            <Text style={styles.buttonText}>{submitting ? 'Kaydediliyor…' : 'Kayıt ol'}</Text>
          </Pressable>

          <Link href="/(auth)/login" asChild>
            <Pressable style={styles.linkWrap}>
              <Text style={styles.link}>Zaten hesabın var mı? Giriş yap</Text>
            </Pressable>
          </Link>
        </ScrollView>
      </SafeAreaView>
    </KeyboardAvoidingView>
  );
}
