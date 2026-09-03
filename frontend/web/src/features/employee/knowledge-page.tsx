import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Search } from 'lucide-react'
import { kbApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'

export function KnowledgePage() {
  const { t } = useTranslation()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')

  const query = useQuery({
    queryKey: ['kb', 'published', search],
    queryFn: () => kbApi.listPublished(search || undefined),
  })

  const articles = useMemo(() => query.data ?? [], [query.data])

  return (
    <div className="space-y-6">
      <PageHeader title={t('kb.title')} description={t('kb.description')} />

      <div className="flex gap-2">
        <Input
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          placeholder={t('kb.searchPlaceholder')}
          onKeyDown={(event) => {
            if (event.key === 'Enter') setSearch(searchInput.trim())
          }}
        />
        <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
          <Search className="h-4 w-4" />
        </Button>
      </div>

      {query.isLoading ? (
        <Skeleton className="h-40 w-full" />
      ) : articles.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('kb.empty')}</p>
      ) : (
        <ul className="space-y-3">
          {articles.map((article) => (
            <li key={article.id} className="rounded-md border border-border px-4 py-3">
              <Link
                to={`/employee/knowledge/${article.slug}`}
                className="text-base font-medium text-primary underline-offset-2 hover:underline"
              >
                {article.title}
              </Link>
              {article.summary ? (
                <p className="mt-1 text-sm text-muted-foreground">{article.summary}</p>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
