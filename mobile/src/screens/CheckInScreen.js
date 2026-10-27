import React, { useCallback, useRef, useState } from 'react';
import { ActivityIndicator, ScrollView, StyleSheet, Text, View } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import * as Location from 'expo-location';
import { useFocusEffect } from '@react-navigation/native';
import { api, errorMessage } from '../api';
import { colors } from '../theme';
import { Button, Empty } from '../ui';
import { t } from '../i18n';

export default function CheckInScreen() {
  const [permission, requestPermission] = useCameraPermissions();
  const [sessions, setSessions] = useState(null);       // null = loading
  const [state, setState] = useState({ phase: 'scan' }); // scan | checking | done
  const busy = useRef(false);

  const loadSessions = useCallback(async () => {
    try {
      const { data } = await api.get('/api/student/active-session');
      setSessions(data.sessions);
    } catch {
      setSessions([]);
    }
  }, []);

  useFocusEffect(useCallback(() => {
    busy.current = false;
    setState({ phase: 'scan' });
    loadSessions();
  }, [loadSessions]));

  const handleScan = async ({ data }) => {
    if (busy.current) return;
    busy.current = true;
    setState({ phase: 'checking' });

    let payload;
    try { payload = JSON.parse(data); } catch { payload = null; }
    if (!payload?.sessionId || !payload?.token) return setState({ phase: 'done', ok: false, message: t('badQr') });

    try {
      const loc = await Location.requestForegroundPermissionsAsync();
      if (loc.status !== 'granted') return setState({ phase: 'done', ok: false, message: t('locationNeeded') });
      const pos = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High });
      const res = await api.post('/api/attendance/check-in', {
        sessionId: payload.sessionId, token: payload.token,
        latitude: pos.coords.latitude, longitude: pos.coords.longitude,
      });
      setState({ phase: 'done', ok: true, record: res.data });
    } catch (e) {
      setState({ phase: 'done', ok: false, message: errorMessage(e) });
    }
  };

  const reset = () => { busy.current = false; setState({ phase: 'scan' }); loadSessions(); };

  if (!permission) return <View style={styles.center}><ActivityIndicator /></View>;
  if (!permission.granted) {
    return (
      <View style={styles.center}>
        <Empty title={t('cameraNeeded')} />
        <Button title={t('grant')} onPress={requestPermission} />
      </View>
    );
  }

  if (sessions && sessions.length === 0 && state.phase === 'scan') {
    return (
      <ScrollView contentContainerStyle={styles.center}>
        <Empty title={t('noSession')} hint={t('noSessionHint')} />
        <Button title={t('refresh')} kind="ghost" onPress={loadSessions} />
      </ScrollView>
    );
  }

  if (state.phase === 'checking') {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={colors.accent} />
        <Text style={styles.hint}>{t('checking')}</Text>
      </View>
    );
  }

  if (state.phase === 'done') {
    const ok = state.ok;
    return (
      <View style={styles.center}>
        <Text style={[styles.icon, { color: ok ? colors.accent : colors.danger }]}>{ok ? '\u2713' : '\u2717'}</Text>
        <Text style={styles.resultTitle}>{ok ? t('success') : t('failed')}</Text>
        {ok ? (
          <Text style={styles.hint}>
            {state.record.courseName}{'\n'}{state.record.status === 'Late' ? t('late') : t('present')} - {Math.round(state.record.distanceMeters)} m
          </Text>
        ) : <Text style={styles.hint}>{state.message}</Text>}
        <View style={{ marginTop: 20, alignSelf: 'stretch' }}>
          <Button title={ok ? 'OK' : t('scanAgain')} onPress={reset} />
        </View>
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: '#000' }}>
      <CameraView style={StyleSheet.absoluteFill} facing="back"
        barcodeScannerSettings={{ barcodeTypes: ['qr'] }} onBarcodeScanned={handleScan} />
      <View style={styles.overlay} pointerEvents="none">
        <View style={styles.frame} />
        <Text style={styles.overlayText}>{t('scanHint')}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  center: { flexGrow: 1, flex: 1, alignItems: 'center', justifyContent: 'center', padding: 28, backgroundColor: colors.bg },
  hint: { color: colors.muted, textAlign: 'center', marginTop: 10, fontSize: 16 },
  icon: { fontSize: 72, fontWeight: '800' },
  resultTitle: { fontSize: 22, fontWeight: '800', color: colors.text, textAlign: 'center' },
  overlay: { ...StyleSheet.absoluteFillObject, alignItems: 'center', justifyContent: 'center' },
  frame: { width: 250, height: 250, borderRadius: 24, borderWidth: 4, borderColor: '#ffffffcc' },
  overlayText: { color: '#fff', marginTop: 24, textAlign: 'center', paddingHorizontal: 40, fontSize: 16, textShadowColor: '#000', textShadowRadius: 6 },
});
