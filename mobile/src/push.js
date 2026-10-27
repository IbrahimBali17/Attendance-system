import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import Constants from 'expo-constants';
import { Platform } from 'react-native';
import { api } from './api';

Notifications.setNotificationHandler({
  handleNotification: async () => ({ shouldShowAlert: true, shouldPlaySound: true, shouldSetBadge: false }),
});

/** Registers this device for push. Prefers the native FCM token; falls back to an Expo push token. Never throws. */
export async function registerForPush() {
  try {
    if (!Device.isDevice) return;
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync('default', { name: 'Attendance alerts', importance: Notifications.AndroidImportance.HIGH });
    }
    let { status } = await Notifications.getPermissionsAsync();
    if (status !== 'granted') ({ status } = await Notifications.requestPermissionsAsync());
    if (status !== 'granted') return;

    let token = null;
    try {
      token = (await Notifications.getDevicePushTokenAsync()).data;            // FCM token (needs google-services.json in the build)
    } catch {
      const projectId = Constants.expoConfig?.extra?.eas?.projectId;
      if (projectId && !projectId.startsWith('REPLACE')) token = (await Notifications.getExpoPushTokenAsync({ projectId })).data;
    }
    if (token) await api.post('/api/auth/register-device', { fcmToken: String(token) });
  } catch {
    // Push is optional - ignore failures (e.g. Expo Go has no FCM support).
  }
}
