import { createBrowserRouter } from "react-router-dom";
import { PublicLayout } from "@/layouts/PublicLayout";
import { AuthLayout } from "@/layouts/AuthLayout";
import { AdminLayout } from "@/layouts/AdminLayout";
import { ProtectedRoute } from "@/router/ProtectedRoute";
import { HomePage } from "@/features/listing/pages/HomePage";
import { PropertiesPage } from "@/features/listing/pages/PropertiesPage";
import { PropertyDetailPage } from "@/features/listing/pages/PropertyDetailPage";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { AdminDashboardPage } from "@/features/dashboard/AdminDashboardPage";
import { AdminPropertiesPage } from "@/features/admin-properties/pages/AdminPropertiesPage";
import { PropertyEditPage } from "@/features/admin-properties/pages/PropertyEditPage";
import { AdminLeadsPage } from "@/features/leads/pages/AdminLeadsPage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { LIST_PATH } from "@/features/listing/paths";

export const router = createBrowserRouter([
  {
    element: <PublicLayout />,
    children: [
      { path: "/", element: <HomePage /> },
      { path: LIST_PATH, element: <PropertiesPage /> },
      { path: "/ملک/:slug", element: <PropertyDetailPage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
  {
    element: <AuthLayout />,
    children: [{ path: "/login", element: <LoginPage /> }],
  },
  {
    element: <ProtectedRoute roles={["Admin"]} />,
    children: [
      {
        path: "/admin",
        element: <AdminLayout />,
        children: [
          { index: true, element: <AdminDashboardPage /> },
          { path: "properties", element: <AdminPropertiesPage /> },
          { path: "properties/new", element: <PropertyEditPage /> },
          { path: "properties/:id", element: <PropertyEditPage /> },
          { path: "leads", element: <AdminLeadsPage /> },
        ],
      },
    ],
  },
]);
