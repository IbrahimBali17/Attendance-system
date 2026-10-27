import React, { useCallback, useState } from 'react';
import { FlatList, RefreshControl, StyleSheet, Text } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { api, errorMessage } from '../api';
import { colors } from '../theme';
import { Card, Empty, ErrorText } from '../ui';
import { t } from '../i18n';

export default function NotificationsScreen() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    try {
      setError('');
      const { data } = await api.get('/api/student/notifications');
      setItems(data);
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
      data={items}
      keyExtractor={(n) => n.id}
      refreshControl={<RefreshControl refreshing={loading} onRefresh={load} />}
      ListHeaderComponent={<ErrorText>{error}</ErrorText>}
      ListEmptyComponent={!loading && !error ? <Empty title={t('noAlerts')} /> : null}
      renderItem={({ item }) => (
        <Card>
          <Text style={styles.msg}>{item.message}</Text>
          <Text style={styles.time}>{new Date(item.sentAt.endsWith('Z') ? item.sentAt : `${item.sentAt}Z`).toLocaleString()}</Text>
        </Card>
      )}
    />
  );
}

const styles = StyleSheet.create({
  msg: { color: colors.text, fontSize: 15 },
  time: { color: colors.muted, marginTop: 6, fontSize: 12 },
});
