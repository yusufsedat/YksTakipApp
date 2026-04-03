import AsyncStorage from '@react-native-async-storage/async-storage';
import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { Appearance, useColorScheme as useSystemColorScheme } from 'react-native';

import { type ThemeColors, darkColors, lightColors } from './colors';

const STORAGE_KEY = '@yks_theme_mode';

export type ThemeMode = 'light' | 'dark' | 'system';

type ThemeContextValue = {
  mode: ThemeMode;
  resolved: 'light' | 'dark';
  colors: ThemeColors;
  setMode: (m: ThemeMode) => void;
};

const ThemeContext = createContext<ThemeContextValue | null>(null);

function resolveMode(mode: ThemeMode, system: 'light' | 'dark' | null | undefined): 'light' | 'dark' {
  if (mode === 'system') {
    return system === 'dark' ? 'dark' : 'light';
  }
  return mode;
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const systemScheme = useSystemColorScheme();
  const [mode, setModeState] = useState<ThemeMode>('system');
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const raw = await AsyncStorage.getItem(STORAGE_KEY);
        if (cancelled) return;
        if (raw === 'light' || raw === 'dark' || raw === 'system') {
          setModeState(raw);
        }
      } finally {
        if (!cancelled) setHydrated(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const setMode = useCallback((m: ThemeMode) => {
    setModeState(m);
    void AsyncStorage.setItem(STORAGE_KEY, m);
  }, []);

  const resolved = useMemo(
    () => resolveMode(mode, systemScheme),
    [mode, systemScheme]
  );

  const colors = useMemo(
    () => (resolved === 'dark' ? darkColors : lightColors),
    [resolved]
  );

  const value = useMemo<ThemeContextValue>(
    () => ({ mode, resolved, colors, setMode }),
    [mode, resolved, colors, setMode]
  );

  if (!hydrated) {
    const bootResolved = resolveMode('system', systemScheme);
    const bootColors = bootResolved === 'dark' ? darkColors : lightColors;
    return (
      <ThemeContext.Provider
        value={{ mode: 'system', resolved: bootResolved, colors: bootColors, setMode }}
      >
        {children}
      </ThemeContext.Provider>
    );
  }

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    throw new Error('useTheme must be used within ThemeProvider');
  }
  return ctx;
}
