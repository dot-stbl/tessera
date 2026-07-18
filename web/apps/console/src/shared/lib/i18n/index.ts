import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

import en from './locales/en/common.json';

export const SUPPORTED_LANGUAGES = ['en'] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = 'en';

/**
 * i18n setup. react-i18next + browser language detector.
 *
 * MVP: English only. Russian and other locales added in stretch.
 * Language is detected from localStorage ('tessera-lang') → navigator.language,
 * with fallback to DEFAULT_LANGUAGE ('en').
 *
 * PreferencesProvider syncs the language pref; this module is initialized once
 * at app bootstrap in main.tsx.
 */
void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
    },
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: [...SUPPORTED_LANGUAGES],
    interpolation: {
      escapeValue: false,
    },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'tessera-lang',
      caches: ['localStorage'],
    },
  });

export default i18n;