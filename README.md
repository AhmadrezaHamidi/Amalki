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
| [docs/SEO-GEO.md](docs/SEO-GEO.md) | سئو و GEO: آنچه هست و آنچه برنامه‌ریزی شده |
| [docs/DESIGN.md](docs/DESIGN.md) | تصمیم‌های طراحی و سیستم طراحی |
| [design-system/amlak/MASTER.md](design-system/amlak/MASTER.md) | سیستم طراحی تولیدشده (منبع حقیقت رنگ و فونت) |
| [design-options.html](design-options.html) | پیش‌نمایش سه طرح پیشنهادی (طرح ۱ انتخاب شد) |
| [design/canvas/](design/canvas/) | بوم طراحی نسخهٔ ۲: صفحه‌های وب و اپ موبایل |

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

۲۴ تست (ناشر کانال، دکمهٔ پست، اعلان سرنخ، سئو، نرمال‌سازی موبایل) با Bot API ساختگی و SQLite اجرا می‌شوند؛ به اینترنت و توکن واقعی نیاز ندارند.

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
