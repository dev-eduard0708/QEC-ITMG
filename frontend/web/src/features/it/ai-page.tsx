import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { aiApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'

type Mode = 'ask' | 'classify' | 'kb' | 'summarize' | 'reports'

const areaClass =
  'min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

export function AiAssistantPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const [mode, setMode] = useState<Mode>('ask')
  const [question, setQuestion] = useState('')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [kbQuery, setKbQuery] = useState('')
  const [recordType, setRecordType] = useState('ticket')
  const [recordId, setRecordId] = useState('')
  const [result, setResult] = useState<unknown>(null)

  const readiness = useQuery({
    queryKey: ['ai', 'readiness'],
    queryFn: () => aiApi.readiness(),
    enabled: can('ai.admin'),
  })

  const ask = useMutation({
    mutationFn: () => aiApi.ask(question),
    onSuccess: setResult,
  })
  const classify = useMutation({
    mutationFn: () => aiApi.suggestClassification({ title, description }),
    onSuccess: setResult,
  })
  const kb = useMutation({
    mutationFn: () => aiApi.suggestKb({ query: kbQuery }),
    onSuccess: setResult,
  })
  const summarize = useMutation({
    mutationFn: () => aiApi.summarize({ recordType, recordId }),
    onSuccess: setResult,
  })
  const reports = useMutation({
    mutationFn: () => aiApi.reportQuery(question),
    onSuccess: setResult,
  })

  if (!can('ai.use') && !can('ai.admin')) {
    return <p className="text-sm text-muted-foreground">{t('ai.noAccess')}</p>
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('ai.title')} description={t('ai.description')} />
      <p className="text-sm text-muted-foreground">{t('ai.advisory')}</p>

      {can('ai.admin') && readiness.data ? (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">{t('ai.readiness')}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-3 text-sm">
            <Badge variant="secondary">{readiness.data.status}</Badge>
            <span>
              {t('ai.enabled')}: {readiness.data.enabled ? t('ai.yes') : t('ai.no')}
            </span>
            <span>
              {t('ai.configured')}: {readiness.data.configured ? t('ai.yes') : t('ai.no')}
            </span>
            <span>
              {t('ai.provider')}: {readiness.data.providerKind}
            </span>
            <span>
              {t('ai.model')}: {readiness.data.modelName || '—'}
            </span>
          </CardContent>
        </Card>
      ) : null}

      <div className="flex flex-wrap gap-2">
        {(
          [
            ['ask', 'ai.modes.ask'],
            ['classify', 'ai.modes.classify'],
            ['kb', 'ai.modes.kb'],
            ['summarize', 'ai.modes.summarize'],
            ['reports', 'ai.modes.reports'],
          ] as const
        ).map(([key, label]) => (
          <Button
            key={key}
            type="button"
            size="sm"
            variant={mode === key ? 'default' : 'outline'}
            onClick={() => {
              setMode(key)
              setResult(null)
            }}
          >
            {t(label)}
          </Button>
        ))}
      </div>

      {mode === 'ask' || mode === 'reports' ? (
        <div className="space-y-2">
          <textarea
            className={areaClass}
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder={t(mode === 'reports' ? 'ai.reportPlaceholder' : 'ai.askPlaceholder')}
            rows={4}
          />
          <Button
            type="button"
            disabled={!question.trim() || ask.isPending || reports.isPending}
            onClick={() => (mode === 'ask' ? ask.mutate() : reports.mutate())}
          >
            {t('ai.submit')}
          </Button>
        </div>
      ) : null}

      {mode === 'classify' ? (
        <div className="space-y-2">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('ai.titleField')} />
          <textarea
            className={areaClass}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder={t('ai.descriptionField')}
            rows={4}
          />
          <Button type="button" disabled={classify.isPending} onClick={() => classify.mutate()}>
            {t('ai.suggest')}
          </Button>
        </div>
      ) : null}

      {mode === 'kb' ? (
        <div className="space-y-2">
          <Input value={kbQuery} onChange={(e) => setKbQuery(e.target.value)} placeholder={t('ai.kbPlaceholder')} />
          <Button type="button" disabled={!kbQuery.trim() || kb.isPending} onClick={() => kb.mutate()}>
            {t('ai.suggest')}
          </Button>
        </div>
      ) : null}

      {mode === 'summarize' ? (
        <div className="flex flex-wrap gap-2">
          <Input value={recordType} onChange={(e) => setRecordType(e.target.value)} className="w-40" />
          <Input value={recordId} onChange={(e) => setRecordId(e.target.value)} placeholder="record GUID" className="w-80" />
          <Button
            type="button"
            disabled={!recordId.trim() || summarize.isPending}
            onClick={() => summarize.mutate()}
          >
            {t('ai.summarize')}
          </Button>
        </div>
      ) : null}

      {result ? (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">{t('ai.result')}</CardTitle>
          </CardHeader>
          <CardContent>
            <pre className="overflow-auto rounded-md bg-muted p-3 text-xs">
              {JSON.stringify(result, null, 2)}
            </pre>
          </CardContent>
        </Card>
      ) : null}
    </div>
  )
}

export function AiAdminPage() {
  return <AiAssistantPage />
}
