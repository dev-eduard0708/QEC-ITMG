import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import ar from '@/i18n/ar.json'
import en from '@/i18n/en.json'

export const LANGUAGE_STORAGE_KEY = 'qec-itmg.language'
export const supportedLanguages = ['en', 'ar'] as const
export type AppLanguage = (typeof supportedLanguages)[number]

export function isAppLanguage(value: string | null | undefined): value is AppLanguage {
  return value === 'en' || value === 'ar'
}

export function getStoredLanguage(): AppLanguage {
  const stored = localStorage.getItem(LANGUAGE_STORAGE_KEY)
  return isAppLanguage(stored) ? stored : 'en'
}

export function applyDocumentLanguage(language: AppLanguage) {
  const root = document.documentElement
  root.lang = language
  root.dir = language === 'ar' ? 'rtl' : 'ltr'
}

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    ar: { translation: ar },
  },
  lng: getStoredLanguage(),
  fallbackLng: 'en',
  interpolation: {
    escapeValue: false,
  },
})

applyDocumentLanguage(getStoredLanguage())

i18n.on('languageChanged', (language) => {
  if (isAppLanguage(language)) {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, language)
    applyDocumentLanguage(language)
  }
})

export default i18n
