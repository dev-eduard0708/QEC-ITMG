import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Languages } from 'lucide-react'
import { isAppLanguage, type AppLanguage } from '@/i18n'
import { useTheme } from '@/app/theme-provider'
import type { ThemeOption } from '@/app/theme'
import { Button } from '@/components/ui/button'
import { ApiError, apiFetch } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'

type QuickLoginKind = 'admin' | 'employee'

export function LoginPage() {
  const { t, i18n } = useTranslation()
  const { theme, setTheme } = useTheme()
  const { refresh } = useAuth()
  const navigate = useNavigate()
  const language: AppLanguage = isAppLanguage(i18n.language) ? i18n.language : 'en'
  const [busyKind, setBusyKind] = useState<QuickLoginKind | null>(null)
  const [quickLoginError, setQuickLoginError] = useState<string | null>(null)

  async function quickLogin(kind: QuickLoginKind) {
    if (busyKind) return
    setQuickLoginError(null)
    setBusyKind(kind)
    try {
      await apiFetch(`/auth/dev-login/${kind}`, { method: 'POST' })
      await refresh()
      navigate(kind === 'admin' ? '/it' : '/employee', { replace: true })
    } catch (caught) {
      setQuickLoginError(
        caught instanceof ApiError && caught.message
          ? caught.message
          : t('login.quickLogin.error'),
      )
    } finally {
      setBusyKind(null)
    }
  }

  return (
    <div className="relative flex min-h-svh flex-col bg-[radial-gradient(ellipse_at_top,_hsl(199_55%_88%)_0%,_hsl(var(--background))_52%)] dark:bg-[radial-gradient(ellipse_at_top,_hsl(199_35%_18%)_0%,_hsl(var(--background))_55%)]">
      <header className="flex items-center justify-between px-6 py-4">
        <div>
          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            {t('brand.organization')}
          </div>
          <div className="text-2xl font-semibold tracking-tight sm:text-3xl">{t('brand.name')}</div>
          <p className="mt-1 text-sm text-muted-foreground">{t('brand.product')}</p>
        </div>
        <div className="flex items-center gap-2">
          <select
            className="h-9 rounded-md border border-input bg-background px-2 text-sm"
            value={theme}
            onChange={(event) => setTheme(event.target.value as ThemeOption)}
            aria-label={t('shell.theme')}
          >
            <option value="light">{t('shell.theme.light')}</option>
            <option value="dark">{t('shell.theme.dark')}</option>
            <option value="system">{t('shell.theme.system')}</option>
          </select>
          <label className="flex items-center gap-2 text-sm text-muted-foreground">
            <Languages className="h-4 w-4" aria-hidden />
            <select
              className="h-9 rounded-md border border-input bg-background px-2 text-sm text-foreground"
              value={language}
              onChange={(event) => {
                void i18n.changeLanguage(event.target.value)
              }}
              aria-label={t('shell.language')}
            >
              <option value="en">{t('shell.language.en')}</option>
              <option value="ar">{t('shell.language.ar')}</option>
            </select>
          </label>
        </div>
      </header>

      <main className="flex flex-1 items-center justify-center px-4 pb-16">
        <div className="w-full max-w-md space-y-6 rounded-2xl border border-border/80 bg-card/90 p-8 shadow-sm backdrop-blur">
          <div className="space-y-2">
            <h1 className="text-xl font-semibold tracking-tight">{t('login.title')}</h1>
            <p className="text-sm text-muted-foreground">{t('login.description')}</p>
          </div>

          <Button asChild className="w-full" size="lg">
            <a href="/auth/login?returnUrl=/it">{t('login.google')}</a>
          </Button>

          {import.meta.env.DEV ? (
            <div className="space-y-2">
              <Button
                type="button"
                variant="secondary"
                className="w-full"
                disabled={busyKind !== null}
                onClick={() => void quickLogin('admin')}
              >
                {busyKind === 'admin' ? t('login.quickLogin.busy') : t('login.quickLogin.admin')}
              </Button>
              <Button
                type="button"
                variant="secondary"
                className="w-full"
                disabled={busyKind !== null}
                onClick={() => void quickLogin('employee')}
              >
                {busyKind === 'employee' ? t('login.quickLogin.busy') : t('login.quickLogin.employee')}
              </Button>
              {quickLoginError ? <p className="text-sm text-destructive">{quickLoginError}</p> : null}
            </div>
          ) : null}

          <p className="text-center text-sm text-muted-foreground">
            <Link to="/break-glass" className="underline underline-offset-4 hover:text-foreground">
              {t('login.emergency')}
            </Link>
          </p>
        </div>
      </main>
    </div>
  )
}
