import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Badge, Button, Card, DataTable } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { faDate, faDigits } from "@/lib/fa";
import { options, statusColor, statusLabels, type ListingStatus } from "@/types/enums";
import { useAdminProperties, usePropertyAction, type AdminPropertyFilter, type PropertyAction } from "@/features/admin-properties/api";
import { propertyPath } from "@/features/listing/paths";

const confirmText: Partial<Record<PropertyAction, string>> = {
  Close: "آگهی بسته شود؟ از سایت و کانال‌ها برداشته می‌شود.",
  Delete: "آگهی، عکس‌ها و پیام کانال‌هایش حذف شود؟ این کار برگشت ندارد.",
};

export function AdminPropertiesPage() {
  const navigate = useNavigate();
  const [filter, setFilter] = useState<AdminPropertyFilter>({ page: 1, pageSize: 20 });
  const [search, setSearch] = useState("");
  const list = useAdminProperties(filter);
  const action = usePropertyAction();

  function run(id: number, a: PropertyAction) {
    if (confirmText[a] && !window.confirm(confirmText[a])) return;
    action.mutate({ id, action: a });
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">آگهی‌ها</h1>
        <Button onClick={() => navigate("/admin/properties/new")}>+ آگهی جدید</Button>
      </div>

      <Card className="space-y-4">
        <form
          className="flex flex-wrap gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            setFilter((f) => ({ ...f, page: 1, search: search.trim() || undefined }));
          }}
        >
          <input
            aria-label="جستجو"
            placeholder="جستجو در عنوان، محله یا نشانی"
            className="min-h-[40px] flex-1 rounded-lg border border-brand-200 px-3 text-sm"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select
            aria-label="وضعیت"
            className="min-h-[40px] rounded-lg border border-brand-200 bg-white px-3 text-sm"
            value={filter.status ?? ""}
            onChange={(e) => setFilter((f) => ({ ...f, page: 1, status: (e.target.value || undefined) as ListingStatus | undefined }))}
          >
            <option value="">همهٔ وضعیت‌ها</option>
            {options(statusLabels).map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
          <Button type="submit" variant="secondary">
            جستجو
          </Button>
        </form>

        {action.isError && <p className="text-sm text-red-700">{extractErrorMessage(action.error)}</p>}

        <DataTable
          data={list.data}
          isLoading={list.isLoading}
          error={list.isError ? extractErrorMessage(list.error) : undefined}
          onPageChange={(page) => setFilter((f) => ({ ...f, page }))}
          columns={[
            {
              header: "عنوان",
              cell: (p) => (
                <Link to={`/admin/properties/${p.id}`} className="font-medium text-brand-800 hover:underline">
                  {p.title}
                </Link>
              ),
            },
            { header: "محل", cell: (p) => `${p.city}، ${p.district}` },
            { header: "معامله", cell: (p) => (p.deal === "Sale" ? "فروش" : "اجاره") },
            {
              header: "وضعیت",
              cell: (p) => (
                <span className="flex flex-wrap gap-1">
                  <Badge color={statusColor[p.status]}>{statusLabels[p.status]}</Badge>
                  {p.hasChannelError && <Badge color="red">خطای کانال</Badge>}
                </span>
              ),
            },
            { header: "عکس", cell: (p) => faDigits(p.photoCount) },
            { header: "درخواست جدید", cell: (p) => (p.newLeadCount ? <Badge color="amber">{faDigits(p.newLeadCount)}</Badge> : "—") },
            { header: "انتشار", cell: (p) => faDate(p.publishedAt) },
            {
              header: "عملیات",
              cell: (p) => (
                <span className="flex gap-1">
                  {p.status !== "Published" && (
                    <Button variant="ghost" onClick={() => run(p.id, "Publish")}>
                      انتشار
                    </Button>
                  )}
                  {p.status === "Published" && (
                    <>
                      <Button variant="ghost" onClick={() => run(p.id, "Unpublish")}>
                        لغو انتشار
                      </Button>
                      <a href={propertyPath(p.slug)} target="_blank" rel="noreferrer" className="px-2 py-2 text-sm text-brand-700">
                        مشاهده
                      </a>
                    </>
                  )}
                  {p.status !== "Closed" && (
                    <Button variant="ghost" onClick={() => run(p.id, "Close")}>
                      بستن
                    </Button>
                  )}
                  <Button variant="ghost" className="text-red-700" onClick={() => run(p.id, "Delete")}>
                    حذف
                  </Button>
                </span>
              ),
            },
          ]}
        />
      </Card>
    </div>
  );
}
