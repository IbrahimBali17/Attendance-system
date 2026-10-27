import { createContext, useContext, useState, useCallback } from 'react';
import { api, storage } from './api.js';

const AuthContext = createContext(null);
export const useAuth = () => useContext(AuthContext);

export function AuthProvider({ children }) {
  const [auth, setAuth] = useState(() => storage.read());

  const login = useCallback(async (login, password) => {
    const { data } = await api.post('/api/auth/login', { login, password });
    if (data.user.role !== 'Professor') throw new Error('This dashboard is for professors only.');
    storage.write(data);
    setAuth(data);
  }, []);

  const logout = useCallback(() => {
    storage.write(null);
    setAuth(null);
  }, []);

  return <AuthContext.Provider value={{ user: auth?.user ?? null, login, logout }}>{children}</AuthContext.Provider>;
}
