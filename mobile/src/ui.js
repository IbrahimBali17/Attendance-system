import React from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import { colors } from './theme';

export function Button({ title, onPress, kind = 'primary', disabled, loading }) {
  return (
    <Pressable onPress={onPress} disabled={disabled || loading}
      style={({ pressed }) => [styles.btn, kind === 'primary' ? styles.primary : styles.ghost, (disabled || loading) && { opacity: 0.5 }, pressed && { opacity: 0.8 }]}>
      {loading ? <ActivityIndicator color={kind === 'primary' ? '#fff' : colors.accent} /> :
        <Text style={[styles.btnText, kind !== 'primary' && { color: colors.accent }]}>{title}</Text>}
    </Pressable>
  );
}

export const Card = ({ children, style }) => <View style={[styles.card, style]}>{children}</View>;

export function Badge({ text, tone }) {
  const map = { ok: [colors.okBg, colors.accent], warn: [colors.warnBg, colors.warn], bad: [colors.dangerBg, colors.danger] };
  const [bg, fg] = map[tone] || map.ok;
  return <View style={[styles.badge, { backgroundColor: bg }]}><Text style={{ color: fg, fontWeight: '700', fontSize: 12 }}>{text}</Text></View>;
}

export const Empty = ({ title, hint }) => (
  <View style={styles.empty}>
    <Text style={styles.emptyTitle}>{title}</Text>
    {hint ? <Text style={styles.emptyHint}>{hint}</Text> : null}
  </View>
);

export const ErrorText = ({ children }) => (children ? <Text style={styles.error}>{children}</Text> : null);

const styles = StyleSheet.create({
  btn: { paddingVertical: 14, paddingHorizontal: 18, borderRadius: 12, alignItems: 'center' },
  primary: { backgroundColor: colors.accent },
  ghost: { backgroundColor: 'transparent', borderWidth: 1, borderColor: colors.border },
  btnText: { color: '#fff', fontWeight: '700', fontSize: 16 },
  card: { backgroundColor: colors.surface, borderRadius: 14, padding: 16, borderWidth: 1, borderColor: colors.border, marginBottom: 12 },
  badge: { paddingHorizontal: 10, paddingVertical: 3, borderRadius: 99, alignSelf: 'flex-start' },
  empty: { alignItems: 'center', padding: 32 },
  emptyTitle: { fontSize: 18, fontWeight: '700', color: colors.text, textAlign: 'center' },
  emptyHint: { marginTop: 8, color: colors.muted, textAlign: 'center' },
  error: { color: colors.danger, marginVertical: 8 },
});
