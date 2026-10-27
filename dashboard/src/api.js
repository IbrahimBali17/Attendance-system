import axios from 'axios';

export const API_URL = (import.meta.env.VITE_API_URL || 'http://localhost:8080').replace(/\/$/, '');

const KEY = 'attendance.auth';
export const storage = {
  read() {
    try { return JSON.parse(localStorage.getItem(KEY) || 'null'); } catch { return null; }
  },
  write(v) {
    try { v ? localStorage.setItem(KEY, JSON.stringify(v)) : localStorage.removeItem(KEY); } catch { /* storage unavailable */ }
  },
};

export const api = axios.create({ baseURL: API_URL });

api.interceptors.request.use((config) => {
  const auth = storage.read();
  if (auth?.accessToken) config.headers.Authorization = `Bearer ${auth.accessToken}`;
  return config;
});

let refreshing = null;
api.interceptors.response.use(
  (r) => r,
  async (error) => {
    const original = error.config;
    const auth = storage.read();
    if (error.response?.status === 401 && auth?.refreshToken && !original._retry && !original.url.includes('/auth/')) {
      original._retry = true;
      try {
        refreshing ??= axios.post(`${API_URL}/api/auth/refresh`, { refreshToken: auth.refreshToken }).finally(() => { refreshing = null; });
        const { data } = await refreshing;
        storage.write(data);
        original.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(original);
      } catch {
        storage.write(null);
        window.location.assign('/login');
      }
    }
    return Promise.reject(error);
  }
);

export const errorMessage = (e) =>
  e?.response?.data?.detail || e?.response?.data?.title || e?.message || 'Something went wrong.';

export async function downloadFile(url, fallbackName) {
  const res = await api.get(url, { responseType: 'blob' });
  const disposition = res.headers['content-disposition'] || '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const name = match ? decodeURIComponent(match[1]) : fallbackName;
  const href = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = href;
  a.download = name;
  a.click();
  URL.revokeObjectURL(href);
}
