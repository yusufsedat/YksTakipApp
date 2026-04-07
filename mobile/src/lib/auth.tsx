import * as SecureStore from 'expo-secure-store';
import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';

import { apiGet, apiPost, setApiAuthHooks } from './api';
import type { LoginResponse, MeResponse, User } from '../types/user';

const TOKEN_KEY = 'auth_token';
const REFRESH_TOKEN_KEY = 'refresh_token';

type AuthContextValue = {
  user: User | null;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const getToken = useCallback(() => SecureStore.getItemAsync(TOKEN_KEY), []);
  const getRefreshToken = useCallback(() => SecureStore.getItemAsync(REFRESH_TOKEN_KEY), []);
  const clearSession = useCallback(async () => {
    await SecureStore.deleteItemAsync(TOKEN_KEY);
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
    setUser(null);
  }, []);

  useEffect(() => {
    setApiAuthHooks({
      getAccessToken: getToken,
      getRefreshToken,
      refreshTokens: async (refreshToken) => {
        const res = await apiPost<{ token: string; refreshToken: string; user?: User }>('/users/refresh-token', { refreshToken });
        if (res.user) setUser(res.user);
        return { token: res.token, refreshToken: res.refreshToken };
      },
      persistTokens: async (token, refreshToken) => {
        await SecureStore.setItemAsync(TOKEN_KEY, token);
        await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, refreshToken);
      },
      clearSession,
    });
  }, [clearSession, getRefreshToken, getToken]);

  const refreshUser = useCallback(async () => {
    const token = await SecureStore.getItemAsync(TOKEN_KEY);
    if (!token) {
      setUser(null);
      return;
    }
    try {
      const me = await apiGet<MeResponse>('/users/me');
      setUser({ id: me.id, name: me.name, email: me.email, role: me.role });
    } catch {
      await clearSession();
    }
  }, [clearSession]);

  useEffect(() => {
    (async () => {
      try {
        await refreshUser();
      } finally {
        setIsLoading(false);
      }
    })();
  }, [refreshUser]);

  const login = useCallback(async (email: string, password: string) => {
    const res = await apiPost<LoginResponse>('/users/login', { email, password });
    await SecureStore.setItemAsync(TOKEN_KEY, res.token);
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, res.refreshToken);
    setUser(res.user);
  }, []);

  const register = useCallback(async (name: string, email: string, password: string) => {
    await apiPost('/users/register', { name, email, password });
    await login(email, password);
  }, [login]);

  const logout = useCallback(async () => {
    await clearSession();
  }, [clearSession]);

  const value = useMemo(
    () => ({ user, isLoading, login, register, logout }),
    [user, isLoading, login, register, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
