import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/apiClient";
import { compact } from "@/features/listing/api";
import type { PagedResult, PageQuery } from "@/types/common";
import { leadStatusOrdinal, type LeadStatus } from "@/types/enums";

export interface LeadItem {
  id: number;
  propertyId: number | null;
  propertyTitle: string | null;
  propertySlug: string | null;
  fullName: string;
  phoneNumber: string;
  message: string | null;
  status: LeadStatus;
  adminNote: string | null;
  createdAt: string;
  handledAt: string | null;
}

export interface LeadStat {
  status: LeadStatus;
  count: number;
}

export interface LeadFilter extends PageQuery {
  status?: LeadStatus;
  propertyId?: number;
}

const keys = {
  list: (f: LeadFilter) => ["leads", f] as const,
  stats: ["leads", "stats"] as const,
};

export function useSubmitLead() {
  return useMutation({
    mutationFn: async (input: { propertyId?: number; fullName: string; phoneNumber: string; message?: string }) =>
      (await apiClient.post<number>("/Leads", input)).data,
  });
}

export function useLeads(filter: LeadFilter) {
  return useQuery({
    queryKey: keys.list(filter),
    queryFn: async () => (await apiClient.get<PagedResult<LeadItem>>("/Admin/Leads", { params: compact(filter) })).data,
  });
}

export function useLeadStats() {
  return useQuery({
    queryKey: keys.stats,
    queryFn: async () => (await apiClient.get<LeadStat[]>("/Admin/Leads/Stats")).data,
  });
}

export function useChangeLeadStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status, note }: { id: number; status: LeadStatus; note?: string }) =>
      apiClient.put(`/Admin/Leads/${id}/Status`, { status: leadStatusOrdinal[status], note }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["leads"] }),
  });
}
