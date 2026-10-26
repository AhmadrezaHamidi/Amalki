import { useState } from "react";
import { Badge, Card, DataTable } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { faDate } from "@/lib/fa";
import { leadStatusColor, leadStatusLabels, options, type LeadStatus } from "@/types/enums";
import { useChangeLeadStatus, useLeads, type LeadFilter, type LeadItem } from "@/features/leads/api";
import { propertyPath } from "@/features/listing/paths";

// هم‌قاعده با ContactLead.CanMoveTo در دامین
const nextStatuses: Record<LeadStatus, LeadStatus[]> = {
  New: ["Contacted", "Closed", "Spam"],
  Contacted: ["Closed", "Spam"],
  Closed: [],
  Spam: [],
};

function StatusAction({ lead }: { lead: LeadItem }) {
  const change = useChangeLeadStatus();
  const next = nextStatuses[lead.status];
  if (next.length === 0) return <span className="text-xs text-slate-500">{lead.adminNote ?? "—"}</span>;
  return (
    <span className="flex flex-col gap-1">
      <select
        aria-label="تغییر وضعیت"
        className="rounded-lg border border-brand-200 bg-white px-2 py-1 text-xs"
        value=""
        disabled={change.isPending}
        onChange={(e) => {
          const status = e.target.value as LeadStatus;
          if (!status) return;
          const note = window.prompt("یادداشت (اختیاری)") ?? undefined;
          change.mutate({ id: lead.id, status, note: note || undefined });
        }}
      >
        <option value="">تغییر وضعیت…</option>
        {next.map((s) => (
          <option key={s} value={s}>
            {leadStatusLabels[s]}
          </option>
        ))}
      </select>
      {change.isError && <span className="text-xs text-red-700">{extractErrorMessage(change.error)}</span>}
    </span>
  );
}

export function AdminLeadsPage() {
  const [filter, setFilter] = useState<LeadFilter>({ page: 1, pageSize: 20, status: "New" });
  const leads = useLeads(filter);

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">درخواست‌های تماس</h1>
      <Card className="space-y-4">
        <select
          aria-label="وضعیت"
          className="min-h-[40px] rounded-lg border border-brand-200 bg-white px-3 text-sm"
          value={filter.status ?? ""}
          onChange={(e) => setFilter((f) => ({ ...f, page: 1, status: (e.target.value || undefined) as LeadStatus | undefined }))}
        >
          <option value="">همه</option>
          {options(leadStatusLabels).map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>

        <DataTable
          data={leads.data}
          isLoading={leads.isLoading}
          error={leads.isError ? extractErrorMessage(leads.error) : undefined}
          onPageChange={(page) => setFilter((f) => ({ ...f, page }))}
          columns={[
            { header: "نام", cell: (l) => l.fullName },
            {
              header: "موبایل",
              cell: (l) => (
                <a href={`tel:${l.phoneNumber}`} dir="ltr" className="text-brand-700">
                  {l.phoneNumber}
                </a>
              ),
            },
            {
              header: "آگهی",
              cell: (l) =>
                l.propertySlug ? (
                  <a href={propertyPath(l.propertySlug)} target="_blank" rel="noreferrer" className="text-brand-700">
                    {l.propertyTitle}
                  </a>
                ) : (
                  "عمومی"
                ),
            },
            { header: "پیام", cell: (l) => <span className="block max-w-xs whitespace-normal text-xs">{l.message ?? "—"}</span> },
            { header: "زمان", cell: (l) => faDate(l.createdAt) },
            { header: "وضعیت", cell: (l) => <Badge color={leadStatusColor[l.status]}>{leadStatusLabels[l.status]}</Badge> },
            { header: "پیگیری", cell: (l) => <StatusAction lead={l} /> },
          ]}
        />
      </Card>
    </div>
  );
}
