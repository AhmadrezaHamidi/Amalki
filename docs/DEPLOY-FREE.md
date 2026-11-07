# میزبانی رایگان روی MonsterASP.NET

سایت (ASP.NET Core 9 + SQL Server) را می‌شود رایگان روی [MonsterASP.NET](https://www.monsterasp.net) بالا آورد.
خروجی: آدرسی مثل `https://royalfinehomes.runasp.net` با حالت آزمایشی روشن (کد پیامکی روی صفحه، ورود یک‌کلیکی مدیر و کاربر).

## چرا MonsterASP

| سرویس | SQL Server رایگان | دیسک ماندگار برای عکس‌ها | خواب رفتن | کارت بانکی |
|---|---|---|---|---|
| **MonsterASP.NET** (پیشنهاد) | دارد (حدود ۱ گیگ) | دارد | استخر IIS بعد از بیکاری می‌خوابد، بیدار شدن چند ثانیه | لازم نیست |
| Render | ندارد (فقط Postgres، ۳۰ روزه) | ندارد، با هر ری‌استارت پاک می‌شود | بعد از ۱۵ دقیقه، بیدار شدن حدود ۱ دقیقه | لازم نیست |
| Azure App Service F1 + Azure SQL رایگان | دارد | دارد | ۶۰ دقیقه CPU در روز | لازم است و با شمارهٔ ایران باز نمی‌شود |
| Fly.io / Railway / Koyeb | ندارد | محدود | — | معمولاً لازم یا فقط آزمایشی |

پروژه فقط با SQL Server کار می‌کند (view های ایندکس‌دار، migration ها)، پس سرویسی که SQL Server رایگان و دیسک ماندگار بدهد بهترین گزینه است.

محدودیت‌های پلن رایگان (ممکن است عوض شود، روی سایت خودشان چک کنید): دامنهٔ اختصاصی ندارد (فقط `*.runasp.net`)، یک دیتابیس حدود ۱ گیگ، منابع کم. برای دمو و تست مشتری کافی است، نه برای بار واقعی.

## کارهایی که فقط خودتان می‌توانید بکنید (یک بار، حدود ۱۵ دقیقه)

### ۱. ساخت حساب و سایت
1. در [monsterasp.net](https://www.monsterasp.net) ثبت‌نام کنید (ایمیل کافی است). اگر سایت از ایران باز نشد با VPN وارد شوید.
2. در Control Panel یک **Website** رایگان بسازید، مثلاً با نام `royalfinehomes` → آدرس `https://royalfinehomes.runasp.net`.
3. نسخهٔ .NET سایت را روی **.NET 9** بگذارید (اگر گزینه‌اش بود).

### ۲. ساخت دیتابیس
1. در Control Panel بخش **Databases** → یک دیتابیس **MSSQL** بسازید.
2. اطلاعات اتصال (Server، Database، Login، Password) را بردارید و رشتهٔ اتصال را این شکلی بسازید:

```
Server=<server>;Database=<db>;User Id=<login>;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=False
```

جدول‌ها لازم نیست ساخته شوند؛ سایت در اولین اجرا migration ها را خودش اعمال می‌کند.

### ۳. روشن کردن Web Deploy
در Control Panel سایت → **Deploy** → **WebDeploy / Visual Studio access** را فعال کنید. چهار مقدار نشان می‌دهد:
Website name، Server (مثل `https://siteXXXX.siteasp.net:8172`)، Username، Password.

### ۴. گذاشتن رمزها در GitHub
در مخزن: **Settings → Secrets and variables → Actions**.

**Secrets** (تب Secrets):

| نام | مقدار |
|---|---|
| `MONSTERASP_WEBSITE_NAME` | Website name از مرحلهٔ ۳ |
| `MONSTERASP_SERVER` | Server از مرحلهٔ ۳ (با `:8172`) |
| `MONSTERASP_USERNAME` | Username از مرحلهٔ ۳ |
| `MONSTERASP_PASSWORD` | Password از مرحلهٔ ۳ |
| `MONSTERASP_DB_CONNECTION` | رشتهٔ اتصال مرحلهٔ ۲ |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | اختیاری: حساب مدیر واقعی (رمز حداقل ۱۰ کاراکتر) |
| `TELEGRAM_BOT_TOKEN` | اختیاری: توکن ربات تلگرام |
| `TELEGRAM_WEBHOOK_SECRET` | اختیاری: یک رشتهٔ تصادفی ۱۶+ کاراکتری برای Webhook ربات |

**Variables** (تب Variables):

| نام | مقدار |
|---|---|
| `SITE_URL` | مثلاً `https://royalfinehomes.runasp.net` |
| `DEMO_MODE` | `true` (پیش‌فرض). پیش از راه‌اندازی واقعی `false` |
| `SEED_DEMO` | `true` (پیش‌فرض) چند آگهی نمونه می‌سازد |
| `MONSTERASP_AUTO_DEPLOY` | `true` اگر می‌خواهید هر merge در main خودکار منتشر شود |
| `TELEGRAM_CHAT_ID` / `TELEGRAM_ADMIN_CHAT_ID` | اختیاری |

هیچ رمزی داخل کد نیست؛ workflow هنگام انتشار فایل `appsettings.Production.json` را فقط روی سرور می‌سازد.

### ۵. انتشار
در GitHub: **Actions → Deploy to MonsterASP → Run workflow**. حدود ۴ دقیقه طول می‌کشد. آخرین مرحله (`Smoke test`) آدرس `/healthz` را چک می‌کند؛ سبز یعنی سایت و دیتابیس بالا هستند.

## بعد از انتشار

- حالت آزمایشی: نوار زرد بالای سایت → «ورود مدیر» یا «ورود کاربر عادی». کد ورود روی همان صفحه نشان داده می‌شود (پیامک واقعی فرستاده نمی‌شود).
- عکس‌های آگهی روی دیسک خود سایت (`wwwroot/uploads`) ذخیره می‌شوند و انتشار بعدی پاکشان نمی‌کند. کلیدهای ورود در `App_Data/keys` هستند تا با ری‌استارت کسی از حساب بیرون نیفتد.
- اولین بازدید بعد از مدتی بیکاری چند ثانیه کندتر است (استخر IIS بیدار می‌شود).
- ربات تلگرام: سرور خارج از ایران است، پس `api.telegram.org` مستقیم در دسترس است. در `/Admin/Bots` توکن را تست کنید و Webhook را ثبت کنید (به `TELEGRAM_WEBHOOK_SECRET` در Secrets نیاز دارد).

## اگر خطا داد

| نشانه | علت محتمل |
|---|---|
| مرحلهٔ `Check settings` قرمز | یکی از Secrets/Variables بالا گذاشته نشده |
| `Deploy with Web Deploy` با 401 | Username/Password مرحلهٔ ۳ اشتباه است یا Web Deploy روشن نیست |
| `Smoke test` با 503 | سایت بالا آمده ولی به دیتابیس وصل نمی‌شود: رشتهٔ اتصال را چک کنید |
| `Smoke test` با 500.30 | برنامه بالا نیامد: در Control Panel لاگ stdout یا نسخهٔ .NET را چک کنید |

راه‌اندازی کامل با Docker روی سرور خودتان: [DEPLOY.md](DEPLOY.md).
