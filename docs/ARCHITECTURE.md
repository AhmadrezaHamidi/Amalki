# معماری

## نمای کلی

یک برنامهٔ ASP.NET Core 9 با Razor Pages (رندر سمت سرور برای سئو) و یک دیتابیس SQL Server. ارسال به کانال‌ها با HTTP مستقیم به Bot API انجام می‌شود؛ سرویس یا صف جداگانه‌ای وجود ندارد.

```
مرورگر ──> Razor Pages ──> AppDbContext ──> SQL Server (Amlak)
              │
              ├─ Pages/Admin ──> PhotoStorage ──> wwwroot/uploads
              └─ Pages/Admin ──> ChannelPublisher ──> BotApiClient ──> Telegram / Bale
```

## مدل داده

| جدول | کلیدها و ستون‌های مهم |
|---|---|
| `Properties` | `Slug` (یکتا)، `Title`، `Deal` (Sale/Rent)، `Kind`، `Status` (Draft/Published/Closed)، `Price` یا `Deposit`+`MonthlyRent`، `AreaM2`، `Bedrooms`، `Floor`، `Parking`، `YearBuilt`، `City`، `District`، `Address`، `Latitude/Longitude`، `PublishedAtUtc` |
| `PropertyPhotos` | `PropertyId` (cascade)، `Url`، `Alt`، `SortOrder` |
| `ChannelPosts` | `PropertyId` (cascade)، `Channel` (Telegram/Bale)، `ExternalMessageId`، `HasPhoto`، `Error`؛ ایندکس یکتا روی (`PropertyId`, `Channel`) |
| `AspNet*` | جدول‌های Identity (کاربر، نقش) |

ایندکس: `Properties(Status, Deal, City)` برای لیست و فیلتر. زمان‌ها UTC هستند.

مهاجرت‌ها: `Initial` و `ChannelPostUniqueAndPhoto` در `src/Amlak.Web/Migrations`.

## صفحه‌ها و مسیرها

| مسیر | توضیح |
|---|---|
| `/` | صفحهٔ اصلی |
| `/املاک` | لیست و فیلتر (پارامترها: `deal`, `kind`, `city`, `maxPrice`, `p`) |
| `/ملک/{slug}` | جزئیات آگهی |
| `/Account/Login`, `/Account/Logout` | ورود و خروج مدیر |
| `/Admin` | لیست مدیریت (نیاز به نقش Admin) |
| `/Admin/Edit/{id?}` | ساخت و ویرایش آگهی |

## جریان انتشار در کانال‌ها

`ChannelPublisher.SyncAsync(propertyId)` پس از هر ذخیره یا تغییر وضعیت صدا زده می‌شود و برای هر کانالِ پیکربندی‌شده:

| وضعیت آگهی | پست موجود دارد؟ | کار |
|---|---|---|
| Published | خیر | `sendPhoto` (اگر فایل عکس هست) وگرنه `sendMessage`؛ شناسهٔ پیام ذخیره می‌شود |
| Published | بله | `editMessageCaption` (پست عکس‌دار) یا `editMessageText` |
| Draft / Closed | بله | `deleteMessage` و حذف رکورد (پیام قبلاً پاک‌شده هم موفق حساب می‌شود) |
| Draft / Closed | خیر | هیچ |

- ارسال ناموفق: خطا در `ChannelPost.Error` ثبت می‌شود و دفعهٔ بعد (ذخیره یا دکمهٔ همگام‌سازی) دوباره تلاش می‌شود.
- «message is not modified» موفق حساب می‌شود.
- پیام خطا از متن پاسخ سرویس ساخته می‌شود، توکن در آن `***` می‌شود و هرگز `Exception.Message` ذخیره نمی‌شود (می‌تواند URL حاوی توکن را داشته باشد).
- لاگ URL درخواست‌های HttpClient بسته شده (`System.Net.Http.HttpClient: Warning`) تا توکن در لاگ نیاید.
- محدودیت: ارسال داخل درخواست ادمین و همزمان (حداکثر ۳۰ ثانیه برای هر تماس). صف پس‌زمینه در برنامه است (CHN-08).

## امنیت

- **احراز هویت:** ASP.NET Identity با کوکی؛ رمز حداقل ۱۰ نویسه؛ قفل پس از ۵ تلاش ناموفق؛ کوکی HttpOnly.
- **مجوز:** سیاست `Admin` روی کل پوشهٔ `/Admin` (convention در `Program.cs`).
- **CSRF:** توکن ضدجعل روی همهٔ فرم‌های POST (پیش‌فرض Razor Pages)؛ درخواست بدون توکن ۴۰۰ می‌گیرد.
- **آپلود:** فقط `.jpg/.jpeg/.png/.webp`، حداکثر ۵ مگابایت، بررسی امضای بایتی فایل (نه فقط پسوند)، نام فایل تصادفی (GUID).
- **اسرار:** حساب مدیر اولیه از `Admin:Email/Admin:Password` ساخته می‌شود؛ بدون این دو کاربری ساخته نمی‌شود (فقط هشدار لاگ).
- **صفحه‌های مدیریت** `noindex,nofollow` هستند.
- **JSON-LD** با escape `</` تولید می‌شود تا تزریق اسکریپت ممکن نباشد.

## پیکربندی

| کلید | کاربرد |
|---|---|
| `ConnectionStrings:Default` | اتصال SQL Server |
| `Site:BaseUrl` | آدرس عمومی سایت برای لینک کپشن (و بعداً sitemap) |
| `Admin:Email`, `Admin:Password` | حساب مدیر اولیه (فقط user-secrets/متغیر محیطی) |
| `Telegram:BotToken`, `Telegram:ChatId`, `Telegram:ApiBase` | کانال تلگرام (ApiBase اختیاری) |
| `Bale:BotToken`, `Bale:ChatId`, `Bale:ApiBase` | ربات بله (ApiBase اختیاری) |

کانالی که `BotToken` یا `ChatId` نداشته باشد غیرفعال است.

## تست

`tests/Amlak.Tests/PublishingTests.cs` (xUnit، SQLite درون‌حافظه، Bot API ساختگی):
کپشن، ارسال یک‌باره و سپس ویرایش، عدم انتشار پیش‌نویس، حذف پیام هنگام لغو انتشار، مسیر عکس‌دار، ثبت خطا بدون نشت توکن و تلاش مجدد، دو کانال با دو میزبان، و بدون کانال = بدون اثر.

پوشش‌داده‌نشده: صفحه‌های Razor (فقط با درخواست HTTP دستی بررسی شده‌اند)، `PhotoStorage`، ورود/خروج.

## تصمیم‌های معماری

| تصمیم | دلیل |
|---|---|
| Razor Pages به‌جای SPA | سئوی بهتر با HTML کامل، سادگی، هم‌خوانی با تخصص تیم (.NET) |
| یک کلاینت برای تلگرام و بله | API بله با Telegram Bot API هم‌شکل است؛ فقط میزبان فرق دارد |
| آپلود عکس به‌جای لینک عکس در کانال | در توسعه (localhost) هم کار می‌کند و به آدرس عمومی عکس وابسته نیست |
| خطای ارسال، غیرمسدودکننده | شکست تلگرام (فیلتر یا قطعی) نباید ثبت آگهی را بخواباند |
| Slug فارسی | گوگل URL فارسی را ایندکس می‌کند و خوانا است |
