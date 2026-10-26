# سئو و GEO

**سئو** = دیده‌شدن در گوگل. **GEO** (Generative Engine Optimization) = ارجاع‌شدن و نقل‌شدن در پاسخ موتورهای هوش مصنوعی (ChatGPT، Gemini، Perplexity و ...). اغلب کارها مشترک است: HTML کامل، داده ساختاریافته، محتوای روشن و قابل‌استناد.

## آنچه الان هست

| مورد | وضعیت | محل |
|---|---|---|
| رندر سمت سرور (محتوا بدون جاوااسکریپت در HTML است) | هست | Razor Pages |
| `<title>` و `meta description` یکتا برای هر صفحه (جزئیات: عنوان + نوع معامله + محله/شهر + قیمت) | هست | `_Layout`, `Details`, `Properties/Index` |
| `canonical`؛ لیست فیلترشده/صفحه‌بندی‌شده به لیست پایه اشاره می‌کند | هست | `ViewData["Canonical"]` |
| Open Graph (`og:title/description/url/image/locale`) | هست | `_Layout` |
| JSON-LD `RealEstateListing` (نام، توضیح، آدرس، متراژ، `Offer` با قیمت، تصاویر) | هست | `Details.cshtml` |
| نشانی تمیز و فارسی (`/ملک/{slug}`) | هست | `Slugger` |
| `lang="fa" dir="rtl"` | هست | `_Layout` |
| `alt` برای عکس‌ها (عنوان + محله + شهر) | هست | `PhotoStorage` / ثبت عکس |
| `noindex` برای پنل مدیریت و ورود | هست | `ViewData["Robots"]` |
| `/sitemap.xml` پویا (صفحه‌های اصلی، لیست‌ها، آگهی‌های منتشرشده با `lastmod` و عکس) | هست | `Services/Seo/SeoEndpoints.cs` |
| `/robots.txt` با `Disallow` برای Admin/Account، خط Sitemap و گروه صریح خزنده‌های هوش مصنوعی | هست | `SeoFiles.Robots` |
| `/llms.txt` (معرفی، صفحه‌های اصلی، ۲۰ آگهی آخر) | هست | `SeoFiles.Llms` |
| JSON-LD `RealEstateAgent` + `WebSite` (صفحهٔ اصلی) و `BreadcrumbList` (لیست و جزئیات) | هست | `Services/Seo/JsonLd.cs` |
| آدرس پایه از `Site:BaseUrl` (نه هدر Host) تا جعل Host فایل‌ها را مسموم نکند | هست | `SeoEndpoints.BaseUrl` |
| `loading="lazy"` و ابعاد عکس کارت‌ها | هست | `_PropertyCard` |
| **لیست:** canonical با `deal` + `city` + `p` (صفحهٔ ۲ به بعد خودش canonical است؛ `kind`/`maxPrice` به همان جمع می‌شوند)، `noindex` برای نتیجهٔ خالی، «صفحهٔ N» در عنوان | هست | `Properties/Index` |
| **لیست:** جملهٔ آغازین با آمار واقعی (تعداد آگهی و بازهٔ قیمت/اجاره) و همان در `meta description` | هست | `SeoText.ListIntro` |
| **لیست:** JSON-LD `ItemList`، مسیر خانه/معامله/شهر، لینک‌های صفحه‌بندی تمیز (بدون پارامتر خالی) | هست | `JsonLd.ItemList`, `IndexModel.PageUrl` |
| صفحه‌های لیست هر شهر (به تفکیک خرید/اجاره) در `sitemap.xml` | هست | `SeoFiles.Sitemap` |
| **جزئیات:** خلاصهٔ مستقل در پاراگراف اول (چه، کجا، چند متر، چند تومان) و همان در `meta description` | هست | `SeoText.Summary` |
| **جزئیات:** جدول مشخصات `<dl>` با برچسب ثابت، تاریخ انتشار شمسی در `<time>`، آگهی‌های مشابه همان شهر | هست | `Details` |
| **جزئیات:** `RealEstateListing` درست: قیمت به `IRR` (تومان × ۱۰)، اجاره با `unitCode: MON` و رهن جدا، `itemOffered` (Apartment/House/Place)، عکس و نشانی مطلق، بدون `null`، بدون نشانی دقیق | هست | `JsonLd.Listing` |
| **جزئیات:** عکس اول `fetchpriority="high"` (LCP)، بقیه lazy؛ `og:image` مطلق و `twitter:card` | هست | `Details`, `_Layout` |
| متن فارسی بدون `&#x...;` در HTML (`HtmlEncoder` با `UnicodeRanges.All`) | هست | `Program.cs` |
| تگ JSON-LD با `type="application/ld+json"` واقعی (قبلاً `ld&#x2B;json` می‌شد) | هست | `JsonLd.Script` |
| Skill کلود برای سئو/GEO این پروژه | هست | `.claude/skills/seo-geo/SKILL.md` |

## برنامه (به ترتیب اولویت)

### ۱. فایل‌های کشف (SEO-04, SEO-05) — انجام‌شده، ثبت در Search Console مانده

- **`/sitemap.xml`** پویا: صفحهٔ اصلی، لیست، همهٔ آگهی‌های منتشرشده با `lastmod` (بر پایهٔ `PublishedAtUtc`/آخرین ویرایش) و `<image:image>`. اگر آگهی‌ها از ۵۰٬۰۰۰ بیشتر شد، sitemap index.
- **`/robots.txt`**: اجازهٔ خزیدن عمومی، `Disallow: /Admin`, `/Account`, `/uploads` نیازی به مسدود کردن ندارد (عکس‌ها باید ایندکس شوند)، خط `Sitemap:`.
- **`/llms.txt`**: خلاصهٔ سایت برای مدل‌ها (معرفی، بخش‌های اصلی، نحوهٔ یافتن آگهی‌ها، اطلاعات تماس). فایل متنی ساده و کوتاه.
- در `robots.txt` خزندهٔ هوش مصنوعی را صریح مجاز کنید یا مسدود کنید (تصمیم کسب‌وکاری): `GPTBot`, `ClaudeBot`, `PerplexityBot`, `Google-Extended`, `CCBot`.

### ۲. داده ساختاریافتهٔ بیشتر (SEO-06) — سازمان/وب‌سایت/مسیر/ItemList/آگهی انجام‌شده؛ FAQPage مانده

- `Organization` / `RealEstateAgent` + `WebSite` (با `SearchAction`) در صفحهٔ اصلی: نام، لوگو، تلفن، نشانی، ساعات کار.
- `BreadcrumbList` در لیست و جزئیات (الان فقط مسیر متنی هست).
- `FAQPage` برای پرسش‌های متداول خرید/اجاره (محتوای واقعی؛ پاسخ کوتاه و مستقیم).
- `ItemList` در لیست آگهی‌ها.
- اعتبارسنجی با Rich Results Test و Schema Markup Validator پیش از انتشار.

### ۳. محتوا و ساختار برای GEO

- هر صفحهٔ آگهی، پاراگراف اول **خلاصهٔ مستقل** داشته باشد (چه، کجا، چند متر، چند تومان) تا مدل بتواند همان را نقل کند.
- جدول مشخصات با برچسب‌های ثابت (متراژ، خواب، طبقه، سال ساخت) به‌جای متن آزاد.
- صفحات **شهر/محله** (SEO-08) با آمار واقعی خود سایت (تعداد آگهی، بازهٔ قیمت) و پرسش‌های متداول محلی.
- نویسنده/سازمان و تاریخ انتشار و به‌روزرسانی در صفحه (سیگنال اعتماد).
- از ادعای غیرقابل‌اثبات («بهترین قیمت») پرهیز کنید؛ مدل‌ها و گوگل به داده و استناد وزن می‌دهند.
- اطلاعات تماس و نام کسب‌وکار در همهٔ صفحات یکسان (NAP).

### ۴. فنی و سرعت (SEO-07)

- تبدیل عکس‌ها به WebP و تولید اندازه‌های واکنش‌گرا (`srcset`)؛ فعلاً عکس اصلی آپلودی سرو می‌شود.
- هدف Core Web Vitals: LCP < 2٫5s، CLS < 0٫1، INP < 200ms.
- فشرده‌سازی پاسخ، کش مرورگر برای `/uploads`، `font-display: swap`، پیش‌اتصال به فونت (هست).
- فونت Vazirmatn را در صورت امکان خودمیزبانی کنید (کاهش وابستگی به Google Fonts و سرعت بهتر در ایران).
- HTTPS و HSTS در استقرار.
- `rel="next/prev"` لازم نیست؛ canonical لیست پایه کافی است.

### ۵. سئوی محلی

- ثبت در Google Business Profile و نشان؛ نقشه و نشانی دفتر در سایت.
- لینک‌سازی داخلی: از صفحهٔ آگهی به لیست همان محله/شهر و آگهی‌های مشابه.

## اندازه‌گیری

- Google Search Console (ثبت sitemap، پوشش ایندکس، Core Web Vitals).
- ابزارهای تحلیل ترافیک سازگار با ایران (مثلاً متریکا) در کنار یا به‌جای GA.
- برای GEO: بررسی دوره‌ای پرسش‌های هدف (مثلاً «خرید آپارتمان در سعادت‌آباد») در موتورهای هوش مصنوعی و ثبت اینکه سایت نقل می‌شود یا نه.

## نکتهٔ تصمیم: خزندهٔ هوش مصنوعی

در `robots.txt` خزنده‌های GPTBot، OAI-SearchBot، ChatGPT-User، ClaudeBot، Claude-User، PerplexityBot، Google-Extended و CCBot **مجاز** هستند تا آگهی‌ها در پاسخ‌های هوش مصنوعی نقل شوند. اگر نمی‌خواهید محتوا برای آموزش مدل‌ها استفاده شود، `Google-Extended` و `CCBot` را از `AiCrawlers` در `SeoFiles.cs` حذف کنید و `Disallow: /` بگذارید.

## چک‌لیست پیش از انتشار

- [ ] `Site:BaseUrl` روی دامنهٔ واقعی
- [ ] sitemap/robots/llms.txt در دسترس و در Search Console ثبت شده
- [ ] JSON-LD بدون خطا در Rich Results Test
- [ ] هر آگهی عنوان، توضیح و عکسِ دارای alt دارد
- [ ] صفحهٔ مدیریت و ورود `noindex`
- [ ] HTTPS فعال، ریدایرکت www/non-www یکتا
