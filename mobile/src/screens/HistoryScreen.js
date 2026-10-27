import React, { useCallback, useState } from 'react';
import { RefreshControl, SectionList, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { api, errorMessage } from '../api';
import { colors } from '../theme';
import { Badge, Empty, ErrorText } from '../ui';
import { t } from '../i18n';

const asDate = (iso) => new Date(iso.endsWith('Z') ? iso : `${iso}Z`);

export default function HistoryScreen() {
  const [courses, setCourses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    try {
      setError('');
      const { data } = await api.get('/api/student/attendance-history');
      setCourses(data);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useFocusEffect(useCallback(() => { load(); }, [load]));

  const sections = courses.map((c) => ({ ...c, data: c.records }));

  return (
    <SectionList
      sections={sections}
      keyExtractor={(r) => r.id}
      contentContainerStyle={{ padding: 16 }}
      refreshControl={<RefreshControl refreshing={loading} onRefresh={load} />}
      ListHeaderComponent={<ErrorText>{error}</ErrorText>}
      ListEmptyComponent={!loading && !error ? <Empty title={t('noHistory')} /> : null}
      renderSectionHeader={({ section }) => (
        <View style={styles.header}>
          <Text style={styles.course}>{section.courseName}</Text>
          <Text style={styles.pct}>{section.percent}% ({section.attended}/{section.totalSessions})</Text>
        </View>
      )}
      renderItem={({ item }) => (
        <View style={styles.row}>
          <View>
            <Text style={styles.date}>{asDate(item.checkedInAt).toLocaleDateString()}</Text>
            <Text style={styles.time}>{asDate(item.checkedInAt).toLocaleTimeString()}</Text>
          </View>
          <Badge tone={item.status === 'Late' ? 'warn' : 'ok'} text={item.status === 'Late' ? t('late') : t('present')} />
        </View>
      )}
    />
  );
}

const styles = StyleSheet.create({
  header: { backgroundColor: colors.bg, paddingVertical: 10, marginTop: 8 },
  course: { fontSize: 17, fontWeight: '800', color: colors.text },
  pct: { color: colors.muted, marginTop: 2 },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', backgroundColor: colors.surface, padding: 14, borderRadius: 12, borderWidth: 1, borderColor: colors.border, marginBottom: 8 },
  date: { fontWeight: '700', color: colors.text }, time: { color: colors.muted },
});
