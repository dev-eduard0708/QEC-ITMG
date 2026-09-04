import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, changesApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

export function ChangeNewPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [type, setType] = useState('Normal')
  const [riskRating, setRiskRating] = useState('Medium')
  const [isRetrospective, setIsRetrospective] = useState(false)
  const [isPreAuthorizedStandard, setIsPreAuthorizedStandard] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const createMutation = useMutation({
    mutationFn: () =>
      changesApi.create({
        title,
        description,
        type,
        riskRating,
        isRetrospective,
        isPreAuthorizedStandard: type === 'Standard' && isPreAuthorizedStandard,
      }),
    onSuccess: (created) => navigate(`/it/changes/${created.id}`),
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('changes.error.generic'))
    },
  })

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageHeader
        title={t('changes.newTitle')}
        description={t('changes.newDescription')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/changes">{t('changes.back')}</Link>
          </Button>
        }
      />

      <form
        className="space-y-4"
        onSubmit={(e) => {
          e.preventDefault()
          setFormError(null)
          createMutation.mutate()
        }}
      >
        <div className="space-y-2">
          <Label htmlFor="title">{t('changes.fields.title')}</Label>
          <Input id="title" value={title} onChange={(e) => setTitle(e.target.value)} required />
        </div>
        <div className="space-y-2">
          <Label htmlFor="description">{t('changes.fields.description')}</Label>
          <textarea
            id="description"
            className="min-h-28 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            required
          />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label>{t('changes.fields.type')}</Label>
            <Select value={type} onValueChange={setType}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {['Standard', 'Normal', 'Emergency'].map((item) => (
                  <SelectItem key={item} value={item}>
                    {item}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>{t('changes.fields.risk')}</Label>
            <Select value={riskRating} onValueChange={setRiskRating}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {['Low', 'Medium', 'High', 'Critical'].map((item) => (
                  <SelectItem key={item} value={item}>
                    {item}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
        <label className="flex items-center gap-2 text-sm">
          <Checkbox
            checked={isRetrospective}
            onCheckedChange={(v) => setIsRetrospective(v === true)}
          />
          {t('changes.fields.retrospective')}
        </label>
        {type === 'Standard' ? (
          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={isPreAuthorizedStandard}
              onCheckedChange={(v) => setIsPreAuthorizedStandard(v === true)}
            />
            {t('changes.fields.preAuthorized')}
          </label>
        ) : null}
        {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
        <Button type="submit" disabled={createMutation.isPending}>
          {t('changes.create')}
        </Button>
      </form>
    </div>
  )
}
