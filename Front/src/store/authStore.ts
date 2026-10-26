import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { UserRole } from "@/types/enums";

interface Session {
  accessToken: string;
  refreshToken: string;
  userId: number;
  role: UserRole;
  displayName: string;
}

interface AuthState {
  session: Session | null;
  setSession: (session: Session) => void;
  clear: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      session: null,
      setSession: (session) => set({ session }),
      clear: () => set({ session: null }),
    }),
    { name: "amlak-auth" }
  )
);
