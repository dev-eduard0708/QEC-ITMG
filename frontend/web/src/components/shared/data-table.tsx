import {
  flexRender,
  getCoreRowModel,
  getPaginationRowModel,
  useReactTable,
  type ColumnDef,
  type Row,
} from '@tanstack/react-table'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'

export type DataTableProps<TData> = {
  columns: ColumnDef<TData, unknown>[]
  data: TData[]
  isLoading?: boolean
  emptyMessage?: string
  onRowClick?: (row: TData) => void
  pageSize?: number
  className?: string
  getRowId?: (row: TData) => string
}

export function DataTable<TData>({
  columns,
  data,
  isLoading = false,
  emptyMessage,
  onRowClick,
  pageSize = 25,
  className,
  getRowId,
}: DataTableProps<TData>) {
  const { t } = useTranslation()
  const enablePagination = data.length > pageSize

  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: enablePagination ? getPaginationRowModel() : undefined,
    getRowId,
    initialState: {
      pagination: {
        pageSize,
      },
    },
  })

  if (isLoading) {
    return (
      <div className={cn('space-y-2 rounded-lg border border-border bg-card p-4', className)}>
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
      </div>
    )
  }

  const rows = table.getRowModel().rows

  return (
    <div className={cn('space-y-3', className)}>
      <div className="rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableHead key={header.id} style={{ width: header.getSize() === 150 ? undefined : header.getSize() }}>
                    {header.isPlaceholder
                      ? null
                      : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {rows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={columns.length} className="h-24 text-center text-muted-foreground">
                  {emptyMessage ?? t('shared.table.empty')}
                </TableCell>
              </TableRow>
            ) : (
              rows.map((row) => (
                <DataTableRow key={row.id} row={row} onRowClick={onRowClick} />
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {enablePagination ? (
        <div className="flex items-center justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={!table.getCanPreviousPage()}
            onClick={() => table.previousPage()}
          >
            {t('shared.table.previous')}
          </Button>
          <span className="text-sm text-muted-foreground">
            {t('shared.table.page', {
              page: table.getState().pagination.pageIndex + 1,
              pages: table.getPageCount(),
            })}
          </span>
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={!table.getCanNextPage()}
            onClick={() => table.nextPage()}
          >
            {t('shared.table.next')}
          </Button>
        </div>
      ) : null}
    </div>
  )
}

function DataTableRow<TData>({
  row,
  onRowClick,
}: {
  row: Row<TData>
  onRowClick?: (row: TData) => void
}) {
  return (
    <TableRow
      className={onRowClick ? 'cursor-pointer' : undefined}
      onClick={onRowClick ? () => onRowClick(row.original) : undefined}
    >
      {row.getVisibleCells().map((cell) => (
        <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
      ))}
    </TableRow>
  )
}
