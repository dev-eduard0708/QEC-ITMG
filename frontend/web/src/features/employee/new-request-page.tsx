import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { ApiError, meApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useState } from 'react'

const schema = z.object({
  type: z.enum(['ServiceRequest', 'Incident']),
  title: z.string().trim().min(1),
  description: z.string().trim().min(1),
  priority: z.enum(['Low', 'Medium', 'High', 'Critical']),
  configurationItemId: z.string().optional(),
})

type FormValues = z.infer<typeof schema>

export function NewRequestPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [formError, setFormError] = useState<string | null>(null)
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      type: 'ServiceRequest',
      title: '',
      description: '',
      priority: 'Medium',
      configurationItemId: '',
    },
  })

  const createMutation = useMutation({
    mutationFn: (values: FormValues) =>
      meApi.createTicket({
        type: values.type,
        title: values.title,
        description: values.description,
        priority: values.priority,
        configurationItemId: values.configurationItemId?.trim() || null,
      }),
    onSuccess: (ticket) => {
      navigate(`/employee/requests/${ticket.id}`)
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('requests.error.generic'))
    },
  })

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageHeader
        title={t('requests.newTitle')}
        description={t('requests.newDescription')}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/requests">{t('requests.back')}</Link>
          </Button>
        }
      />

      <form
        className="space-y-4"
        onSubmit={form.handleSubmit((values) => {
          setFormError(null)
          createMutation.mutate(values)
        })}
      >
        <div className="space-y-2">
          <Label>{t('requests.fields.type')}</Label>
          <Select
            value={form.watch('type')}
            onValueChange={(value) => form.setValue('type', value as FormValues['type'])}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ServiceRequest">{t('requests.types.serviceRequest')}</SelectItem>
              <SelectItem value="Incident">{t('requests.types.incident')}</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="title">{t('requests.fields.title')}</Label>
          <Input id="title" {...form.register('title')} />
        </div>

        <div className="space-y-2">
          <Label htmlFor="description">{t('requests.fields.description')}</Label>
          <textarea
            id="description"
            className="min-h-28 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            {...form.register('description')}
          />
        </div>

        <div className="space-y-2">
          <Label>{t('requests.fields.priority')}</Label>
          <Select
            value={form.watch('priority')}
            onValueChange={(value) => form.setValue('priority', value as FormValues['priority'])}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(['Low', 'Medium', 'High', 'Critical'] as const).map((priority) => (
                <SelectItem key={priority} value={priority}>
                  {priority}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="ci">{t('requests.fields.relatedCi')}</Label>
          <Input id="ci" placeholder={t('requests.fields.relatedCiPlaceholder')} {...form.register('configurationItemId')} />
        </div>

        {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

        <Button type="submit" disabled={createMutation.isPending}>
          {t('requests.submit')}
        </Button>
      </form>
    </div>
  )
}
