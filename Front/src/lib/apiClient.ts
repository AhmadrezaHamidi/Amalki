import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { useAuthStore } from "@/store/authStore";
import type { ValidationProblem } from "@/types/common";
import { API_BASE_URL } from "@/lib/config";

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
});

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().session?.accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

let refreshPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = useAuthStore.getState().session?.refreshToken;
  if (!refreshToken) return null;
  try {
    const { data } = await axios.post(
      `${API_BASE_URL}/Auth/Refresh`,
      { refreshToken }
    );
    const session = useAuthStore.getState().session;
    if (!session) return null;
    useAuthStore.getState().setSession({ ...session, ...data });
    return data.accessToken as string;
  } catch {
    useAuthStore.getState().clear();
    return null;
  }
}

apiClient.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
    if (error.response?.status === 401 && original && !original._retry) {
      original._retry = true;
      refreshPromise ??= refreshAccessToken().finally(() => (refreshPromise = null));
      const newToken = await refreshPromise;
      if (newToken) {
        original.headers.Authorization = `Bearer ${newToken}`;
        return apiClient(original);
      }
    }
    return Promise.reject(error);
  }
);

export function extractErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const problem = error.response?.data as ValidationProblem | undefined;
    if (problem?.errors) {
      return Object.values(problem.errors).flat().join(" — ");
    }
    if (problem?.title) return problem.title;
    if (error.response?.status === 403 || error.response?.status === 401) {
      return "دسترسی مجاز نیست";
    }
    return "خطا در ارتباط با سرور";
  }
  return "خطای نامشخص";
}
