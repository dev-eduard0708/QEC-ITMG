import { useMemo, useState } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AlertCircle, ChevronDown, Languages, Moon, ShieldAlert, Sun, Monitor } from 'lucide-react'
import { isAppLanguage, type AppLanguage } from '@/i18n'
import { useTheme } from '@/app/theme-provider'
import type { ThemeOption } from '@/app/theme'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { ApiError, apiFetch } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { cn } from '@/lib/utils'

type QuickLoginKind = 'admin' | 'employee'

function sanitizeLocalReturnUrl(candidate: string | null | undefined): string {
  // Default "/" lets RootWorkspaceRedirect send Employees to /employee and IT users to /it.
  if (!candidate || !candidate.trim()) return '/'
  const value = candidate.trim().split('#')[0]?.split('?')[0] ?? ''
  if (!value || value[0] !== '/') return '/'
  if (value.length > 1 && (value[1] === '/' || value[1] === '\\')) return '/'
  if (value.includes('\\') || value.includes('://')) return '/'
  if (value.startsWith('/login') || value.startsWith('/break-glass')) return '/'
  return value
}

function GoogleMark({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path
        fill="#4285F4"
        d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
      />
      <path
        fill="#34A853"
        d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
      />
      <path
        fill="#FBBC05"
        d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l3.66-2.84z"
      />
      <path
        fill="#EA4335"
        d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
      />
    </svg>
  )
}

export function LoginPage() {
  const { t, i18n } = useTranslation()
  const { theme, setTheme } = useTheme()
  const { refresh } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const language: AppLanguage = isAppLanguage(i18n.language) ? i18n.language : 'en'

  const [busyKind, setBusyKind] = useState<QuickLoginKind | null>(null)
  const [quickLoginError, setQuickLoginError] = useState<string | null>(null)
  const [googleBusy, setGoogleBusy] = useState(false)
  const [devOpen, setDevOpen] = useState(false)
  const [errorDismissed, setErrorDismissed] = useState(false)

  const authErrorCode = searchParams.get('authError')
  const showAuthError = Boolean(authErrorCode) && !errorDismissed

  const returnUrl = useMemo(() => {
    const fromState = (location.state as { from?: string } | null)?.from
    return sanitizeLocalReturnUrl(searchParams.get('returnUrl') ?? fromState ?? '/')
  }, [location.state, searchParams])

  const googleLoginHref = `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`

  function authErrorMessage(code: string | null): string {
    if (code === 'remote') return t('login.error.remote')
    return t('login.error.generic')
  }

  function dismissAuthError() {
    setErrorDismissed(true)
    const next = new URLSearchParams(searchParams)
    next.delete('authError')
    setSearchParams(next, { replace: true })
  }

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
    <div className="relative min-h-svh overflow-x-hidden bg-background text-foreground">
      <div
        className="pointer-events-none absolute inset-0 opacity-[0.55] dark:opacity-40"
        aria-hidden
        style={{
          backgroundImage:
            'radial-gradient(ellipse 80% 50% at 0% 0%, hsl(var(--primary) / 0.14), transparent 55%), radial-gradient(ellipse 60% 40% at 100% 100%, hsl(var(--primary) / 0.08), transparent 50%)',
        }}
      />

      <header className="relative z-20 flex items-center justify-end gap-2 px-4 py-3 sm:px-6">
        <label className="sr-only" htmlFor="login-theme">
          {t('shell.theme')}
        </label>
        <div className="flex items-center rounded-lg border border-border/80 bg-card/80 p-0.5 shadow-sm backdrop-blur-sm">
          {(
            [
              { value: 'light', icon: Sun, label: t('shell.theme.light') },
              { value: 'dark', icon: Moon, label: t('shell.theme.dark') },
              { value: 'system', icon: Monitor, label: t('shell.theme.system') },
            ] as const
          ).map((option) => {
            const Icon = option.icon
            const selected = theme === option.value
            return (
              <button
                key={option.value}
                type="button"
                id={option.value === 'light' ? 'login-theme' : undefined}
                className={cn(
                  'inline-flex h-8 min-w-8 items-center justify-center rounded-md px-2 text-muted-foreground transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  selected && 'bg-accent text-accent-foreground',
                )}
                aria-label={option.label}
                aria-pressed={selected}
                onClick={() => setTheme(option.value as ThemeOption)}
              >
                <Icon className="h-3.5 w-3.5" aria-hidden />
              </button>
            )
          })}
        </div>

        <label className="sr-only" htmlFor="login-language">
          {t('shell.language')}
        </label>
        <div className="flex items-center gap-1.5 rounded-lg border border-border/80 bg-card/80 px-2 shadow-sm backdrop-blur-sm">
          <Languages className="h-3.5 w-3.5 text-muted-foreground" aria-hidden />
          <select
            id="login-language"
            className="h-8 bg-transparent pe-1 text-sm text-foreground focus-visible:outline-none"
            value={language}
            onChange={(event) => {
              void i18n.changeLanguage(event.target.value)
            }}
            aria-label={t('shell.language')}
          >
            <option value="en">{t('shell.language.en')}</option>
            <option value="ar">{t('shell.language.ar')}</option>
          </select>
        </div>
      </header>

      <main className="relative z-10 mx-auto flex min-h-[calc(100svh-3.5rem)] w-full max-w-6xl flex-col lg:flex-row lg:items-stretch">
        {/* Brand panel */}
        <section
          className="relative flex flex-col justify-center px-6 pb-6 pt-2 sm:px-10 lg:w-[48%] lg:px-12 lg:py-16"
          aria-labelledby="login-brand-heading"
        >
          <div
            className="pointer-events-none absolute inset-6 hidden rounded-3xl border border-border/40 bg-gradient-to-br from-primary/[0.07] via-transparent to-transparent lg:block dark:from-primary/[0.12]"
            aria-hidden
          />
          <div
            className="pointer-events-none absolute inset-6 hidden opacity-[0.35] [background-image:linear-gradient(hsl(var(--border)/0.55)_1px,transparent_1px),linear-gradient(90deg,hsl(var(--border)/0.55)_1px,transparent_1px)] [background-size:28px_28px] [mask-image:radial-gradient(ellipse_at_center,black_35%,transparent_75%)] lg:block"
            aria-hidden
          />

          <div className="relative space-y-5 lg:max-w-md">
            <div className="flex items-center gap-3">
              <img
                src="/qec-mark.svg"
                alt=""
                className="h-12 w-12 shrink-0 rounded-xl shadow-sm"
                width={48}
                height={48}
              />
              <div className="min-w-0">
                <p className="text-[0.7rem] font-semibold uppercase tracking-[0.2em] text-muted-foreground">
                  {t('brand.organization')}
                </p>
                <p className="truncate text-xs text-muted-foreground">{t('brand.website')}</p>
              </div>
            </div>
            <div className="space-y-2">
              <h1
                id="login-brand-heading"
                className="text-balance text-3xl font-semibold tracking-tight sm:text-4xl lg:text-[2.65rem] lg:leading-tight"
              >
                {t('brand.name')}
              </h1>
              <p className="text-base font-medium text-primary sm:text-lg">{t('brand.product')}</p>
            </div>
            <p className="max-w-md text-pretty text-sm leading-relaxed text-muted-foreground sm:text-[0.95rem]">
              {t('login.brandMessage')}
            </p>
            <p className="max-w-md text-pretty text-sm leading-relaxed text-muted-foreground">
              {t('brand.tagline')}
            </p>
            <ul className="hidden gap-2 pt-2 text-sm text-muted-foreground lg:grid">
              <li className="flex items-start gap-2">
                <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-primary" aria-hidden />
                {t('login.brandPoint1')}
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-primary" aria-hidden />
                {t('login.brandPoint2')}
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-primary" aria-hidden />
                {t('login.brandPoint3')}
              </li>
            </ul>
          </div>
        </section>

        {/* Auth panel */}
        <section
          className="flex flex-1 items-start justify-center px-4 pb-10 sm:px-8 lg:items-center lg:px-10 lg:pb-16"
          aria-labelledby="login-welcome-heading"
        >
          <div className="w-full max-w-[26rem] space-y-5 rounded-2xl border border-border/80 bg-card p-6 shadow-sm sm:p-8">
            <div className="space-y-3">
              <div className="inline-flex items-center gap-2 rounded-full border border-border/70 bg-muted/40 px-2.5 py-1 text-xs font-medium text-muted-foreground">
                <img src="/qec-mark.svg" alt="" className="h-5 w-5 rounded" width={20} height={20} />
                <span className="font-semibold text-foreground">{t('brand.name')}</span>
                <span aria-hidden>·</span>
                <span>{t('login.secureAccessBadge')}</span>
              </div>
              <div className="space-y-1.5">
                <h2 id="login-welcome-heading" className="text-xl font-semibold tracking-tight sm:text-2xl">
                  {t('login.welcome')}
                </h2>
                <p className="text-sm leading-relaxed text-muted-foreground">{t('login.description')}</p>
              </div>
            </div>

            {showAuthError ? (
              <div
                role="alert"
                className="rounded-xl border border-destructive/30 bg-destructive/5 p-3.5"
              >
                <div className="flex gap-3">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" aria-hidden />
                  <div className="min-w-0 flex-1 space-y-2">
                    <p className="text-sm font-medium text-destructive">{t('login.error.title')}</p>
                    <p className="text-sm text-muted-foreground">{authErrorMessage(authErrorCode)}</p>
                    <div className="flex flex-wrap gap-2 pt-1">
                      <Button asChild size="sm" className={cn(googleBusy && 'pointer-events-none opacity-70')}>
                        <a
                          href={googleLoginHref}
                          onClick={(event) => {
                            if (googleBusy) {
                              event.preventDefault()
                              return
                            }
                            setGoogleBusy(true)
                          }}
                          aria-busy={googleBusy}
                          aria-disabled={googleBusy}
                        >
                          {googleBusy ? t('login.googleBusy') : t('login.error.retry')}
                        </a>
                      </Button>
                      <Button type="button" size="sm" variant="ghost" onClick={dismissAuthError}>
                        {t('login.error.dismiss')}
                      </Button>
                    </div>
                  </div>
                </div>
              </div>
            ) : null}

            <div className="space-y-3">
              <Button
                asChild
                size="lg"
                className={cn(
                  'h-12 w-full text-base font-semibold shadow-sm',
                  googleBusy && 'pointer-events-none opacity-70',
                )}
              >
                <a
                  href={googleLoginHref}
                  onClick={(event) => {
                    if (googleBusy) {
                      event.preventDefault()
                      return
                    }
                    setGoogleBusy(true)
                  }}
                  aria-busy={googleBusy}
                  aria-disabled={googleBusy}
                  aria-label={t('login.google')}
                >
                  {googleBusy ? (
                    t('login.googleBusy')
                  ) : (
                    <>
                      <GoogleMark className="h-5 w-5 shrink-0" />
                      {t('login.google')}
                    </>
                  )}
                </a>
              </Button>
              <p className="text-center text-xs text-muted-foreground">{t('login.googleHint')}</p>
            </div>

            <div className="rounded-xl border border-border/70 bg-muted/30 p-3.5">
              <p className="text-sm font-medium text-foreground">{t('login.firstTime.title')}</p>
              <p className="mt-1.5 text-sm leading-relaxed text-muted-foreground">
                {t('login.firstTime.body')}
              </p>
            </div>

            {import.meta.env.DEV ? (
              <div className="space-y-3 rounded-xl border border-dashed border-border/80 bg-muted/20 p-3">
                <button
                  type="button"
                  className="flex w-full items-center justify-between gap-2 text-start focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  aria-expanded={devOpen}
                  onClick={() => setDevOpen((open) => !open)}
                >
                  <span className="flex items-center gap-2">
                    <Badge variant="outline">{t('login.dev.badge')}</Badge>
                    <span className="text-xs font-medium text-muted-foreground">
                      {t('login.dev.title')}
                    </span>
                  </span>
                  <ChevronDown
                    className={cn(
                      'h-4 w-4 text-muted-foreground transition-transform motion-reduce:transition-none',
                      devOpen && 'rotate-180',
                    )}
                    aria-hidden
                  />
                </button>
                {devOpen ? (
                  <div className="space-y-2 pt-1">
                    <p className="text-xs text-muted-foreground">{t('login.dev.note')}</p>
                    <Separator />
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-9 w-full"
                      disabled={busyKind !== null}
                      onClick={() => void quickLogin('admin')}
                    >
                      {busyKind === 'admin' ? t('login.quickLogin.busy') : t('login.quickLogin.admin')}
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-9 w-full"
                      disabled={busyKind !== null}
                      onClick={() => void quickLogin('employee')}
                    >
                      {busyKind === 'employee'
                        ? t('login.quickLogin.busy')
                        : t('login.quickLogin.employee')}
                    </Button>
                    {quickLoginError ? (
                      <p className="text-xs text-destructive" role="alert">
                        {quickLoginError}
                      </p>
                    ) : null}
                  </div>
                ) : null}
              </div>
            ) : null}

            <div className="pt-1 text-center">
              <Link
                to="/break-glass"
                className="inline-flex items-center gap-1.5 text-xs text-muted-foreground underline-offset-4 transition-colors hover:text-foreground hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <ShieldAlert className="h-3.5 w-3.5" aria-hidden />
                {t('login.emergency')}
              </Link>
            </div>
          </div>
        </section>
      </main>
    </div>
  )
}
