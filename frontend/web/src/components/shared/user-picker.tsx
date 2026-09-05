import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, ChevronDown, X } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

export type PickableUser = {
  id: string
  displayName: string
  upn: string
}

function matches(user: PickableUser, query: string): boolean {
  if (!query) return true
  const needle = query.toLowerCase()
  return (
    user.displayName.toLowerCase().includes(needle) || user.upn.toLowerCase().includes(needle)
  )
}

function useDismissOnOutside(open: boolean, onClose: () => void) {
  const ref = useRef<HTMLDivElement | null>(null)
  useEffect(() => {
    if (!open) return
    function handlePointer(event: MouseEvent) {
      if (ref.current && !ref.current.contains(event.target as Node)) onClose()
    }
    function handleKey(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }
    document.addEventListener('mousedown', handlePointer)
    document.addEventListener('keydown', handleKey)
    return () => {
      document.removeEventListener('mousedown', handlePointer)
      document.removeEventListener('keydown', handleKey)
    }
  }, [open, onClose])
  return ref
}

function UserLine({ user }: { user: PickableUser }) {
  return (
    <span className="flex min-w-0 flex-col text-start">
      <span className="truncate text-sm leading-tight">{user.displayName}</span>
      <span className="truncate text-xs leading-tight text-muted-foreground">{user.upn}</span>
    </span>
  )
}

export function UserPicker({
  users,
  value,
  onChange,
  disabled,
  placeholder,
  allowClear = true,
  className,
  id,
}: {
  users: PickableUser[]
  value: string | null
  onChange: (userId: string | null) => void
  disabled?: boolean
  placeholder?: string
  allowClear?: boolean
  className?: string
  id?: string
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const containerRef = useDismissOnOutside(open, () => setOpen(false))
  const generatedId = useId()
  const triggerId = id ?? generatedId

  const selected = useMemo(() => users.find((user) => user.id === value) ?? null, [users, value])
  const filtered = useMemo(() => users.filter((user) => matches(user, query.trim())), [users, query])

  return (
    <div ref={containerRef} className={cn('relative', className)}>
      <button
        id={triggerId}
        type="button"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={() => setOpen((prev) => !prev)}
        className="flex h-auto min-h-9 w-full items-center justify-between gap-2 rounded-md border border-input bg-background px-3 py-1.5 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
      >
        {selected ? (
          <UserLine user={selected} />
        ) : (
          <span className="truncate text-muted-foreground">
            {placeholder ?? t('policyMgmt.picker.placeholder')}
          </span>
        )}
        <ChevronDown className="h-4 w-4 shrink-0 opacity-50" aria-hidden />
      </button>

      {open ? (
        <div className="absolute z-50 mt-1 w-full overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-md">
          <div className="border-b p-2">
            <Input
              autoFocus
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t('policyMgmt.picker.search')}
              className="h-8"
            />
          </div>
          <ul role="listbox" className="max-h-60 overflow-y-auto p-1">
            {allowClear ? (
              <li>
                <button
                  type="button"
                  className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                  onClick={() => {
                    onChange(null)
                    setOpen(false)
                  }}
                >
                  <X className="h-3.5 w-3.5" aria-hidden />
                  {t('policyMgmt.picker.unassigned')}
                </button>
              </li>
            ) : null}
            {filtered.length === 0 ? (
              <li className="px-2 py-3 text-center text-sm text-muted-foreground">
                {t('policyMgmt.picker.empty')}
              </li>
            ) : (
              filtered.map((user) => (
                <li key={user.id}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={user.id === value}
                    className="flex w-full items-center justify-between gap-2 rounded-sm px-2 py-1.5 hover:bg-accent hover:text-accent-foreground"
                    onClick={() => {
                      onChange(user.id)
                      setOpen(false)
                    }}
                  >
                    <UserLine user={user} />
                    {user.id === value ? <Check className="h-4 w-4 shrink-0" aria-hidden /> : null}
                  </button>
                </li>
              ))
            )}
          </ul>
        </div>
      ) : null}
    </div>
  )
}

export function UserMultiPicker({
  users,
  value,
  onChange,
  disabled,
  className,
}: {
  users: PickableUser[]
  value: string[]
  onChange: (userIds: string[]) => void
  disabled?: boolean
  className?: string
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const containerRef = useDismissOnOutside(open, () => setOpen(false))

  const filtered = useMemo(() => users.filter((user) => matches(user, query.trim())), [users, query])
  const selectedSet = useMemo(() => new Set(value), [value])

  function toggle(userId: string) {
    onChange(selectedSet.has(userId) ? value.filter((item) => item !== userId) : [...value, userId])
  }

  return (
    <div ref={containerRef} className={cn('relative', className)}>
      <button
        type="button"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={() => setOpen((prev) => !prev)}
        className="flex h-9 w-full items-center justify-between gap-2 rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
      >
        <span className={cn('truncate', value.length === 0 && 'text-muted-foreground')}>
          {value.length === 0
            ? t('policyMgmt.picker.selectEmployees')
            : t('policyMgmt.picker.selectedCount', { total: value.length })}
        </span>
        <ChevronDown className="h-4 w-4 shrink-0 opacity-50" aria-hidden />
      </button>

      {open ? (
        <div className="absolute z-50 mt-1 w-full overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-md">
          <div className="border-b p-2">
            <Input
              autoFocus
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t('policyMgmt.picker.search')}
              className="h-8"
            />
          </div>
          <ul role="listbox" aria-multiselectable className="max-h-60 overflow-y-auto p-1">
            {filtered.length === 0 ? (
              <li className="px-2 py-3 text-center text-sm text-muted-foreground">
                {t('policyMgmt.picker.empty')}
              </li>
            ) : (
              filtered.map((user) => (
                <li key={user.id}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={selectedSet.has(user.id)}
                    className="flex w-full items-center justify-between gap-2 rounded-sm px-2 py-1.5 hover:bg-accent hover:text-accent-foreground"
                    onClick={() => toggle(user.id)}
                  >
                    <UserLine user={user} />
                    {selectedSet.has(user.id) ? (
                      <Check className="h-4 w-4 shrink-0" aria-hidden />
                    ) : null}
                  </button>
                </li>
              ))
            )}
          </ul>
          {value.length > 0 ? (
            <div className="border-t p-1">
              <button
                type="button"
                className="w-full rounded-sm px-2 py-1.5 text-start text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                onClick={() => onChange([])}
              >
                {t('policyMgmt.picker.clearSelection')}
              </button>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}
