import { Link, Outlet } from "react-router-dom";
import { SITE_NAME } from "@/lib/config";
import { LIST_PATH, listPath } from "@/features/listing/paths";

export function PublicLayout() {
  return (
    <>
      <a href="#main" className="absolute -start-[999px] z-50 bg-white px-3 py-2 focus:start-2 focus:top-2">
        رفتن به محتوا
      </a>
      <header className="mx-auto max-w-6xl px-4">
        <nav aria-label="منوی اصلی" className="flex items-center justify-between gap-4 py-4">
          <Link to="/" className="text-2xl font-extrabold text-brand-700">
            {SITE_NAME}
          </Link>
          <ul className="hidden gap-6 md:flex">
            <li><Link to={listPath("Sale")}>خرید</Link></li>
            <li><Link to={listPath("Rent")}>اجاره و رهن</Link></li>
          </ul>
          <Link to={LIST_PATH} className="rounded-2xl bg-brand-700 px-5 py-2.5 font-bold text-white transition hover:-translate-y-0.5 hover:shadow-soft">
            همهٔ آگهی‌ها
          </Link>
        </nav>
      </header>
      <main id="main" className="pb-12">
        <Outlet />
      </main>
      <footer className="px-4 py-10 text-center text-sm text-slate-600">© {SITE_NAME} · خرید، فروش و اجارهٔ ملک</footer>
    </>
  );
}
