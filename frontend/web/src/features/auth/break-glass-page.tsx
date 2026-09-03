import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Languages } from 'lucide-react'
import { ApiError, apiFetch } from '@/api/client'
import { meKeys } from '@/auth/api'
import { isAppLanguage, type AppLanguage } from '@/i18n'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

type BreakGlassResponse = {
  signedIn: boolean
  authMethod: string
  upn: string
}

export function BreakGlassPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const language: AppLanguage = isAppLanguage(i18n.language) ? i18n.language : 'en'

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await apiFetch<BreakGlassResponse>('/auth/break-glass', {
        method: 'POST',
        body: JSON.stringify({ username, password }),
      })
      await queryClient.invalidateQueries({ queryKey: meKeys.session() })
      navigate('/it', { replace: true })
    } catch (caught) {
      if (caught instanceof ApiError) {
        if (caught.status === 503) {
          setError(t('breakGlass.error.disabled'))
        } else if (caught.status === 403) {
          setError(t('breakGlass.error.userInactive'))
        } else if (caught.status === 401) {
          setError(t('breakGlass.error.invalidCredentials'))
        } else {
          setError(caught.message || t('breakGlass.error.generic'))
        }
      } else {
        setError(t('breakGlass.error.generic'))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="relative flex min-h-svh flex-col bg-[radial-gradient(ellipse_at_top,_hsl(199_40%_92%)_0%,_hsl(var(--background))_55%)] dark:bg-[radial-gradient(ellipse_at_top,_hsl(199_30%_16%)_0%,_hsl(var(--background))_55%)]">
      <div className="absolute inset-x-0 top-0 h-1 bg-destructive" aria-hidden />
      <header className="flex items-center justify-between px-6 py-4">
        <div>
          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            {t('brand.organization')}
          </div>
          <div className="text-lg font-semibold tracking-tight">{t('brand.name')}</div>
        </div>
        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          <Languages className="h-4 w-4" aria-hidden />
          <span className="sr-only">{t('shell.language')}</span>
          <select
            className="rounded-md border border-input bg-background px-2 py-1 text-foreground"
            value={language}
            onChange={(event) => {
              void i18n.changeLanguage(event.target.value)
            }}
          >
            <option value="en">{t('shell.language.en')}</option>
            <option value="ar">{t('shell.language.ar')}</option>
          </select>
        </label>
      </header>

      <main className="flex flex-1 items-center justify-center px-4 pb-16">
        <form
          onSubmit={onSubmit}
          className="w-full max-w-md space-y-6 rounded-xl border border-destructive/30 bg-card/90 p-8 shadow-sm backdrop-blur"
        >
          <div className="space-y-2">
            <div className="flex items-center gap-2 text-destructive">
              <AlertTriangle className="h-5 w-5 shrink-0" aria-hidden />
              <h1 className="text-xl font-semibold tracking-tight">{t('breakGlass.title')}</h1>
            </div>
            <p className="text-sm text-muted-foreground">{t('breakGlass.description')}</p>
          </div>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="break-glass-username">{t('breakGlass.username')}</Label>
              <Input
                id="break-glass-username"
                name="username"
                autoComplete="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="break-glass-password">{t('breakGlass.password')}</Label>
              <Input
                id="break-glass-password"
                name="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
            </div>
          </div>

          {error ? (
            <p className="text-sm text-destructive" role="alert">
              {error}
            </p>
          ) : null}

          <Button type="submit" className="w-full" disabled={submitting}>
            {submitting ? t('breakGlass.signingIn') : t('breakGlass.signIn')}
          </Button>
        </form>
      </main>
    </div>
  )
}
