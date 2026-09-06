import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { PickableUser } from '@/components/shared/user-picker'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

function matches(user: PickableUser, query: string): boolean {
  if (!query) return true
  const needle = query.toLowerCase()
  return (
    user.displayName.toLowerCase().includes(needle) || user.upn.toLowerCase().includes(needle)
  )
}

export function AccessStageDualList({
  title,
  users,
  selectedIds,
  onChange,
  disabled,
}: {
  title: string
  users: PickableUser[]
  selectedIds: string[]
  onChange: (ids: string[]) => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const [availableQuery, setAvailableQuery] = useState('')
  const [selectedQuery, setSelectedQuery] = useState('')
  const [pickedAvailable, setPickedAvailable] = useState<string[]>([])
  const [pickedSelected, setPickedSelected] = useState<string[]>([])

  const selectedSet = useMemo(() => new Set(selectedIds), [selectedIds])
  const byId = useMemo(() => new Map(users.map((user) => [user.id, user])), [users])

  const available = useMemo(
    () =>
      users
        .filter((user) => !selectedSet.has(user.id))
        .filter((user) => matches(user, availableQuery.trim())),
    [users, selectedSet, availableQuery],
  )

  const selected = useMemo(
    () =>
      selectedIds
        .map((id) => byId.get(id))
        .filter((user): user is PickableUser => Boolean(user))
        .filter((user) => matches(user, selectedQuery.trim())),
    [selectedIds, byId, selectedQuery],
  )

  function toggle(list: string[], id: string, setList: (next: string[]) => void) {
    setList(list.includes(id) ? list.filter((item) => item !== id) : [...list, id])
  }

  function addSelected() {
    if (pickedAvailable.length === 0) return
    const next = [...selectedIds]
    for (const id of pickedAvailable) {
      if (!next.includes(id)) next.push(id)
    }
    onChange(next)
    setPickedAvailable([])
  }

  function removeSelected() {
    if (pickedSelected.length === 0) return
    const remove = new Set(pickedSelected)
    onChange(selectedIds.filter((id) => !remove.has(id)))
    setPickedSelected([])
  }

  return (
    <div className="space-y-3 rounded-lg border p-4">
      <h3 className="text-sm font-medium">{title}</h3>
      <div className="grid gap-3 lg:grid-cols-[1fr_auto_1fr] lg:items-stretch">
        <DualColumn
          heading={t('access.dual.available')}
          query={availableQuery}
          onQueryChange={setAvailableQuery}
          users={available}
          picked={pickedAvailable}
          onToggle={(id) => toggle(pickedAvailable, id, setPickedAvailable)}
          disabled={disabled}
        />
        <div className="flex flex-row items-center justify-center gap-2 lg:flex-col">
          <Button type="button" size="sm" variant="secondary" disabled={disabled || pickedAvailable.length === 0} onClick={addSelected}>
            {t('access.dual.add')}
          </Button>
          <Button type="button" size="sm" variant="secondary" disabled={disabled || pickedSelected.length === 0} onClick={removeSelected}>
            {t('access.dual.remove')}
          </Button>
        </div>
        <DualColumn
          heading={t('access.dual.selected')}
          query={selectedQuery}
          onQueryChange={setSelectedQuery}
          users={selected}
          picked={pickedSelected}
          onToggle={(id) => toggle(pickedSelected, id, setPickedSelected)}
          disabled={disabled}
        />
      </div>
    </div>
  )
}

function DualColumn({
  heading,
  query,
  onQueryChange,
  users,
  picked,
  onToggle,
  disabled,
}: {
  heading: string
  query: string
  onQueryChange: (value: string) => void
  users: PickableUser[]
  picked: string[]
  onToggle: (id: string) => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const pickedSet = useMemo(() => new Set(picked), [picked])

  return (
    <div className="flex min-h-[220px] flex-col rounded-md border bg-background">
      <div className="border-b p-2">
        <p className="mb-2 text-xs font-medium text-muted-foreground">{heading}</p>
        <Input
          value={query}
          onChange={(event) => onQueryChange(event.target.value)}
          placeholder={t('access.dual.search')}
          className="h-8"
          disabled={disabled}
        />
      </div>
      <ul className="max-h-56 flex-1 overflow-y-auto p-1">
        {users.length === 0 ? (
          <li className="px-2 py-4 text-center text-sm text-muted-foreground">{t('access.dual.empty')}</li>
        ) : (
          users.map((user) => (
            <li key={user.id}>
              <button
                type="button"
                disabled={disabled}
                className={cn(
                  'flex w-full flex-col rounded-sm px-2 py-1.5 text-start hover:bg-accent hover:text-accent-foreground disabled:opacity-50',
                  pickedSet.has(user.id) && 'bg-accent text-accent-foreground',
                )}
                onClick={() => onToggle(user.id)}
              >
                <span className="truncate text-sm">{user.displayName}</span>
                <span className="truncate text-xs text-muted-foreground">{user.upn}</span>
              </button>
            </li>
          ))
        )}
      </ul>
    </div>
  )
}
