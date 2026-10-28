# آشیانه (Amlak) — سایت خرید، فروش و اجارهٔ ملک

سایت آگهی املاک با پنل مدیریت. آگهی‌ها (فروش، رهن و اجاره) از پنل ثبت می‌شوند و با انتشار، خودکار در کانال تلگرام و ربات بله هم قرار می‌گیرند. سئو و GEO (دیده‌شدن در موتورهای جست‌وجوی هوش مصنوعی) بخشی از طراحی است.

> وضعیت لحظه‌ای کارها: [PLANNER.md](PLANNER.md)

## مستندات

| فایل | محتوا |
|---|---|
| [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) | نیازمندی‌های کارکردی و غیرکارکردی، با وضعیت هر مورد |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | معماری، مدل داده، جریان انتشار، امنیت |
| [docs/ADMIN-GUIDE.md](docs/ADMIN-GUIDE.md) | راهنمای استفاده از پنل مدیریت |
| [docs/CHANNELS.md](docs/CHANNELS.md) | راه‌اندازی ربات تلگرام و بله، تنظیمات، رفع خطا |
| [docs/STORAGE.md](docs/STORAGE.md) | ذخیرهٔ عکس‌ها روی دیسک یا MinIO |
| [docs/SEO-GEO.md](docs/SEO-GEO.md) | سئو و GEO: آنچه هست و آنچه برنامه‌ریزی شده |
| [docs/DESIGN.md](docs/DESIGN.md) | تصمیم‌های طراحی و سیستم طراحی |
| [design-system/amlak/MASTER.md](design-system/amlak/MASTER.md) | سیستم طراحی تولیدشده (منبع حقیقت رنگ و فونت) |
| [design-options.html](design-options.html) | پیش‌نمایش سه طرح پیشنهادی (طرح ۱ انتخاب شد) |
| [design/canvas/](design/canvas/) | بوم طراحی نسخهٔ ۲: صفحه‌های وب و اپ موبایل |
| [docs/UI-VARIANTS.md](docs/UI-VARIANTS.md) | ده طرح ظاهر سایت و بررسی سایت‌های املاک ایران |
| [loadtest/README.md](loadtest/README.md) | تست بار با k6: اجرا، نتیجه‌ها و گلوگاه‌ها |

## ده طرح ظاهر سایت

ده طرح UI/UX بر اساس سایت‌های برتر املاک ایران ساخته شده است. همه روی همان HTML کار می‌کنند و با `Ui:Variant` در appsettings انتخاب می‌شوند؛ در Development با `?ui=<شناسه>` یا دکمهٔ شناور «طرح» و صفحهٔ `/طرح-ها` می‌توان همه را امتحان کرد. جزئیات و بررسی بیست سایت: [docs/UI-VARIANTS.md](docs/UI-VARIANTS.md).

عکس‌های آگهی‌ها واقعی‌اند (۱۸ عکس از Unsplash با مجوز استفادهٔ تجاری آزاد، در `wwwroot/img/demo`، فهرست در `tools/demo-photos.tsv`). اسکرین‌شات‌ها: چپ صفحهٔ اصلی در دسکتاپ، راست فهرست آگهی‌ها در موبایل.

### ۱. فید ساده (`divar`) — الهام از دیوار

قرمز تیره، بدون کادر و سایه، فهرست با خط جداکننده.

<p>
  <img src="docs/screenshots/variants/divar-desktop.jpg" alt="طرح فید ساده در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/divar-mobile.jpg" alt="طرح فید ساده در موبایل" width="24%">
</p>

### ۲. بازار دوستانه (`sheypoor`) — الهام از شیپور

آبی فیروزه‌ای، کارت‌های گرد سفید روی خاکستری روشن.

<p>
  <img src="docs/screenshots/variants/sheypoor-desktop.jpg" alt="طرح بازار دوستانه در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/sheypoor-mobile.jpg" alt="طرح بازار دوستانه در موبایل" width="24%">
</p>

### ۳. پورتال تخصصی (`kilid`) — الهام از کیلید

هدر آبی، جست‌وجوی بزرگ، شبکهٔ کارت عکس‌دار، دکمهٔ نارنجی.

<p>
  <img src="docs/screenshots/variants/kilid-desktop.jpg" alt="طرح پورتال تخصصی در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/kilid-mobile.jpg" alt="طرح پورتال تخصصی در موبایل" width="24%">
</p>

### ۴. داده و قیمت (`melkradar`) — الهام از ملک‌رادار

سرمه‌ای و کهربایی، قیمت درشت بالای کارت، مشخصات برچسبی.

<p>
  <img src="docs/screenshots/variants/melkradar-desktop.jpg" alt="طرح داده و قیمت در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/melkradar-mobile.jpg" alt="طرح داده و قیمت در موبایل" width="24%">
</p>

### ۵. آرام و جادار (`ihome`) — الهام از آی‌هوم

بنفش، گوشه‌های خیلی گرد، کارت شناور.

<p>
  <img src="docs/screenshots/variants/ihome-desktop.jpg" alt="طرح آرام و جادار در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/ihome-mobile.jpg" alt="طرح آرام و جادار در موبایل" width="24%">
</p>

### ۶. سفر و اقامت (`jabama`) — الهام از جاباما

هدر نارنجی، جست‌وجوی کپسولی، قیمت رنگی.

<p>
  <img src="docs/screenshots/variants/jabama-desktop.jpg" alt="طرح سفر و اقامت در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/jabama-mobile.jpg" alt="طرح سفر و اقامت در موبایل" width="24%">
</p>

### ۷. ویلا و طبیعت (`jajiga`) — الهام از جاجیگا

سبز جنگلی روی کرم.

<p>
  <img src="docs/screenshots/variants/jajiga-desktop.jpg" alt="طرح ویلا و طبیعت در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/jajiga-mobile.jpg" alt="طرح ویلا و طبیعت در موبایل" width="24%">
</p>

### ۸. صمیمی و نرم (`shab`) — الهام از شب / اتاقک

مرجانی، همه‌چیز کپسولی.

<p>
  <img src="docs/screenshots/variants/shab-desktop.jpg" alt="طرح صمیمی و نرم در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/shab-mobile.jpg" alt="طرح صمیمی و نرم در موبایل" width="24%">
</p>

### ۹. لوکس (`delta`) — الهام از دلتا

همیشه تیره، طلایی، گوشه‌های تیز.

<p>
  <img src="docs/screenshots/variants/delta-desktop.jpg" alt="طرح لوکس در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/delta-mobile.jpg" alt="طرح لوکس در موبایل" width="24%">
</p>

### ۱۰. میز کار مشاور (`melkana`) — الهام از ملکانه / ایران‌فایل

نیلی روی خاکستری، نوار رنگی نوع معامله.

<p>
  <img src="docs/screenshots/variants/melkana-desktop.jpg" alt="طرح میز کار مشاور در دسکتاپ" width="68%">
  <img src="docs/screenshots/variants/melkana-mobile.jpg" alt="طرح میز کار مشاور در موبایل" width="24%">
</p>

## استک

ASP.NET Core 9 (Razor Pages) · EF Core 9 · SQL Server (Express در توسعه) · ASP.NET Identity · xUnit + SQLite برای تست

## اجرای محلی

پیش‌نیاز: .NET SDK 9، SQL Server (یا Express با نام `localhost\SQLEXPRESS`).

```bash
cd src/Amlak.Web

# یک‌بار: حساب مدیر توسعه (مقادیر خودتان را بگذارید؛ رمز حداقل ۱۰ نویسه با حرف بزرگ، کوچک، عدد و نماد)
dotnet user-secrets set "Admin:Email" "admin@example.com"
dotnet user-secrets set "Admin:Password" "<رمز-قوی>"

dotnet ef database update      # ساخت دیتابیس Amlak
dotnet run --launch-profile http
```

اطلاعات تماس سایت (اختیاری؛ اگر خالی باشد نمایش داده نمی‌شود):

```bash
dotnet user-secrets set "Site:Phone" "۰۲۱-۱۲۳۴۵۶۷۸"
dotnet user-secrets set "Site:Address" "تهران، ..."
dotnet user-secrets set "Site:TelegramUrl" "https://t.me/<کانال>"
dotnet user-secrets set "Site:BaleUrl" "https://ble.ir/<کانال>"
```

سایت روی `http://localhost:5244` بالا می‌آید. در حالت Development چهار آگهی نمونه خودکار ساخته می‌شود. ورود مدیر: `/Account/Login` ← پنل: `/Admin`.

## تست

```bash
dotnet test tests/Amlak.Tests
```

۲۹ تست (ناشر کانال و آلبوم، دکمهٔ پست، اعلان سرنخ، کد تیکت، ذخیره‌ساز محلی، سئو، نرمال‌سازی موبایل) با Bot API ساختگی و SQLite اجرا می‌شوند؛ به اینترنت و توکن واقعی نیاز ندارند.

## ساختار پوشه‌ها

```
src/Amlak.Web/
  Domain/            موجودیت‌ها (Property, PropertyPhoto, ChannelPost)
  Data/              AppDbContext, AdminSeed, DevSeed
  Migrations/        EF Core migrations
  Services/          Slugger, Fa (ارقام و پول فارسی), PhotoStorage
  Services/Publishing/  BotApiClient, CaptionBuilder, ChannelPublisher
  Pages/             صفحه‌های عمومی، Account، Admin
tests/Amlak.Tests/   تست‌های ناشر
docs/                مستندات
design-system/       سیستم طراحی
```

## امنیت پیکربندی

هیچ رمز یا توکنی در مخزن ذخیره نمی‌شود. مقادیر حساس (`Admin:*`, `Telegram:*`, `Bale:*`) فقط با user-secrets یا متغیر محیطی تنظیم می‌شوند. جزئیات: [docs/CHANNELS.md](docs/CHANNELS.md).
