import { Badge, Button, Card, CardTitle, ErrorState, Spinner } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { faDate } from "@/lib/fa";
import { channelLabels } from "@/types/enums";
import { usePropertyChannels, useSyncChannels } from "@/features/admin-properties/api";

/** وضعیت پیام آگهی در تلگرام و بله، با همگام‌سازی دوباره */
export function ChannelPanel({ propertyId }: { propertyId: number }) {
  const posts = usePropertyChannels(propertyId);
  const sync = useSyncChannels(propertyId);

  return (
    <Card>
      <CardTitle>کانال‌ها</CardTitle>
      {posts.isLoading && <Spinner />}
      {posts.isError && <ErrorState message={extractErrorMessage(posts.error)} />}
      {posts.data && posts.data.length === 0 && (
        <p className="text-sm text-slate-600">پستی در کانال‌ها نیست. با انتشار آگهی، پست ساخته می‌شود (اگر ربات‌ها تنظیم شده باشند).</p>
      )}
      <ul className="space-y-2">
        {posts.data?.map((c) => (
          <li key={c.id} className="rounded-lg border border-brand-200 p-3 text-sm">
            <div className="flex items-center gap-2">
              <span className="font-bold">{channelLabels[c.channel]}</span>
              {c.error ? <Badge color="red">ناموفق</Badge> : c.externalMessageId ? <Badge color="green">ارسال شد</Badge> : <Badge>در انتظار</Badge>}
              <span className="ms-auto text-xs text-slate-500">{faDate(c.lastSyncedAt ?? c.createdAt)}</span>
            </div>
            {c.error && <p className="mt-1 break-words text-xs text-red-700" dir="auto">{c.error}</p>}
          </li>
        ))}
      </ul>
      {sync.isError && <div className="mt-2"><ErrorState message={extractErrorMessage(sync.error)} /></div>}
      <Button variant="secondary" className="mt-3" loading={sync.isPending} onClick={() => sync.mutate()}>
        همگام‌سازی دوباره
      </Button>
    </Card>
  );
}
