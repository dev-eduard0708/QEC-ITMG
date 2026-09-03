import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { kbApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'

export function KnowledgeArticlePage() {
  const { slug = '' } = useParams()
  const { t } = useTranslation()
  const query = useQuery({
    queryKey: ['kb', 'published', 'detail', slug],
    queryFn: () => kbApi.getPublished(slug),
    enabled: Boolean(slug),
  })

  if (query.isLoading) {
    return <Skeleton className="h-40 w-full" />
  }

  const article = query.data
  if (!article) {
    return <p className="text-sm text-muted-foreground">{t('kb.notFound')}</p>
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={article.title}
        description={article.summary ?? ''}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/knowledge">{t('kb.back')}</Link>
          </Button>
        }
      />
      <article className="whitespace-pre-wrap text-sm leading-relaxed text-foreground">{article.body}</article>
    </div>
  )
}
