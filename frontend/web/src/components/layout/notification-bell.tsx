import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ApiError, apiFetch } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'

export type NotificationItem = {
  id: string
  type: string
  severity: string
  title: string
  message: string
  resourceType: string | null
  resourceId: string | null
  actionUrl: string | null
  createdAtUtc: string
  readAtUtc: string | null
  isRead: boolean
}

const notificationKeys = {
  list: ['me', 'notifications'] as const,
  unread: ['me', 'notifications', 'unread-count'] as const,
}

async function fetchNotifications() {
  return apiFetch<NotificationItem[]>('/api/v1/me/notifications')
}

async function fetchUnreadCount() {
  const payload = await apiFetch<{ count: number }>('/api/v1/me/notifications/unread-count')
  return payload.count
}

async function markNotificationRead(id: string) {
  return apiFetch<NotificationItem>(`/api/v1/me/notifications/${id}/read`, { method: 'POST' })
}

export function NotificationBell() {
  const { t } = useTranslation()
  const { isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)

  const listQuery = useQuery({
    queryKey: notificationKeys.list,
    queryFn: fetchNotifications,
    enabled: isAuthenticated,
    staleTime: 15_000,
    retry: false,
  })

  const unreadQuery = useQuery({
    queryKey: notificationKeys.unread,
    queryFn: fetchUnreadCount,
    enabled: isAuthenticated,
    staleTime: 15_000,
    retry: false,
  })

  const markRead = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: notificationKeys.list }),
        queryClient.invalidateQueries({ queryKey: notificationKeys.unread }),
      ])
    },
  })

  if (!isAuthenticated) {
    return null
  }

  const unread = unreadQuery.data ?? 0
  const items = listQuery.data ?? []

  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="icon"
          className="relative"
          aria-label={t('notifications.bell')}
        >
          <Bell className="h-4 w-4" />
          {unread > 0 ? (
            <Badge
              variant="warning"
              className="absolute -end-1.5 -top-1.5 h-5 min-w-5 justify-center px-1 text-[10px]"
            >
              {unread > 99 ? '99+' : unread}
            </Badge>
          ) : null}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-80 p-0">
        <div className="border-b border-border px-3 py-2">
          <div className="text-sm font-semibold">{t('notifications.title')}</div>
          <div className="text-xs text-muted-foreground">{t('notifications.subtitle')}</div>
        </div>

        {listQuery.isLoading ? (
          <div className="px-3 py-6 text-center text-sm text-muted-foreground">
            {t('notifications.loading')}
          </div>
        ) : null}

        {listQuery.isError ? (
          <div className="px-3 py-6 text-center text-sm text-destructive">
            {listQuery.error instanceof ApiError
              ? listQuery.error.message
              : t('notifications.error')}
          </div>
        ) : null}

        {!listQuery.isLoading && !listQuery.isError && items.length === 0 ? (
          <div className="px-3 py-6 text-center text-sm text-muted-foreground">
            {t('notifications.empty')}
          </div>
        ) : null}

        <div className="max-h-80 overflow-y-auto py-1">
          {items.map((item) => (
            <DropdownMenuItem
              key={item.id}
              className={cn(
                'flex cursor-pointer flex-col items-stretch gap-1 px-3 py-2',
                !item.isRead && 'bg-accent/40',
              )}
              onSelect={(event) => {
                event.preventDefault()
                if (!item.isRead) {
                  markRead.mutate(item.id)
                }
              }}
            >
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0 text-sm font-medium">{item.title}</div>
                {!item.isRead ? (
                  <Badge variant="secondary" className="shrink-0 text-[10px]">
                    {t('notifications.unread')}
                  </Badge>
                ) : null}
              </div>
              <div className="line-clamp-2 text-xs text-muted-foreground">{item.message}</div>
              {item.actionUrl ? (
                <Link
                  to={item.actionUrl}
                  className="text-xs font-medium text-primary underline-offset-2 hover:underline"
                  onClick={(event) => {
                    event.stopPropagation()
                    setOpen(false)
                    if (!item.isRead) {
                      markRead.mutate(item.id)
                    }
                  }}
                >
                  {t('notifications.open')}
                </Link>
              ) : null}
            </DropdownMenuItem>
          ))}
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
