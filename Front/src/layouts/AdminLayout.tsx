import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { clsx } from "@/lib/clsx";
import { useSeo } from "@/lib/seo";
import { useAuthStore } from "@/store/authStore";
import { logout } from "@/features/auth/api";

const nav = [
  { to: "/admin", label: "داشبورد", end: true },
  { to: "/admin/properties", label: "آگهی‌ها" },
  { to: "/admin/leads", label: "درخواست‌های تماس" },
];

export function AdminLayout() {
  const session = useAuthStore((s) => s.session);
  const clear = useAuthStore((s) => s.clear);
  const navigate = useNavigate();
  // پنل نباید ایندکس شود
  useSeo({ title: "پنل مدیریت", robots: "noindex,nofollow" });

  async function onLogout() {
    await logout();
    clear();
    navigate("/login");
  }

  return (
    <div className="min-h-screen md:flex">
      <aside className="border-b border-brand-200 bg-white md:w-60 md:shrink-0 md:border-b-0 md:border-e">
        <div className="p-5 text-xl font-extrabold text-brand-700">پنل آشیانه</div>
        <nav aria-label="منوی پنل" className="flex gap-1 overflow-x-auto px-3 pb-3 md:flex-col">
          {nav.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.end}
              className={({ isActive }) =>
                clsx("whitespace-nowrap rounded-lg px-3 py-2 text-sm", isActive ? "bg-brand-100 font-bold text-brand-800" : "hover:bg-brand-50")
              }
            >
              {n.label}
            </NavLink>
          ))}
          <a href="/" target="_blank" rel="noreferrer" className="whitespace-nowrap rounded-lg px-3 py-2 text-sm hover:bg-brand-50">
            مشاهدهٔ سایت ↗
          </a>
        </nav>
        <div className="hidden border-t border-brand-200 p-4 text-sm md:block">
          <div className="text-slate-600">{session?.displayName}</div>
          <button type="button" onClick={onLogout} className="mt-2 text-red-700">
            خروج
          </button>
        </div>
      </aside>
      <main className="min-w-0 flex-1 p-4 md:p-8">
        <Outlet />
      </main>
    </div>
  );
}
