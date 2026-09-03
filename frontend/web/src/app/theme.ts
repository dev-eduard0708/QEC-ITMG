export const THEME_STORAGE_KEY = 'qec-itmg.theme'
export const themeOptions = ['light', 'dark', 'system'] as const
export type ThemeOption = (typeof themeOptions)[number]
export type ResolvedTheme = 'light' | 'dark'

export function isThemeOption(value: string | null | undefined): value is ThemeOption {
  return value === 'light' || value === 'dark' || value === 'system'
}

export function getStoredTheme(): ThemeOption {
  const stored = localStorage.getItem(THEME_STORAGE_KEY)
  return isThemeOption(stored) ? stored : 'system'
}

export function getSystemTheme(): ResolvedTheme {
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function resolveTheme(theme: ThemeOption): ResolvedTheme {
  return theme === 'system' ? getSystemTheme() : theme
}

export function applyResolvedTheme(resolvedTheme: ResolvedTheme) {
  const root = document.documentElement
  root.classList.toggle('dark', resolvedTheme === 'dark')
  root.dataset.theme = resolvedTheme
}
