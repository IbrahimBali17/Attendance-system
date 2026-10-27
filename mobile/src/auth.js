import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { api, setSessionExpiredHandler, tokenStore } from './api';
import { registerForPush } from './push';

const AuthContext = createContext(null);
export const useAuth = () => useContext(AuthContext);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    setSessionExpiredHandler(() => setUser(null));
    (async () => {
      const auth = await tokenStore.read();
      if (auth?.user?.role === 'Student') {
        setUser(auth.user);
        registerForPush();
      }
      setReady(true);
    })();
  }, []);

  const login = useCallback(async (login, password) => {
    const { data } = await api.post('/api/auth/login', { login, password });
    if (data.user.role !== 'Student') throw new Error('This app is for students. Professors use the web dashboard.');
    await tokenStore.write(data);
    setUser(data.user);
    registerForPush();
  }, []);

  const logout = useCallback(async () => {
    await tokenStore.write(null);
    setUser(null);
  }, []);

  return <AuthContext.Provider value={{ user, ready, login, logout }}>{children}</AuthContext.Provider>;
}
