import { Link } from "react-router-dom";
import { Card, CardTitle, ErrorState, Spinner } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { faDigits } from "@/lib/fa";
import { channelLabels, leadStatusLabels, statusLabels, type ListingStatus } from "@/types/enums";
import { useAdminProperties, useFailedChannelPosts } from "@/features/admin-properties/api";
import { useLeadStats } from "@/features/leads/api";

function StatTile({ label, value, to }: { label: string; value: number | undefined; to: string }) {
  return (
    <Link to={to} className="rounded-2xl border border-brand-200 bg-white p-4 transition hover:shadow-soft">
      <div className="text-sm text-slate-600">{label}</div>
      <div className="mt-1 text-3xl font-extrabold text-brand-800">{value === undefined ? "…" : faDigits(value)}</div>
    </Link>
  );
}

const useCount = (status: ListingStatus) => useAdminProperties({ status, page: 1, pageSize: 1 }).data?.totalCount;

export function AdminDashboardPage() {
  const published = useCount("Published");
  const drafts = useCount("Draft");
  const stats = useLeadStats();
  const failed = useFailedChannelPosts();
  const newLeads = stats.data?.find((s) => s.status === "New")?.count ?? (stats.data ? 0 : undefined);

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">داشبورد</h1>
      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <StatTile label={statusLabels.Published} value={published} to="/admin/properties" />
        <StatTile label={statusLabels.Draft} value={drafts} to="/admin/properties" />
        <StatTile label={`درخواست تماس ${leadStatusLabels.New}`} value={newLeads} to="/admin/leads" />
        <StatTile label="خطای کانال" value={failed.data?.length} to="/admin" />
      </div>

      <Card>
        <CardTitle>پست‌های ناموفق کانال‌ها</CardTitle>
        {failed.isLoading && <Spinner />}
        {failed.isError && <ErrorState message={extractErrorMessage(failed.error)} />}
        {failed.data?.length === 0 && <p className="text-sm text-slate-600">همهٔ پست‌ها همگام‌اند.</p>}
        <ul className="divide-y divide-brand-100">
          {failed.data?.map((c) => (
            <li key={c.id} className="flex flex-wrap items-center gap-2 py-2 text-sm">
              <Link to={`/admin/properties/${c.propertyId}`} className="font-medium text-brand-800">
                {c.propertyTitle}
              </Link>
              <span className="text-slate-500">· {channelLabels[c.channel]}</span>
              <span className="w-full break-words text-xs text-red-700" dir="auto">{c.error}</span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}
