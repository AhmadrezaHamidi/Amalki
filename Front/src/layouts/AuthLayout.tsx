import { Outlet } from "react-router-dom";

export function AuthLayout() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-b from-brand-50 to-white px-4">
      <div className="w-full max-w-sm">
        <p className="mb-8 text-center text-2xl font-extrabold text-brand-700">آشیانه</p>
        <Outlet />
      </div>
    </div>
  );
}
