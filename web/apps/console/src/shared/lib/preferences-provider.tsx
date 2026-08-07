import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import i18n from '@/shared/lib/i18n';

/**
 * User visual preferences. The single source of truth for theme + font size.
 * Persisted in localStorage and applied to the document as CSS variables /
 * class list on every change.
 *
 * Theme precedence: explicit `theme` value wins; if it's 'system' we follow
 * `prefers-color-scheme` via the inline script in main.tsx (no flash) — this
 * provider just records the user's intent.
 *
 * Language: also persisted here (NOT only in i18next's own 'tessera-lang' key).
 * This is the single source of truth — i18n is synced via i18n.changeLanguage()
 * on every change.
 *
 * Accent colors: MVP uses default ink only. Accent picker is stretch.
 */
export type Theme = 'light' | 'dark' | 'system';
export type FontSize = 'small' | 'medium' | 'large';
export type Language = 'en' | 'ru';

export interface Preferences {
  theme: Theme;
  fontSize: FontSize;
  language: Language;
}

/**
 * Dark is the default, not `system`.
 *
 * Tessera is designed dark-first, the way every observability tool an operator
 * already has open is: Grafana, Datadog, Sentry, Honeycomb. That is not fashion —
 * on a dark ground the coloured data carries, and span bars, log severities and
 * latency all read as signal. On white the same palette washes out and the
 * hairlines between four hundred rows turn the screen into a spreadsheet.
 *
 * Light remains fully supported and switchable; it is the secondary target.
 */
export const PREFERENCES_DEFAULT: Preferences = {
  theme: 'dark',
  fontSize: 'medium',
  language: 'en',
};

const STORAGE_KEY = 'tessera-preferences';

const FONT_SIZE_VALUES: Record<FontSize, string> = {
  small: '14px',
  medium: '16px',
  large: '18px',
};

interface PreferencesContextValue {
  preferences: Preferences;
  setPreferences: (next: Preferences) => void;
  update: <K extends keyof Preferences>(key: K, value: Preferences[K]) => void;
  reset: () => void;
}

const PreferencesContext = createContext<PreferencesContextValue | null>(null);

function loadFromStorage(): Preferences {
  if (typeof window === 'undefined') return PREFERENCES_DEFAULT;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return PREFERENCES_DEFAULT;
    const parsed = JSON.parse(raw) as Partial<Preferences>;
    const theme: Theme =
      parsed.theme === 'light' || parsed.theme === 'dark' || parsed.theme === 'system'
        ? parsed.theme
        : PREFERENCES_DEFAULT.theme;
    const fontSize: FontSize =
      parsed.fontSize && parsed.fontSize in FONT_SIZE_VALUES
        ? (parsed.fontSize as FontSize)
        : PREFERENCES_DEFAULT.fontSize;
    const language: Language =
      parsed.language === 'en' || parsed.language === 'ru'
        ? parsed.language
        : PREFERENCES_DEFAULT.language;
    return { theme, fontSize, language };
  } catch {
    return PREFERENCES_DEFAULT;
  }
}

function applyToDocument(prefs: Preferences) {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;

  // Theme — class list (the inline script in main.tsx already applied the
  // resolved class before mount, so this just keeps it in sync if the user
  // toggles at runtime).
  root.classList.remove('light', 'dark');
  if (prefs.theme === 'system') {
    const mql = window.matchMedia('(prefers-color-scheme: dark)');
    root.classList.add(mql.matches ? 'dark' : 'light');
  } else {
    root.classList.add(prefs.theme);
  }

  // Font size — base scale. All Tailwind `text-*` utilities resolve through
  // rem (1rem = font-size on <html>), so changing this scales the entire UI.
  root.style.fontSize = FONT_SIZE_VALUES[prefs.fontSize];

  // Language — keep i18n in sync with the pref.
  if (i18n.language !== prefs.language) {
    void i18n.changeLanguage(prefs.language);
  }
}

export interface PreferencesProviderProps {
  children: ReactNode;
  defaultPreferences?: Partial<Preferences>;
  storageKey?: string;
}

export function PreferencesProvider({
  children,
  defaultPreferences,
  storageKey = STORAGE_KEY,
}: PreferencesProviderProps) {
  const [preferences, setPreferencesState] = useState<Preferences>(() => {
    const loaded = loadFromStorage();
    return { ...PREFERENCES_DEFAULT, ...defaultPreferences, ...loaded };
  });

  // Persist + apply to document on every change.
  useEffect(() => {
    if (typeof window === 'undefined') return;
    try {
      window.localStorage.setItem(storageKey, JSON.stringify(preferences));
    } catch {
      // ignore (private mode, quota exceeded)
    }
    applyToDocument(preferences);
  }, [preferences, storageKey]);

  // React to system theme changes while in 'system' mode.
  useEffect(() => {
    if (preferences.theme !== 'system') return;
    const mql = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = () => applyToDocument(preferences);
    mql.addEventListener('change', handler);
    return () => mql.removeEventListener('change', handler);
  }, [preferences]);

  const setPreferences = useCallback((next: Preferences) => {
    setPreferencesState(next);
  }, []);

  const update = useCallback(
    <K extends keyof Preferences>(key: K, value: Preferences[K]) => {
      setPreferencesState((prev) => ({ ...prev, [key]: value }));
    },
    [],
  );

  const reset = useCallback(() => {
    setPreferencesState({ ...PREFERENCES_DEFAULT, ...defaultPreferences });
  }, [defaultPreferences]);

  const value: PreferencesContextValue = {
    preferences,
    setPreferences,
    update,
    reset,
  };

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>;
}

export function usePreferences(): PreferencesContextValue {
  const ctx = useContext(PreferencesContext);
  if (ctx === null) {
    throw new Error('usePreferences must be used within a PreferencesProvider');
  }
  return ctx;
}

// Re-export the legacy `useTheme` hook for backwards compat with theme-toggle.
export function useTheme() {
  const { preferences, update, reset } = usePreferences();
  return {
    theme: preferences.theme,
    setTheme: (next: Preferences['theme']) => update('theme', next),
    reset,
  };
}