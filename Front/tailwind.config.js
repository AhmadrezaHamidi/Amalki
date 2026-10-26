/** @type {import('tailwindcss').Config} */
// رنگ‌ها از سیستم طراحی (design-system/amlak/MASTER.md): طرح ۱ «مدرن و قابل‌اعتماد»
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#f0fdfa",
          100: "#ccfbf1",
          200: "#99f6e4",
          300: "#5eead4",
          400: "#2dd4bf",
          500: "#14b8a6",
          600: "#0d9488",
          700: "#0f766e",
          800: "#115e59",
          900: "#134e4a",
        },
        accent: {
          600: "#0284c7",
          700: "#0369a1",
        },
      },
      fontFamily: {
        sans: ["Vazirmatn", "Tahoma", "sans-serif"],
      },
      boxShadow: {
        soft: "0 10px 30px rgba(15,118,110,.12)",
      },
    },
  },
  plugins: [],
};
