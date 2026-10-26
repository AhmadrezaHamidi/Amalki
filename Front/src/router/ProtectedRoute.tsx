import { Navigate, Outlet } from "react-router-dom";
import { useAuthStore } from "@/store/authStore";
import type { UserRole } from "@/types/enums";

export function ProtectedRoute({ roles }: { roles?: UserRole[] }) {
  const session = useAuthStore((s) => s.session);
  if (!session) return <Navigate to="/login" replace />;
  // نقش در API هم بررسی می‌شود؛ این فقط کاربر عادی را از پنل دور نگه می‌دارد
  if (roles && !roles.includes(session.role)) return <Navigate to="/" replace />;
  return <Outlet />;
}
