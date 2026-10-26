import { useMutation } from "@tanstack/react-query";
import { apiClient } from "@/lib/apiClient";
import { useAuthStore } from "@/store/authStore";
import type { UserRole } from "@/types/enums";

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  userId: number;
  role: UserRole;
  displayName: string;
}

export async function sendOtp(phoneNumber: string) {
  const { data } = await apiClient.post<{ message: string }>("/Auth/SendOtp", { phoneNumber });
  return data.message;
}

export async function verifyOtp(phoneNumber: string, code: string) {
  const { data } = await apiClient.post<LoginResponse>("/Auth/VerifyOtp", { phoneNumber, code });
  return data;
}

export async function logout() {
  const refreshToken = useAuthStore.getState().session?.refreshToken;
  if (!refreshToken) return;
  await apiClient.post("/Auth/Logout", { refreshToken }).catch(() => {});
}

export function useSendOtp() {
  return useMutation({ mutationFn: sendOtp });
}

export function useVerifyOtp() {
  return useMutation({ mutationFn: (v: { phoneNumber: string; code: string }) => verifyOtp(v.phoneNumber, v.code) });
}
