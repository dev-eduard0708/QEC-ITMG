import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { cn } from '@/lib/utils'

function initialsFromName(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase()
  return `${parts[0]![0] ?? ''}${parts[1]![0] ?? ''}`.toUpperCase()
}

const sizeClass: Record<'sm' | 'md' | 'lg' | 'xl', string> = {
  sm: 'h-8 w-8 text-[10px]',
  md: 'h-9 w-9 text-xs',
  lg: 'h-12 w-12 text-sm',
  xl: 'h-20 w-20 text-lg',
}

export type UserAvatarProps = {
  profileImageUrl?: string | null
  displayName: string
  size?: 'sm' | 'md' | 'lg' | 'xl'
  className?: string
}

/** Consistent Google-image / initials avatar used across hierarchy and profile UI. */
export function UserAvatar({
  profileImageUrl,
  displayName,
  size = 'md',
  className,
}: UserAvatarProps) {
  return (
    <Avatar className={cn(sizeClass[size], className)}>
      {profileImageUrl ? (
        <AvatarImage src={profileImageUrl} alt={displayName} />
      ) : null}
      <AvatarFallback>{initialsFromName(displayName)}</AvatarFallback>
    </Avatar>
  )
}
