import React, { useCallback, useState } from 'react';
import { FlatList, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { api, errorMessage } from '../api';
import { colors } from '../theme';
import { Badge, Card, Empty, ErrorText } from '../ui';
import { t } from '../i18n';

export default function CoursesScreen() {
  const [courses, setCourses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    try {
      setError('');
      const { data } = await api.get('/api/student/courses');
      setCourses(data);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useFocusEffect(useCallback(() => { load(); }, [load]));

  return (
    <FlatList
      contentContainerStyle={{ padding: 16 }}
      data={courses}
      keyExtractor={(c) => c.id}
      refreshControl={<RefreshControl refreshing={loading} onRefresh={load} />}
      ListHeaderComponent={<ErrorText>{error}</ErrorText>}
      ListEmptyComponent={!loading && !error ? <Empty title={t('noCourses')} /> : null}
      renderItem={({ item: c }) => {
        const low = c.totalSessions > 0 && c.percent < c.thresholdPercent;
        return (
          <Card>
            <Text style={styles.code}>{c.code}</Text>
            <Text style={styles.name}>{c.name}</Text>
            <Text style={styles.muted}>{c.professor}</Text>
            <View style={styles.row}>
              <Text style={styles.pct}>{c.percent}%</Text>
              <Badge tone={low ? 'bad' : 'ok'} text={`${c.attended}/${c.totalSessions} ${t('sessions')}`} />
            </View>
            <View style={styles.bar}><View style={[styles.fill, { width: `${Math.min(100, c.percent)}%`, backgroundColor: low ? colors.danger : colors.accent }]} /></View>
          </Card>
        );
      }}
    />
  );
}

const styles = StyleSheet.create({
  code: { color: colors.accent, fontWeight: '800' },
  name: { fontSize: 18, fontWeight: '700', color: colors.text, marginTop: 2 },
  muted: { color: colors.muted, marginBottom: 10 },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  pct: { fontSize: 28, fontWeight: '800', color: colors.text },
  bar: { height: 8, backgroundColor: colors.bg, borderRadius: 99, marginTop: 8, overflow: 'hidden' },
  fill: { height: '100%', borderRadius: 99 },
});
