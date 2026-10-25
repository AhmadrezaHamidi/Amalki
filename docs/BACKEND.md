# بک‌اند (DDD + CQRS)

بک‌اند املاک روی .NET 8 با همان ساختار همیشگی (`DentV2` و `Salmandan`) و پکیج‌های `AhmadBase.*` ساخته شده است. سایت Razor فعلی (`Src/Web/Amlak.Web`) هنوز مستقل کار می‌کند؛ قدم بعدی این است که صفحه‌ها به‌جای `AppDbContext` از این API بخوانند.

> نام اسمبلی‌ها باید از الگوی `{CompanyName}.{ContextName}.{Layer}` پیروی کند (`Amlak.Estate.Application` و…) چون `AhmadBase.Config` اسمبلی‌ها را با همین قرارداد reflection می‌کند. تغییر `CompanyName` یا `ContextName` در `appsettings.json` بدون تغییر `AssemblyName` باعث خطای زمان اجرا می‌شود.

## لایه‌ها

| پروژه | مسئولیت | وابسته به |
|---|---|---|
| `Amlak.Domain` | Aggregate ها، قواعد کسب‌وکار، Exception ها (پیام فارسی از `Resources/Message.resx`)، Repository interface ها | `Amlak.Event` |
| `Amlak.Event` | Domain Event ها | — |
| `Amlak.Application.Contract` | Command ها و قرارداد سرویس‌های بیرونی (`IPhotoStorage`, `IChannelGateway`, `IAdminNotifier`, `IJwtService`) | Domain |
| `Amlak.Application` | Command Handler ها | Contract، Persistence.EF |
| `Amlak.Application.Validators` | FluentValidation + `ValidationBehavior` (خطا ← ۴۲۲ با پیام فارسی) | Contract |
| `Amlak.Event.Handler` | واکنش به رویدادها: همگام‌سازی کانال، پاک کردن فایل عکس، پیامک به مدیر | Contract |
| `Amlak.Application.Query(.Contract)` | Query ها، Result ها و Handler ها | Read.Dapper |
| `Amlak.Persistence.EF` | `AmlakDbContext`، Configuration ها، Repository ها، Migration ها، `BotApiClient`، انبار عکس، JWT | Domain، Contract |
| `Amlak.Read.Dapper` | SQL خواندن (فقط آگهی منتشرشده برای عموم) | Query.Contract |
| `Amlak.Config` | `AutofacModule`، `DomainEventDispatchBehavior`، CorrelationId، Swagger | همه |
| `Amlak.Rest` | Minimal API Endpoint ها | Contract ها |
| `Amlak.ServiceHost` | `WebBuilder` فریم‌ورک | همه |

### جریان یک Command

```
Endpoint → ICommandBus → ValidationBehavior → DomainEventDispatchBehavior → Handler → SaveChanges
                                                     │
                                                     └─ پس از موفقیت: رویدادهای Aggregate ها → همهٔ Event Handler ها
```

`DomainEventDispatchBehavior` (کپی از Salmandan) همهٔ handler های یک رویداد را صدا می‌زند و خطای آن‌ها را لاگ می‌کند و می‌بلعد؛ بنابراین قطعی تلگرام هرگز انتشار آگهی را برنمی‌گرداند.

## Bounded Context ها و قواعد

### Listing — آگهی (`Property` + `PropertyPhoto`)

| قاعده | کجا |
|---|---|
| آگهی همیشه پیش‌نویس ساخته می‌شود | `Property.Create` |
| فروش: قیمت لازم؛ اجاره: رهن یا اجارهٔ ماهانه (یا هر دو). مبلغ نامربوط دور ریخته می‌شود (فروش ← رهن/اجاره null) | `Property.Pricing` |
| مبلغ منفی، متراژ ≤ ۰، و مختصات نصفه یا خارج بازه رد می‌شود | `Property.Apply` |
| نشانی صفحه از عنوان (حروف فارسی حفظ، ی/ک عربی یکسان) و یکتا: `عنوان`، `عنوان-2`… ؛ slug آگهی حذف‌شده هم رزرو می‌ماند | `Slug`، `ListingHandlers` |
| slug با ویرایش عنوان عوض نمی‌شود تا لینک ایندکس‌شده نشکند | `Property.Update` |
| `PublishedAt` فقط بار اول ثبت می‌شود | `Property.Publish` |
| چرخه: Draft ⇄ Published، هر دو → Closed، Closed → Published | `Publish/Unpublish/Close` |
| حداکثر ۲۰ عکس؛ ترتیب همیشه ۰..n-1؛ عکس اصلی = ترتیب صفر؛ ترتیب جدید باید دقیقاً همهٔ عکس‌ها را یک‌بار داشته باشد | `AddPhoto/ReorderPhotos/SetCoverPhoto` |
| فایل عکس: JPG/PNG/WebP، حداکثر ۵ مگابایت، امضای بایتی باید با پسوند بخواند | `PhotoFile.Validate` |
| عکس رد شده یا ذخیرهٔ ناموفق، فایلی روی دیسک جا نمی‌گذارد؛ فایل عکس حذف‌شده پس از ثبت موفق پاک می‌شود | `ListingHandlers` |
| alt خالی = عنوان آگهی | `Property.AltOrTitle` |
| حذف نرم؛ فایل عکس‌ها و پیام کانال‌ها با رویداد `PropertyDeletedEvent` پاک می‌شوند | `Property.Delete`، `PropertyDeletedEventHandler` |

### Publishing — کانال تلگرام و بله (`ChannelPost`)

رویدادهای `Published / Updated / Unpublished / Closed` ← `SyncPropertyChannelsCommand`:

| وضعیت آگهی | پست موجود؟ | کار |
|---|---|---|
| منتشرشده | خیر | `sendPhoto` با اولین عکس موجود روی دیسک، وگرنه `sendMessage` |
| منتشرشده | بله | `editMessageCaption` (پست عکس‌دار) یا `editMessageText`؛ «not modified» موفقیت است |
| غیرمنتشر/حذف‌شده | بله | `deleteMessage` و حذف رکورد؛ «not found» موفقیت است |

- یک پست برای هر (آگهی، کانال) — ایندکس یکتا.
- شکست: متن خطا (بدون توکن، حداکثر ۳۰۰ نویسه) روی پست ثبت می‌شود و `ChannelSyncFailedEvent` لاگ می‌شود؛ همگام‌سازی بعدی یا `POST /Admin/Channels/Properties/{id}/Sync` دوباره تلاش می‌کند.
- `HttpClient` کانال‌ها از `IHttpClientFactory` نمی‌آید، پس هیچ handler لاگی URL حاوی توکن را ثبت نمی‌کند. `ex.Message` هرگز ذخیره نمی‌شود.

### Lead — درخواست تماس (`ContactLead`) — جدید

- بازدیدکننده بدون ورود برای یک آگهی منتشرشده (یا به‌طور کلی) نام و موبایل می‌گذارد. درخواست برای آگهی پیش‌نویس «یافت نشد» می‌گیرد تا وجودش لو نرود.
- حداکثر ۳ درخواست از یک شماره در ۱۰ دقیقه (`TooManyLeadsException`).
- با ثبت، پیامک به `Platform:AdminPhones` می‌رود (`LeadSubmittedEventHandler`).
- وضعیت: New → Contacted → Closed، و New/Contacted → Spam. Closed و Spam پایانی‌اند.

### Identity — ورود با OTP (`User`)

- کد ۶ رقمی، ۵ دقیقه اعتبار، حداقل یک دقیقه فاصله بین دو درخواست برای یک شماره، و پس از ۵ تلاش اشتباه کد می‌سوزد.
- نقش هرگز از کلاینت گرفته نمی‌شود: فقط شماره‌های `Platform:AdminPhones` نقش `Admin` می‌گیرند و نقش در هر ورود و تمدید توکن با تنظیمات همگام می‌شود (حذف شماره = گرفتن دسترسی در تمدید بعدی، حداکثر پس از عمر توکن دسترسی).
- توکن تمدید ۳۰ روزه و در هر تمدید چرخانده می‌شود؛ خروج آن را باطل می‌کند.

## Endpoint ها

پایهٔ همه: `/api/v1`. مستندات کامل روی Swagger.

| گروه | مسیر | دسترسی |
|---|---|---|
| ورود | `POST /Auth/{SendOtp, VerifyOtp, Refresh, Logout}` · `PUT /Auth/Profile` | عمومی / کاربر |
| آگهی‌ها | `GET /Properties` (فیلتر: `deal, kind, city, district, minPrice, maxPrice, maxRent, minArea, maxArea, minBedrooms, sort, page, pageSize`) | عمومی |
| | `GET /Properties/Slug/{slug}` (۴۰۴ برای پیش‌نویس/بسته/ناموجود) · `GET /Properties/{id}/Similar` · `GET /Properties/Locations` · `GET /Properties/Sitemap` | عمومی |
| عکس | `GET /Photos/{fileName}` (کش یک‌ساله، نام فایل تصادفی و تغییرناپذیر) | عمومی |
| درخواست تماس | `POST /Leads` | عمومی |
| مدیریت آگهی | `GET/POST /Admin/Properties` · `GET/PUT/DELETE /Admin/Properties/{id}` · `PUT .../{id}/{Publish, Unpublish, Close}` | Admin |
| مدیریت عکس | `POST .../{id}/Photos` (multipart: `file`, `alt`) · `DELETE .../Photos/{photoId}` · `PUT .../Photos/Order` · `PUT .../Photos/{photoId}/{Cover, Alt}` | Admin |
| کانال‌ها | `GET /Admin/Channels/Properties/{id}` · `POST /Admin/Channels/Properties/{id}/Sync` · `GET /Admin/Channels/Failed` | Admin |
| درخواست‌ها | `GET /Admin/Leads` · `GET /Admin/Leads/Stats` · `GET /Admin/Leads/{id}` · `PUT /Admin/Leads/{id}/Status` | Admin |

کدهای وضعیت: ۴۰۱ ورود نکرده، ۴۰۳ نقش کافی نیست، ۴۰۴ ناموجود، ۴۲۲ ورودی نامعتبر یا نقض قاعدهٔ کسب‌وکار (پیام فارسی در `errors.exception`).

## پیکربندی

| کلید | کاربرد |
|---|---|
| `ConnectionStrings:CommandConnection` / `QueryConnection` | SQL Server برای EF و Dapper |
| `JWTOptions:Secret` | کلید امضای JWT — فقط user-secrets/متغیر محیطی |
| `Platform:AdminPhones` | شماره‌های مدیر (نقش Admin + پیامک درخواست تماس) |
| `Site:BaseUrl` | آدرس عمومی سایت برای لینک داخل پست کانال |
| `Storage:PhotoPath` | پوشهٔ عکس‌ها روی سرور (پیش‌فرض `storage/photos`) |
| `Storage:PhotoUrlPrefix` | پیشوند آدرس عمومی عکس (پیش‌فرض `/api/v1/Photos/`؛ برای CDN عوض شود) |
| `Telegram:BotToken/ChatId/ApiBase`، `Bale:BotToken/ChatId/ApiBase` | کانال‌ها؛ بدون توکن یا ChatId غیرفعال |
| `Sms:*` | پیامک (sms.ir) |
| `UseFakeServices` | `true` در Development: پیامک واقعی ارسال نمی‌شود |
| `NHilo` | شناسه‌سازی HiLo برای Aggregate Root ها (جدول `HiLoIds` خودکار ساخته می‌شود) |

## تست

| پروژه | پوشش |
|---|---|
| `Test/Amlak.Domain.Tests` (۳۱) | قیمت‌گذاری فروش/اجاره، چرخهٔ وضعیت، رویدادها، عکس‌ها و ترتیب، slug، امضای فایل، وضعیت درخواست تماس، OTP |
| `Test/Amlak.Application.Tests` (۳۱) | جریان کامل آگهی روی SQLite با Event Handler های واقعی و Bot API ساختگی (ارسال یک‌باره، ویرایش، حذف، شکست و تلاش دوباره، حذف آگهی)، درخواست تماس و محدودیت نرخ، ورود OTP و چرخش توکن، Validator ها، نشت‌نکردن توکن، `DomainEventDispatchBehavior` |

پوشش‌داده‌نشده: SQL های Dapper (نحو SQL Server دارند و روی SQLite اجرا نمی‌شوند) و Endpoint ها — راه‌اندازی Host، Swagger، ۴۰۱ مسیرهای مدیر، ۴۲۲ اعتبارسنجی و ۴۰۴ مسیر عکس با درخواست HTTP دستی بررسی شده‌اند، ولی نه روی SQL Server واقعی.

## مهاجرت سایت Razor به API (پیشنهاد)

1. صفحه‌های عمومی (`/املاک`، `/ملک/{slug}`، sitemap) به `GET /Properties*` وصل شوند و `AppDbContext` از آن‌ها حذف شود.
2. فرم «درخواست تماس» صفحهٔ جزئیات به `POST /Leads` وصل شود (PUB-06).
3. پنل مدیریت به `/Admin/*` با ورود OTP منتقل شود؛ سپس Identity و `ChannelPublisher` سایت Razor حذف شوند.
4. داده‌های دیتابیس `Amlak` قدیم با یک اسکریپت به `AmlakApi` منتقل شوند (فایل عکس‌ها از `wwwroot/uploads` به `storage/photos`).
