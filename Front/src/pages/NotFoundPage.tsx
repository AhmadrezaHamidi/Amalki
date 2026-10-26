import { Link } from "react-router-dom";
import { useSeo } from "@/lib/seo";
import { LIST_PATH } from "@/features/listing/paths";

export function NotFoundPage() {
  useSeo({ title: "صفحه پیدا نشد", robots: "noindex" });
  return (
    <div className="mx-auto max-w-xl px-4 py-20 text-center">
      <h1 className="text-3xl font-extrabold">صفحه پیدا نشد</h1>
      <p className="mt-3 text-slate-600">این آگهی منتشر نشده، بسته شده یا وجود ندارد.</p>
      <Link to={LIST_PATH} className="mt-6 inline-block rounded-2xl bg-brand-700 px-6 py-3 font-bold text-white">
        مشاهدهٔ همهٔ آگهی‌ها
      </Link>
    </div>
  );
}
