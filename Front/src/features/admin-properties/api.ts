import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { apiClient } from "@/lib/apiClient";
import { compact, type PropertyDetail } from "@/features/listing/api";
import type { PagedResult, PageQuery } from "@/types/common";
import { dealOrdinal, kindOrdinal, type Channel, type DealType, type ListingStatus, type PropertyKind } from "@/types/enums";

export interface PropertyAdminListItem {
  id: number;
  slug: string;
  title: string;
  deal: DealType;
  status: ListingStatus;
  city: string;
  district: string;
  photoCount: number;
  newLeadCount: number;
  hasChannelError: boolean;
  createdAt: string;
  publishedAt: string | null;
}

export interface ChannelPostItem {
  id: number;
  propertyId: number;
  propertyTitle: string;
  channel: Channel;
  externalMessageId: string | null;
  hasPhoto: boolean;
  error: string | null;
  createdAt: string;
  lastSyncedAt: string | null;
}

export interface AdminPropertyFilter extends PageQuery {
  status?: ListingStatus;
  search?: string;
}

/** ورودی فرم ساخت/ویرایش — enum ها پیش از ارسال به عدد تبدیل می‌شوند */
export interface PropertyInput {
  title: string;
  description: string;
  deal: DealType;
  kind: PropertyKind;
  price?: number;
  deposit?: number;
  monthlyRent?: number;
  areaM2: number;
  bedrooms?: number;
  floor?: number;
  parking?: number;
  yearBuilt?: number;
  city: string;
  district: string;
  address?: string;
  latitude?: number;
  longitude?: number;
}

const toBody = (v: PropertyInput) => ({
  ...v,
  deal: dealOrdinal[v.deal],
  kind: kindOrdinal[v.kind],
  // فروش فقط قیمت و اجاره فقط رهن/اجاره می‌فرستد؛ دامین هم مبلغ نامربوط را دور می‌ریزد
  price: v.deal === "Sale" ? v.price : undefined,
  deposit: v.deal === "Rent" ? v.deposit : undefined,
  monthlyRent: v.deal === "Rent" ? v.monthlyRent : undefined,
});

export const adminKeys = {
  list: (f: AdminPropertyFilter) => ["admin-properties", f] as const,
  detail: (id: number) => ["admin-properties", id] as const,
  channels: (id: number) => ["admin-properties", id, "channels"] as const,
  failed: ["channels", "failed"] as const,
};

export function useAdminProperties(filter: AdminPropertyFilter) {
  return useQuery({
    queryKey: adminKeys.list(filter),
    queryFn: async () =>
      (await apiClient.get<PagedResult<PropertyAdminListItem>>("/Admin/Properties", { params: compact(filter) })).data,
    placeholderData: (prev) => prev,
  });
}

export function useAdminProperty(id: number) {
  return useQuery({
    queryKey: adminKeys.detail(id),
    queryFn: async () => {
      try {
        return (await apiClient.get<PropertyDetail>(`/Admin/Properties/${id}`)).data;
      } catch (e) {
        if (axios.isAxiosError(e) && e.response?.status === 404) return null;
        throw e;
      }
    },
    enabled: id > 0,
  });
}

export function usePropertyChannels(id: number) {
  return useQuery({
    queryKey: adminKeys.channels(id),
    queryFn: async () => (await apiClient.get<ChannelPostItem[]>(`/Admin/Channels/Properties/${id}`)).data,
    enabled: id > 0,
  });
}

export function useFailedChannelPosts() {
  return useQuery({
    queryKey: adminKeys.failed,
    queryFn: async () => (await apiClient.get<ChannelPostItem[]>("/Admin/Channels/Failed")).data,
  });
}

/** هر تغییر آگهی ممکن است پست کانال‌ها و لیست را هم عوض کند */
function useInvalidate(id?: number) {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: ["admin-properties"] });
    qc.invalidateQueries({ queryKey: ["channels"] });
    qc.invalidateQueries({ queryKey: ["properties"] });
    if (id) qc.invalidateQueries({ queryKey: adminKeys.detail(id) });
  };
}

export function useCreateProperty() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async (v: PropertyInput) => (await apiClient.post<{ id: number; slug: string }>("/Admin/Properties", toBody(v))).data,
    onSuccess: invalidate,
  });
}

export function useUpdateProperty(id: number) {
  const invalidate = useInvalidate(id);
  return useMutation({
    mutationFn: (v: PropertyInput) => apiClient.put(`/Admin/Properties/${id}`, toBody(v)),
    onSuccess: invalidate,
  });
}

export type PropertyAction = "Publish" | "Unpublish" | "Close" | "Delete";

export function usePropertyAction() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, action }: { id: number; action: PropertyAction }) =>
      action === "Delete" ? apiClient.delete(`/Admin/Properties/${id}`) : apiClient.put(`/Admin/Properties/${id}/${action}`),
    onSuccess: invalidate,
  });
}

export function useSyncChannels(id: number) {
  const invalidate = useInvalidate(id);
  return useMutation({
    mutationFn: () => apiClient.post(`/Admin/Channels/Properties/${id}/Sync`),
    onSuccess: invalidate,
  });
}

// ── عکس‌ها ────────────────────────────────────────────────────────────

export function useUploadPhoto(id: number) {
  const invalidate = useInvalidate(id);
  return useMutation({
    mutationFn: async ({ file, alt }: { file: File; alt?: string }) => {
      const form = new FormData();
      form.append("file", file);
      if (alt) form.append("alt", alt);
      return (await apiClient.post<number>(`/Admin/Properties/${id}/Photos`, form)).data;
    },
    onSuccess: invalidate,
  });
}

export function usePhotoActions(id: number) {
  const invalidate = useInvalidate(id);
  const opts = { onSuccess: invalidate };
  return {
    remove: useMutation({ mutationFn: (photoId: number) => apiClient.delete(`/Admin/Properties/${id}/Photos/${photoId}`), ...opts }),
    cover: useMutation({ mutationFn: (photoId: number) => apiClient.put(`/Admin/Properties/${id}/Photos/${photoId}/Cover`), ...opts }),
    reorder: useMutation({ mutationFn: (photoIds: number[]) => apiClient.put(`/Admin/Properties/${id}/Photos/Order`, { photoIds }), ...opts }),
    alt: useMutation({
      mutationFn: ({ photoId, alt }: { photoId: number; alt: string }) => apiClient.put(`/Admin/Properties/${id}/Photos/${photoId}/Alt`, { alt }),
      ...opts,
    }),
  };
}
