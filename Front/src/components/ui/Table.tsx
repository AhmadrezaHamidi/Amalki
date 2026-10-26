import type { ReactNode } from "react";
import { Spinner, ErrorState, EmptyState } from "@/components/ui/Spinner";
import { Button } from "@/components/ui/Button";
import type { PagedResult } from "@/types/common";
import { faDigits } from "@/lib/fa";

interface Column<T> {
  header: string;
  cell: (row: T) => ReactNode;
}

interface Props<T> {
  columns: Column<T>[];
  data?: PagedResult<T>;
  isLoading: boolean;
  error?: string;
  onRowClick?: (row: T) => void;
  onPageChange?: (page: number) => void;
}

export function DataTable<T extends { id?: number | string }>({
  columns,
  data,
  isLoading,
  error,
  onRowClick,
  onPageChange,
}: Props<T>) {
  if (isLoading) return <Spinner />;
  if (error) return <ErrorState message={error} />;
  if (!data || data.items.length === 0) return <EmptyState />;

  return (
    <div>
      <div className="overflow-x-auto">
        <table className="w-full text-right text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-gray-500">
              {columns.map((c) => (
                <th key={c.header} className="px-3 py-2 font-medium">
                  {c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.items.map((row, i) => (
              <tr
                key={row.id ?? i}
                onClick={() => onRowClick?.(row)}
                className={onRowClick ? "cursor-pointer border-b border-gray-100 hover:bg-brand-50" : "border-b border-gray-100"}
              >
                {columns.map((c) => (
                  <td key={c.header} className="px-3 py-3">
                    {c.cell(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {onPageChange && data.totalPages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-3">
          <Button
            variant="secondary"
            disabled={!data.hasPreviousPage}
            onClick={() => onPageChange(data.page - 1)}
          >
            قبلی
          </Button>
          <span className="text-sm text-gray-500">
            صفحه {faDigits(data.page)} از {faDigits(data.totalPages)}
          </span>
          <Button variant="secondary" disabled={!data.hasNextPage} onClick={() => onPageChange(data.page + 1)}>
            بعدی
          </Button>
        </div>
      )}
    </div>
  );
}
