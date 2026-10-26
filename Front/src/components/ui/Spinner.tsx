export function Spinner({ label = "در حال بارگذاری..." }: { label?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12 text-gray-500">
      <span className="h-8 w-8 animate-spin rounded-full border-2 border-brand-600 border-t-transparent" />
      <span className="text-sm">{label}</span>
    </div>
  );
}

export function ErrorState({ message }: { message: string }) {
  return (
    <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{message}</div>
  );
}

export function EmptyState({ message = "موردی یافت نشد" }: { message?: string }) {
  return <div className="py-12 text-center text-sm text-gray-400">{message}</div>;
}
