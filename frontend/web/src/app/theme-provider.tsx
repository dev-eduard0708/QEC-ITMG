import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import {
  applyResolvedTheme,
  getStoredTheme,
  getSystemTheme,
  resolveTheme,
  THEME_STORAGE_KEY,
  type ResolvedTheme,
  type ThemeOption,
} from '@/app/theme'

type ThemeContextValue = {
  theme: ThemeOption
  resolvedTheme: ResolvedTheme
  setTheme: (theme: ThemeOption) => void
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

type ThemeProviderProps = {
  children: ReactNode
}

export function ThemeProvider({ children }: ThemeProviderProps) {
  const [theme, setThemeState] = useState<ThemeOption>(() => getStoredTheme())
  const [resolvedTheme, setResolvedTheme] = useState<ResolvedTheme>(() => resolveTheme(getStoredTheme()))

  const setTheme = useCallback((nextTheme: ThemeOption) => {
    localStorage.setItem(THEME_STORAGE_KEY, nextTheme)
    setThemeState(nextTheme)
    const resolved = resolveTheme(nextTheme)
    setResolvedTheme(resolved)
    applyResolvedTheme(resolved)
  }, [])

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)')

    const syncFromSystem = () => {
      if (theme !== 'system') {
        return
      }

      const resolved = getSystemTheme()
      setResolvedTheme(resolved)
      applyResolvedTheme(resolved)
    }

    applyResolvedTheme(resolveTheme(theme))
    media.addEventListener('change', syncFromSystem)
    return () => media.removeEventListener('change', syncFromSystem)
  }, [theme])

  const value = useMemo(
    () => ({
      theme,
      resolvedTheme,
      setTheme,
    }),
    [theme, resolvedTheme, setTheme],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}

export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) {
    throw new Error('useTheme must be used within ThemeProvider.')
  }

  return context
}
