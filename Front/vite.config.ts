/// <reference types="vitest" />
import path from "node:path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// در توسعه API و فایل‌های سئو از همان مبدأ فرانت پروکسی می‌شوند،
// همان کاری که nginx در تولید می‌کند (docs/FRONTEND.md)
const api = process.env.VITE_API_PROXY ?? "http://localhost:5300";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { "@": path.resolve(process.cwd(), "src") },
  },
  server: {
    port: 5173,
    proxy: {
      "/api": api,
      "/sitemap.xml": api,
      "/robots.txt": api,
      "/llms.txt": api,
    },
  },
  test: {
    environment: "node",
  },
});
