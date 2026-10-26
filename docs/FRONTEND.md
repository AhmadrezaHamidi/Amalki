# فرانت‌اند (React)

`Front/` با همان ساختار Salmandan: Vite + React 18 + TypeScript + Tailwind + React Query + Zustand + react-hook-form/zod. سایت Razor قبلی حذف شده و همهٔ داده از API (`Src/`) می‌آید.

## ساختار

```
Front/src/
├── lib/            apiClient (توکن + تمدید خودکار در ۴۰۱)، config، fa (ارقام و پول فارسی)، seo (عنوان/متا/canonical/JSON-LD)
├── store/          authStore (نشست در localStorage)
├── types/          enums (برچسب فارسی + نگاشت عددی برای بدنهٔ Command ها)، common
├── components/ui/  Button، Card، Input/Select/Textarea، Badge، Spinner، DataTable
├── layouts/        PublicLayout، AuthLayout، AdminLayout
├── router/         مسیرها + ProtectedRoute (نقش Admin)
├── pages/          NotFoundPage
└── features/
    ├── listing/           صفحهٔ اصلی، لیست با فیلتر پیشرفته، جزئیات آگهی، کارت، جست‌وجو
    ├── leads/             فرم درخواست تماس (عمومی) و صفحهٔ پیگیری (مدیر)
    ├── auth/              ورود با کد پیامکی
    ├── admin-properties/  لیست مدیریت، فرم ساخت/ویرایش، مدیریت عکس، وضعیت کانال‌ها
    └── dashboard/         داشبورد مدیر
```

## مسیرها

| مسیر | صفحه |
|---|---|
| `/` | صفحهٔ اصلی: جست‌وجو و ۸ آگهی آخر |
| `/املاک` | لیست (پارامترها همان API: `deal, kind, city, district, minPrice, maxPrice, maxRent, minArea, maxArea, minBedrooms, sort, page`) |
| `/ملک/{slug}` | جزئیات، گالری، درخواست تماس، آگهی‌های مشابه؛ ناموجود ← صفحهٔ «پیدا نشد» با `noindex` |
| `/login` | ورود مدیر |
| `/admin`، `/admin/properties`، `/admin/properties/new`، `/admin/properties/{id}`، `/admin/leads` | پنل (فقط نقش Admin) |

مسیرهای فارسی همان مسیرهای سایت قبلی‌اند تا لینک‌های ایندکس‌شده نشکنند.

## اجرا

```bash
cd Front
npm install
npm run dev      # http://localhost:5173 — /api و فایل‌های سئو به http://localhost:5300 پروکسی می‌شوند
npm test         # تست واحد کمکی‌ها (vitest)
npm run build    # خروجی در dist/
```

آدرس API در توسعه با `VITE_API_PROXY` عوض می‌شود. متغیرها در `.env.example`.

## استقرار

`dist/` را پشت nginx بگذارید؛ نمونه: [`Front/nginx.conf`](../Front/nginx.conf). سه چیز لازم است:

۱. `/api/*` به API (`Amlak.ServiceHost`).
۲. `/sitemap.xml`، `/robots.txt` و `/llms.txt` به API — این فایل‌ها حالا در API ساخته می‌شوند.
۳. بازگشت همهٔ مسیرهای دیگر به `index.html` (مسیریابی سمت مرورگر).

`Site:BaseUrl` در API و `VITE_SITE_URL` در فرانت باید دامنهٔ واقعی باشند.

## سئو در SPA — محدودیت مهم

سایت Razor قبلی HTML کامل از سرور می‌داد. React فقط پوستهٔ خالی می‌دهد و عنوان، توضیح، canonical، Open Graph و JSON-LD پس از اجرای JS ساخته می‌شوند (`useSeo`):

- **گوگل** JS را اجرا می‌کند و صفحه‌ها را می‌بیند، ولی ایندکس کندتر است.
- **بیشتر خزنده‌های هوش مصنوعی (GEO)** و **پیش‌نمایش لینک در تلگرام/بله** JS اجرا نمی‌کنند و فقط عنوان پیش‌فرض را می‌بینند.

راه‌حل‌های پیشنهادی، از ساده به کامل:

1. **پیش‌رندر در nginx:** برای user-agent ربات‌ها، صفحه از یک سرویس پیش‌رندر (مثل Rendertron یا prerender.io) گرفته شود.
2. **پیش‌رندر هنگام build** برای صفحه‌های ثابت (صفحهٔ اصلی، لیست‌ها)؛ صفحهٔ آگهی‌ها پویاست و کافی نیست.
3. **مهاجرت به React با SSR** (Next.js یا React Router در حالت framework) — همان کامپوننت‌ها، با HTML کامل از سرور.

`sitemap.xml`، `robots.txt` و `llms.txt` چون در API ساخته می‌شوند، تحت تأثیر این محدودیت نیستند.

## تست

- `npm test`: قالب پول فارسی (هم‌رفتار با API)، تجزیهٔ عدد با ارقام فارسی، escape شدن JSON-LD، و تبدیل فیلتر ↔ URL.
- بررسی در مرورگر (Playwright، API ساختگی با همان شکل پاسخ‌های API): صفحهٔ اصلی و جست‌وجو، لیست با فیلتر پیشرفته/ترتیب/صفحه‌بندی، نبود اسکرول افقی در ۳۷۵ پیکسل، جزئیات با JSON-LD و og:image، فرم درخواست تماس، ۴۰۴ با noindex، ورود OTP و خطای ۴۲۲ فارسی، ساخت آگهی اجاره (enum عددی و ارقام فارسی در بدنه)، آپلود multipart عکس، انتشار، و تغییر وضعیت درخواست تماس. این اسکریپت در مخزن نیست.
