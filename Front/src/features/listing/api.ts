import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import { apiClient } from "@/lib/apiClient";
import type { PagedResult, PageQuery } from "@/types/common";
import type { DealType, ListingSort, ListingStatus, PropertyKind } from "@/types/enums";

export interface PropertyCard {
  id: number;
  slug: string;
  title: string;
  deal: DealType;
  kind: PropertyKind;
  price: number | null;
  deposit: number | null;
  monthlyRent: number | null;
  areaM2: number;
  bedrooms: number | null;
  city: string;
  district: string;
  coverPhotoUrl: string | null;
  publishedAt: string | null;
}

export interface PhotoItem {
  id: number;
  url: string;
  alt: string;
  sortOrder: number;
}

export interface PropertyDetail extends Omit<PropertyCard, "coverPhotoUrl"> {
  description: string;
  status: ListingStatus;
  floor: number | null;
  parking: number | null;
  yearBuilt: number | null;
  address: string | null;
  latitude: number | null;
  longitude: number | null;
  createdAt: string;
  updatedAt: string;
  photos: PhotoItem[];
}

export interface LocationItem {
  city: string;
  district: string;
  count: number;
}

export interface SearchFilter extends PageQuery {
  deal?: DealType;
  kind?: PropertyKind;
  city?: string;
  district?: string;
  minPrice?: number;
  maxPrice?: number;
  maxRent?: number;
  minArea?: number;
  maxArea?: number;
  minBedrooms?: number;
  sort?: ListingSort;
}

export const listingKeys = {
  search: (f: SearchFilter) => ["properties", "search", f] as const,
  bySlug: (slug: string) => ["properties", "slug", slug] as const,
  similar: (id: number) => ["properties", id, "similar"] as const,
  locations: ["properties", "locations"] as const,
};

/** پارامترهای خالی فرستاده نمی‌شوند تا URL و کلید کش تمیز بمانند */
export function compact<T extends object>(obj: T): Partial<T> {
  return Object.fromEntries(Object.entries(obj).filter(([, v]) => v !== undefined && v !== "" && v !== null)) as Partial<T>;
}

export function useSearchProperties(filter: SearchFilter) {
  return useQuery({
    queryKey: listingKeys.search(filter),
    queryFn: async () =>
      (await apiClient.get<PagedResult<PropertyCard>>("/Properties", { params: compact(filter) })).data,
    placeholderData: (prev) => prev,
  });
}

/** null یعنی ۴۰۴ — آگهی پیش‌نویس، بسته یا ناموجود */
export function usePropertyBySlug(slug: string) {
  return useQuery({
    queryKey: listingKeys.bySlug(slug),
    queryFn: async () => {
      try {
        return (await apiClient.get<PropertyDetail>(`/Properties/Slug/${encodeURIComponent(slug)}`)).data;
      } catch (e) {
        if (axios.isAxiosError(e) && e.response?.status === 404) return null;
        throw e;
      }
    },
    enabled: !!slug,
  });
}

export function useSimilarProperties(id: number | undefined) {
  return useQuery({
    queryKey: listingKeys.similar(id ?? 0),
    queryFn: async () => (await apiClient.get<PropertyCard[]>(`/Properties/${id}/Similar`)).data,
    enabled: !!id,
  });
}

export function useLocations() {
  return useQuery({
    queryKey: listingKeys.locations,
    queryFn: async () => (await apiClient.get<LocationItem[]>("/Properties/Locations")).data,
    staleTime: 5 * 60_000,
  });
}
