# معماری

## نمای کلی

یک برنامهٔ ASP.NET Core 9 با Razor Pages (رندر سمت سرور برای سئو) و یک دیتابیس SQL Server. ارسال به کانال‌ها با HTTP مستقیم به Bot API انجام می‌شود؛ سرویس یا صف جداگانه‌ای وجود ندارد.

```
مرورگر ──> Razor Pages ──> AppDbContext ──> SQL Server (Amlak)
              │
              ├─ Pages/Admin ──> PhotoStorage ──> wwwroot/uploads
              └─ Pages/Admin ──> ChannelPublisher ──> BotApiClient ──> Telegram / Bale
```

## لایهٔ Domain (DDD)

قوانین کسب‌وکار داخل Aggregateها هستند، نه در صفحه‌ها. همهٔ setterها private هستند و تغییر فقط از متدها می‌گذرد (`src/Amlak.Web/Domain`).

| Aggregate | متدها | قانون‌ها | رویدادها |
|---|---|---|---|
| `Property` (آگهی، با `PropertyPhoto`) | `Create`، `Update`، `Publish`، `Unpublish`، `Close`، `ChangeStatus`، `TogglePublished`، `AddPhoto`، `RemovePhoto`، `MarkDeleted` | فقط آگهی با قیمت کامل منتشر می‌شود؛ آگهی منتشرشده قیمتش را از دست نمی‌دهد؛ تاریخ اولین انتشار حفظ می‌شود؛ مبلغ منفی، متراژ صفر و enum نامعتبر رد می‌شود | `ListingCreated`، `ListingChanged`، `ListingPublished`، `ListingUnpublished`، `ListingPhotosChanged`، `ListingDeleted` |
| `Ticket` (با `TicketMessage`) | `Open`، `ReplyFromVisitor`، `ReplyFromStaff`، `ChangeStatus` | تیکت بسته پیام بازدیدکننده نمی‌گیرد؛ پاسخ پشتیبانی تیکت را «پاسخ داده شد» می‌کند | `TicketOpened`، `TicketMessageReceived`، `TicketAnswered` |
| `Lead` | `Submit`، `ToggleHandled` | نام و پیام محدودیت طول دارند | `LeadSubmitted` |

Value Objectها: `Pricing` (فروش یا رهن/اجاره)، `ListingDetails` (مشخصات آگهی، با نرمال‌سازی ي/ك)، `PhoneNumber` (همیشه 09xxxxxxxxx). خطای قانون با `DomainException` و پیام فارسی برمی‌گردد و صفحه آن را در فرم نشان می‌دهد. زمان از `TimeProvider` تزریق می‌شود.

رویدادها فعلاً فقط جمع می‌شوند؛ انتشار آن‌ها از طریق Outbox مرحلهٔ بعد است.

## مدل داده

| جدول | کلیدها و ستون‌های مهم |
|---|---|
| `Properties` | `Slug` (یکتا)، `Title`، `Deal` (Sale/Rent)، `Kind`، `Status` (Draft/Published/Closed)، `Price` یا `Deposit`+`MonthlyRent`، `AreaM2`، `Bedrooms`، `Floor`، `Parking`، `YearBuilt`، `City`، `District`، `Address`، `Latitude/Longitude`، `PublishedAtUtc` |
| `PropertyPhotos` | `PropertyId` (cascade)، `Url`، `Alt`، `SortOrder` |
| `ChannelPosts` | `PropertyId` (cascade)، `Channel` (Telegram/Bale)، `ExternalMessageId`، `HasPhoto`، `Error`؛ ایندکس یکتا روی (`PropertyId`, `Channel`) |
| `AspNet*` | جدول‌های Identity (کاربر، نقش) |

ایندکس: `Properties(Status, Deal, City)` برای لیست و فیلتر. زمان‌ها UTC هستند.

مهاجرت‌ها: `Initial` و `ChannelPostUniqueAndPhoto` در `src/Amlak.Web/Migrations`.

## سمت خواندن (CQRS) با View های SQL

نوشتن‌ها مثل قبل از مدل اصلی (`Property` و `AppDbContext`) انجام می‌شوند. همهٔ خواندن‌های صفحه‌های عمومی و شمارنده‌های پنل از `src/Amlak.Web/Read` و از View های SQL می‌آیند (موجودیت‌های keyless در EF):

| View | محتوا | مصرف‌کننده |
|---|---|---|
| `vw_ListingCards` | هر آگهی منتشرشده، تخت‌شده با عکس کاور (اولین `SortOrder`)، تعداد عکس و `SortPrice` | خانه، لیست و لندینگ‌ها، آگهی‌های مشابه، نشان‌شده‌ها |
| `vw_ListingFacets` | تعداد آگهی و جمع قیمت و متراژ به ازای (معامله، نوع، شهر، محله)؛ روی SQL Server **indexed view** است (ذخیره‌شده و خودکار به‌روز) و با `NOEXPAND` خوانده می‌شود | شمارش نوع ملک در خانه، لینک شهرها و محله‌ها، پیشنهاد شهر |
| `vw_AdminCounters` | یک ردیف با همهٔ شمارنده‌های داشبورد و badge های منو | داشبورد و منوی پنل |

- `ListingQueries` و `AdminQueries` تنها جای کوئری خواندن هستند؛ فیلترها در `ListingFilter` جمع شده‌اند.
- `Properties.SortPrice` ستون محاسبه‌شدهٔ ذخیره‌شده است (`ISNULL(Price, ISNULL(Deposit, 0))`) تا فیلتر و مرتب‌سازی قیمت از ایندکس استفاده کند.
- ایندکس‌ها: `Properties(Status, Deal, City, Kind, PublishedAtUtc)` برای لندینگ‌ها، `(Status, PublishedAtUtc)`، `(Status, SortPrice)`، و `PropertyPhotos(PropertyId, SortOrder) INCLUDE (Url, Alt)` برای عکس کاور.
- **هشدار:** `vw_ListingFacets` با `SCHEMABINDING` ساخته شده؛ تغییر ستون‌های `Status`، `Deal`، `Kind`، `City`، `District`، `AreaM2` یا `SortPrice` در یک migration باید اول این View را drop و بعد دوباره بسازد. اسکریپت‌هایی که با `sqlcmd` روی جدول `Properties` می‌نویسند باید با `-I` (QUOTED_IDENTIFIER ON) اجرا شوند.
- نسخهٔ SQLite همین View ها در `Read/ReadViews.cs` است (برای تست‌ها، چون `EnsureCreated` View نمی‌سازد) و باید هم‌گام با migration `ReadViews` بماند.

اندازه‌گیری روی SQL Server 2022 Express با ۲۰ هزار آگهی و ۱۰۰ هزار عکس (میانهٔ ۲۰ درخواست پشت‌سرهم، کل صفحه):

| صفحه | قبل | بعد |
|---|---|---|
| `/املاک` | 27 ms | 11 ms |
| `/املاک?sort=Cheapest` | 67 ms | 11 ms |
| `/املاک/تهران` | 31 ms | 18 ms |
| `/املاک/تهران/خرید-آپارتمان` | 57 ms | 8 ms |
| `/ملک/{slug}` | 20 ms | 7 ms |

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
- **پشت پراکسی:** `UseForwardedHeaders` اول pipeline است. IP پراکسی (nginx) را در `Proxy:KnownProxies` یا `Proxy:KnownNetworks` بگذارید (loopback پیش‌فرض قبول است)؛ وگرنه همهٔ بازدیدکننده‌ها یک IP دارند و rate limit برای همه با هم بسته می‌شود.
- **Host:** در Production، scheme و host درخواست از `Site:BaseUrl` گرفته می‌شود تا canonical و sitemap با هدر Host جعلی ساخته نشوند.
- **هدرها:** `X-Content-Type-Options`، `Referrer-Policy`، `Permissions-Policy`، `X-Frame-Options` و CSP پایه (`frame-ancestors`، `base-uri`، `object-src`، `form-action`).
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

`tests/Amlak.Tests/ReadSideTests.cs`: View ها و `ListingQueries` (کاور، فیلترها، مرتب‌سازی و صفحه‌بندی، facet ها، به‌روز شدن View بعد از نوشتن، شمارنده‌های پنل). هر تست دو بار اجرا می‌شود: روی SQLite و، اگر `AMLAK_TEST_SQLSERVER` (رشتهٔ اتصال بدون نام دیتابیس) تنظیم شده باشد، روی SQL Server واقعی با همان migration ها؛ در غیر این صورت نسخهٔ SQL Server رد (skip) می‌شود.

پوشش‌داده‌نشده: صفحه‌های Razor (فقط با درخواست HTTP دستی بررسی شده‌اند)، `PhotoStorage`، ورود/خروج.

## تصمیم‌های معماری

| تصمیم | دلیل |
|---|---|
| Razor Pages به‌جای SPA | سئوی بهتر با HTML کامل، سادگی، هم‌خوانی با تخصص تیم (.NET) |
| یک کلاینت برای تلگرام و بله | API بله با Telegram Bot API هم‌شکل است؛ فقط میزبان فرق دارد |
| آپلود عکس به‌جای لینک عکس در کانال | در توسعه (localhost) هم کار می‌کند و به آدرس عمومی عکس وابسته نیست |
| خطای ارسال، غیرمسدودکننده | شکست تلگرام (فیلتر یا قطعی) نباید ثبت آگهی را بخواباند |
| Slug فارسی | گوگل URL فارسی را ایندکس می‌کند و خوانا است |
