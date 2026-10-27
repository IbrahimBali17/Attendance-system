import axios from 'axios';
import * as SecureStore from 'expo-secure-store';

export const API_URL = (process.env.EXPO_PUBLIC_API_URL || 'http://10.0.2.2:8080').replace(/\/$/, '');
const KEY = 'attendance.auth';

export const tokenStore = {
  async read() {
    try { return JSON.parse((await SecureStore.getItemAsync(KEY)) || 'null'); } catch { return null; }
  },
  async write(v) {
    try {
      if (v) await SecureStore.setItemAsync(KEY, JSON.stringify(v));
      else await SecureStore.deleteItemAsync(KEY);
    } catch { /* secure storage unavailable */ }
  },
};

export const api = axios.create({ baseURL: API_URL, timeout: 15000 });

let onSessionExpired = () => {};
export const setSessionExpiredHandler = (fn) => { onSessionExpired = fn; };

api.interceptors.request.use(async (config) => {
  const auth = await tokenStore.read();
  if (auth?.accessToken) config.headers.Authorization = `Bearer ${auth.accessToken}`;
  return config;
});

let refreshing = null;
api.interceptors.response.use(
  (r) => r,
  async (error) => {
    const original = error.config;
    const auth = await tokenStore.read();
    if (error.response?.status === 401 && auth?.refreshToken && original && !original._retry && !original.url?.includes('/auth/')) {
      original._retry = true;
      try {
        refreshing ??= axios.post(`${API_URL}/api/auth/refresh`, { refreshToken: auth.refreshToken }).finally(() => { refreshing = null; });
        const { data } = await refreshing;
        await tokenStore.write(data);
        original.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(original);
      } catch {
        await tokenStore.write(null);
        onSessionExpired();
      }
    }
    return Promise.reject(error);
  }
);

export const errorMessage = (e) => {
  if (e?.response?.data?.detail) return e.response.data.detail;
  if (e?.response?.data?.title) return e.response.data.title;
  if (e?.code === 'ECONNABORTED' || e?.message === 'Network Error') return 'Cannot reach the server. Check your connection.';
  return e?.message || 'Something went wrong.';
};
