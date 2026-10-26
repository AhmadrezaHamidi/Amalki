# آشیانه (Amlak) — سایت خرید، فروش و اجارهٔ ملک

سایت آگهی املاک با پنل مدیریت. آگهی‌ها (فروش، رهن و اجاره) از پنل ثبت می‌شوند و با انتشار، خودکار در کانال تلگرام و ربات بله هم قرار می‌گیرند. سئو و GEO (دیده‌شدن در موتورهای جست‌وجوی هوش مصنوعی) بخشی از طراحی است.

> وضعیت لحظه‌ای کارها: [PLANNER.md](PLANNER.md)

## مستندات

| فایل | محتوا |
|---|---|
| [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) | نیازمندی‌های کارکردی و غیرکارکردی، با وضعیت هر مورد |
| [docs/BACKEND.md](docs/BACKEND.md) | بک‌اند DDD: لایه‌ها، Aggregate ها و قواعد، Endpoint ها، پیکربندی |
| [docs/FRONTEND.md](docs/FRONTEND.md) | فرانت React: ساختار، مسیرها، اجرا، استقرار با nginx، محدودیت سئوی SPA |
| [docs/ADMIN-GUIDE.md](docs/ADMIN-GUIDE.md) | راهنمای استفاده از پنل مدیریت |
| [docs/CHANNELS.md](docs/CHANNELS.md) | راه‌اندازی ربات تلگرام و بله، تنظیمات، رفع خطا |
| [docs/SEO-GEO.md](docs/SEO-GEO.md) | سئو و GEO: آنچه هست و آنچه برنامه‌ریزی شده |
| [docs/DESIGN.md](docs/DESIGN.md) | تصمیم‌های طراحی و سیستم طراحی |
| [design-system/amlak/MASTER.md](design-system/amlak/MASTER.md) | سیستم طراحی تولیدشده (منبع حقیقت رنگ و فونت) |
| [design-options.html](design-options.html) | پیش‌نمایش سه طرح پیشنهادی (طرح ۱ انتخاب شد) |

## استک

**بک‌اند (API):** .NET 8 · DDD + CQRS با ساختار همیشگی (`AhmadBase.*`) · Minimal API · EF Core 8 (نوشتن) · Dapper (خواندن) · Autofac · FluentValidation · JWT با ورود OTP · SQL Server — جزئیات: [docs/BACKEND.md](docs/BACKEND.md)

**فرانت‌اند:** React 18 · Vite · TypeScript · Tailwind · React Query · Zustand — جزئیات: [docs/FRONTEND.md](docs/FRONTEND.md)

## ساختار پوشه‌ها

```
Src/
├── Domain/
│   ├── Amlak.Domain            ← Aggregate ها (Property, ChannelPost, ContactLead, User)، Args، Enums، Exceptions، Repository interfaces
│   └── Amlak.Event             ← Domain Event ها
├── Application/
│   ├── Amlak.Application.Contract        ← Command ها + قرارداد سرویس‌ها (عکس، کانال، پیامک مدیر، JWT)
│   ├── Amlak.Application                 ← Command Handler ها
│   ├── Amlak.Application.Query.Contract  ← Query ها + Result ها
│   ├── Amlak.Application.Query           ← Query Handler ها
│   ├── Amlak.Application.Validators      ← FluentValidation + ValidationBehavior
│   └── Amlak.Event.Handler               ← Event Handler ها (همگام‌سازی کانال، پیامک درخواست تماس)
├── Infrastructure/
│   ├── Amlak.Persistence.EF    ← AmlakDbContext، Configurations، Repositories، Migrations، Bot API، انبار عکس
│   ├── Amlak.Read.Dapper       ← Read Repository ها (سمت Query)
│   └── Amlak.Config            ← AutofacModule، DomainEventDispatchBehavior، Middlewares، Swagger
├── Rest/Amlak.Rest             ← Minimal API Endpoints + sitemap/robots/llms
└── Host/
    ├── Amlak.Option
    └── Amlak.ServiceHost       ← نقطهٔ شروع API
Front/                          ← React: سایت عمومی + پنل مدیریت
Test/
├── Amlak.Domain.Tests          ← قواعد دامین
└── Amlak.Application.Tests     ← Handler ها روی SQLite، Validator ها، Bot API ساختگی، فایل‌های سئو
Nuget/                          ← پکیج‌های AhmadBase.* (منبع محلی در nuget.config)
docs/                           مستندات
design-system/                  سیستم طراحی
```

## اجرا

پیش‌نیاز: .NET SDK 8، Node 20+، SQL Server (پیش‌فرض `localhost\SQLEXPRESS`، دیتابیس `AmlakApi`).

```bash
# API
cd Src/Host/Amlak.ServiceHost
dotnet user-secrets set "JWTOptions:Secret" "<کلید-تصادفی-حداقل-۳۲-نویسه>"
dotnet user-secrets set "Platform:AdminPhones:0" "09xxxxxxxxx"   # شمارهٔ مدیر
cd ../../..
dotnet ef database update --project Src/Infrastructure/Amlak.Persistence.EF --startup-project Src/Host/Amlak.ServiceHost
dotnet run --project Src/Host/Amlak.ServiceHost                  # http://localhost:5300/swagger

# فرانت (ترمینال دیگر)
cd Front
npm install
npm run dev                                                      # http://localhost:5173
```

ورود مدیر: `http://localhost:5173/login` با شماره‌ای که در `Platform:AdminPhones` گذاشتید. در Development پیامک ارسال نمی‌شود و کد زیر فرم نشان داده می‌شود.

## تست

```bash
dotnet test Test/Amlak.Domain.Tests        # ۳۱ تست قواعد دامین
dotnet test Test/Amlak.Application.Tests   # ۳۵ تست Handler/Validator/Bot API/سئو
cd Front && npm test                       # ۷ تست کمکی‌های فرانت
```

هیچ‌کدام به اینترنت، SQL Server یا توکن واقعی نیاز ندارند.

## امنیت پیکربندی

هیچ رمز یا توکنی در مخزن ذخیره نمی‌شود. مقادیر حساس (`JWTOptions:Secret`, `Sms:ApiKey`, `Telegram:*`, `Bale:*`) فقط با user-secrets یا متغیر محیطی تنظیم می‌شوند. جزئیات: [docs/CHANNELS.md](docs/CHANNELS.md).
