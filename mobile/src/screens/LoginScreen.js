import React, { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { colors } from '../theme';
import { useAuth } from '../auth';
import { errorMessage } from '../api';
import { Button, ErrorText } from '../ui';
import { getLang, setLang, t } from '../i18n';

export default function LoginScreen({ onLanguageChange }) {
  const { login } = useAuth();
  const [id, setId] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    setError('');
    try {
      await login(id.trim(), password);
    } catch (e) {
      setError(e?.response?.status === 401 ? 'Invalid credentials.' : errorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <KeyboardAvoidingView style={styles.wrap} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <View style={styles.logo}><Text style={styles.logoText}>HU</Text></View>
      <Text style={styles.title}>{t('appName')}</Text>
      <Text style={styles.sub}>The Hashemite University</Text>
      <TextInput style={styles.input} placeholder={t('login')} autoCapitalize="none" autoCorrect={false} value={id} onChangeText={setId} />
      <TextInput style={styles.input} placeholder={t('password')} secureTextEntry value={password} onChangeText={setPassword} onSubmitEditing={submit} />
      <ErrorText>{error}</ErrorText>
      <Button title={busy ? t('signingIn') : t('signIn')} onPress={submit} loading={busy} disabled={!id || !password} />
      <Pressable onPress={() => { setLang(getLang() === 'en' ? 'ar' : 'en'); onLanguageChange(); }} style={{ marginTop: 20 }}>
        <Text style={styles.lang}>{t('language')}</Text>
      </Pressable>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  wrap: { flex: 1, justifyContent: 'center', padding: 28, backgroundColor: colors.bg },
  logo: { alignSelf: 'center', backgroundColor: colors.accent, borderRadius: 18, paddingHorizontal: 20, paddingVertical: 14 },
  logoText: { color: '#fff', fontWeight: '800', fontSize: 26 },
  title: { fontSize: 26, fontWeight: '800', textAlign: 'center', color: colors.text, marginTop: 14 },
  sub: { textAlign: 'center', color: colors.muted, marginBottom: 28 },
  input: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderRadius: 12, padding: 14, fontSize: 16, marginBottom: 12 },
  lang: { textAlign: 'center', color: colors.accent, fontWeight: '700' },
});
