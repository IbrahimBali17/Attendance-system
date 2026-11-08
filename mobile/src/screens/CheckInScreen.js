import React, { useCallback, useRef, useState } from 'react';
import { ActivityIndicator, ScrollView, StyleSheet, Text, View } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { Camera as LegacyCamera } from 'expo-camera/legacy';
import * as Location from 'expo-location';
import { useFocusEffect } from '@react-navigation/native';
import { api, errorMessage } from '../api';
import { colors } from '../theme';
import { Button, Empty } from '../ui';
import { t } from '../i18n';

const VERSION = 'v3';

export default function CheckInScreen() {
  const [permission, requestPermission] = useCameraPermissions();
  const [sessions, setSessions] = useState(null);        // null = still loading
  const [state, setState] = useState({ phase: 'scan' }); // scan | checking | done
  // "legacy" uses the older Android camera engine, which draws correctly on phones where the new one shows a grey screen.
  const [engine, setEngine] = useState('legacy');
  const [cameraKey, setCameraKey] = useState(0);
  const [cameraOn, setCameraOn] = useState(false);
  const [camReady, setCamReady] = useState(false);
  const [camError, setCamError] = useState('');
  const busy = useRef(false);
  const timer = useRef(null);

  const loadSessions = useCallback(async () => {
    try {
      const { data } = await api.get('/api/student/active-session');
      setSessions(data.sessions);
    } catch {
      setSessions([]);
    }
  }, []);

  // Android needs a moment to release the camera before it can be opened again.
  const startCamera = useCallback((delay = 500) => {
    clearTimeout(timer.current);
    setCameraOn(false);
    setCamReady(false);
    setCamError('');
    setCameraKey((k) => k + 1);
    timer.current = setTimeout(() => setCameraOn(true), delay);
  }, []);

  useFocusEffect(useCallback(() => {
    busy.current = false;
    setState({ phase: 'scan' });
    loadSessions();
    startCamera(500);
    return () => {               // leaving the tab: release the camera completely
      clearTimeout(timer.current);
      setCameraOn(false);
    };
  }, [loadSessions, startCamera]));

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

  const reset = () => {
    busy.current = false;
    setState({ phase: 'scan' });
    loadSessions();
    startCamera(500);
  };

  const switchEngine = () => {
    setEngine((e) => (e === 'legacy' ? 'new' : 'legacy'));
    startCamera(500);
  };

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

  const status = camError ? `ERROR: ${camError}` : camReady ? t('scanHint') : 'Starting camera...';

  const renderCamera = () => {
    if (!cameraOn) {
      return <View style={styles.placeholder}><ActivityIndicator size="large" color="#fff" /></View>;
    }
    const common = {
      key: cameraKey,
      style: StyleSheet.absoluteFill,
      onCameraReady: () => setCamReady(true),
      onMountError: (e) => setCamError(e?.message || 'The camera could not start.'),
    };
    if (engine === 'legacy') {
      return <LegacyCamera {...common} barCodeScannerSettings={{ barCodeTypes: ['qr'] }} onBarCodeScanned={handleScan} />;
    }
    return <CameraView {...common} facing="back" barcodeScannerSettings={{ barcodeTypes: ['qr'] }} onBarcodeScanned={handleScan} />;
  };

  // Everything except the camera itself is in normal layout flow, so it stays visible even if the camera surface misbehaves.
  return (
    <View style={styles.container}>
      <View style={styles.statusBar}>
        <Text style={styles.statusText} numberOfLines={2}>{`${VERSION} - ${engine} camera - ${status}`}</Text>
      </View>
      <View style={styles.cameraArea}>
        {renderCamera()}
        <View style={styles.overlay} pointerEvents="none">
          <View style={styles.frame} />
        </View>
      </View>
      <View style={styles.controls}>
        <View style={{ flex: 1 }}><Button title="Restart camera" kind="ghost" onPress={() => startCamera(500)} /></View>
        <View style={{ width: 10 }} />
        <View style={{ flex: 1 }}><Button title="Switch engine" kind="ghost" onPress={switchEngine} /></View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  statusBar: { backgroundColor: colors.surface, paddingHorizontal: 14, paddingVertical: 8, borderBottomWidth: 1, borderBottomColor: colors.border },
  statusText: { color: colors.text, fontSize: 13 },
  cameraArea: { flex: 1, backgroundColor: '#000', overflow: 'hidden' },
  placeholder: { ...StyleSheet.absoluteFillObject, justifyContent: 'center', alignItems: 'center' },
  overlay: { ...StyleSheet.absoluteFillObject, alignItems: 'center', justifyContent: 'center' },
  frame: { width: 250, height: 250, borderRadius: 24, borderWidth: 4, borderColor: '#ffffffcc' },
  controls: { flexDirection: 'row', padding: 12, backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.border },
  center: { flexGrow: 1, flex: 1, alignItems: 'center', justifyContent: 'center', padding: 28, backgroundColor: colors.bg },
  hint: { color: colors.muted, textAlign: 'center', marginTop: 10, fontSize: 16 },
  icon: { fontSize: 72, fontWeight: '800' },
  resultTitle: { fontSize: 22, fontWeight: '800', color: colors.text, textAlign: 'center' },
});
