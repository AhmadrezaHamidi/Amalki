import { useRef, useState } from "react";
import { Button, Card, CardTitle, ErrorState } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { faDigits } from "@/lib/fa";
import type { PhotoItem } from "@/features/listing/api";
import { usePhotoActions, useUploadPhoto } from "@/features/admin-properties/api";

const MAX_PHOTOS = 20;
const MAX_BYTES = 5 * 1024 * 1024;
const ACCEPT = ".jpg,.jpeg,.png,.webp";

/** آپلود، ترتیب، عکس اصلی، alt و حذف — اولین عکس عکس اصلی و عکس پست کانال است */
export function PhotoManager({ propertyId, photos }: { propertyId: number; photos: PhotoItem[] }) {
  const upload = useUploadPhoto(propertyId);
  const actions = usePhotoActions(propertyId);
  const fileRef = useRef<HTMLInputElement>(null);
  const [localError, setLocalError] = useState<string | null>(null);
  const busy = upload.isPending || Object.values(actions).some((m) => m.isPending);
  const error = localError ?? [upload, ...Object.values(actions)].map((m) => (m.isError ? extractErrorMessage(m.error) : null)).find(Boolean);

  async function onFiles(files: FileList | null) {
    setLocalError(null);
    if (!files) return;
    const list = Array.from(files);
    if (photos.length + list.length > MAX_PHOTOS) return setLocalError(`هر آگهی حداکثر ${faDigits(MAX_PHOTOS)} عکس دارد.`);
    const big = list.find((f) => f.size > MAX_BYTES);
    if (big) return setLocalError(`«${big.name}» بزرگ‌تر از ۵ مگابایت است.`);
    // یکی‌یکی: شناسهٔ عکس در API از max+1 است و ارسال هم‌زمان تداخل می‌کند
    for (const file of list) await upload.mutateAsync({ file }).catch(() => undefined);
    if (fileRef.current) fileRef.current.value = "";
  }

  function move(index: number, delta: number) {
    const ids = photos.map((p) => p.id);
    const [moved] = ids.splice(index, 1);
    ids.splice(index + delta, 0, moved);
    actions.reorder.mutate(ids);
  }

  return (
    <Card>
      <CardTitle>
        عکس‌ها ({faDigits(photos.length)} از {faDigits(MAX_PHOTOS)})
      </CardTitle>
      <p className="mb-4 text-sm text-slate-600">JPG، PNG یا WebP تا ۵ مگابایت. عکس اول، عکس اصلی آگهی و پست کانال است.</p>

      {error && <div className="mb-3"><ErrorState message={error} /></div>}

      <ul className="grid grid-cols-[repeat(auto-fill,minmax(180px,1fr))] gap-3">
        {photos.map((ph, i) => (
          <li key={ph.id} className="overflow-hidden rounded-xl border border-brand-200 bg-white">
            <div className="relative">
              <img src={ph.url} alt={ph.alt} className="aspect-[4/3] w-full object-cover" />
              {i === 0 && <span className="absolute start-2 top-2 rounded-full bg-brand-700 px-2 text-xs text-white">عکس اصلی</span>}
            </div>
            <div className="space-y-2 p-2">
              <input
                aria-label={`متن جایگزین عکس ${faDigits(i + 1)}`}
                defaultValue={ph.alt}
                className="w-full rounded-lg border border-brand-200 px-2 py-1 text-xs"
                onBlur={(e) => e.target.value !== ph.alt && actions.alt.mutate({ photoId: ph.id, alt: e.target.value })}
              />
              <div className="flex flex-wrap gap-1 text-xs">
                <button type="button" disabled={busy || i === 0} onClick={() => move(i, -1)} className="rounded px-2 py-1 hover:bg-brand-50 disabled:opacity-40" aria-label="جابه‌جایی به قبل">→</button>
                <button type="button" disabled={busy || i === photos.length - 1} onClick={() => move(i, 1)} className="rounded px-2 py-1 hover:bg-brand-50 disabled:opacity-40" aria-label="جابه‌جایی به بعد">←</button>
                {i > 0 && (
                  <button type="button" disabled={busy} onClick={() => actions.cover.mutate(ph.id)} className="rounded px-2 py-1 text-brand-700 hover:bg-brand-50">
                    عکس اصلی
                  </button>
                )}
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => window.confirm("این عکس حذف شود؟") && actions.remove.mutate(ph.id)}
                  className="ms-auto rounded px-2 py-1 text-red-700 hover:bg-red-50"
                >
                  حذف
                </button>
              </div>
            </div>
          </li>
        ))}
      </ul>

      <div className="mt-4">
        <input ref={fileRef} type="file" accept={ACCEPT} multiple hidden onChange={(e) => onFiles(e.target.files)} />
        <Button type="button" variant="secondary" loading={upload.isPending} disabled={photos.length >= MAX_PHOTOS} onClick={() => fileRef.current?.click()}>
          افزودن عکس
        </Button>
      </div>
    </Card>
  );
}
