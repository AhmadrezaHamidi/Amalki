# راه‌اندازی روی سرور ابری آروان (ArvanCloud)

کل سایت (برنامه و SQL Server) روی یک سرور ابری آروان با Docker اجرا می‌شود. عکس‌ها روی دیسک همان سرور می‌مانند.
تقریباً همهٔ کارها را اسکریپت `deploy/arvan/install.sh` انجام می‌دهد.

## ۱. ساخت سرور در پنل آروان (کار شما، حدود ۵ دقیقه)

1. در [پنل آروان](https://panel.arvancloud.ir) به **ابر/سرور ابری** بروید و **ایجاد سرور** را بزنید.
2. منطقه (دیتاسنتر) فرقی ندارد، مثلاً تهران.
3. سیستم‌عامل: **Ubuntu 24.04** (یا 22.04).
4. مشخصات: حداقل **۲ هسته و ۴ گیگ رم**، دیسک ۴۰ گیگ یا بیشتر. SQL Server با رم کمتر به‌سختی کار می‌کند. با ۲ گیگ، اسکریپت swap اضافه می‌کند ولی کند می‌شود.
5. شبکه: **IP عمومی** داشته باشد.
6. دسترسی: رمز root را خودتان بگذارید یا کلید SSH اضافه کنید.
7. در گروه امنیتی/فایروال، پورت‌های **22** (SSH) و **80** (وب) باز باشند.
8. بعد از ساخت، **IP سرور** را بردارید.

## ۲. وصل شدن به سرور

- ویندوز ۱۰/۱۱: PowerShell را باز کنید: `ssh root@IP_SERVER`
- یا از پنل آروان دکمهٔ **کنسول** همان سرور.

## ۳. نصب (یک دستور)

روی سرور:

```bash
git clone https://github.com/AhmadrezaHamidi/Amalki.git
cd Amalki
sudo bash deploy/arvan/install.sh
```

اسکریپت این کارها را می‌کند:
1. Docker را از مخزن اوبونتو نصب می‌کند (مخزن رسمی Docker به ایران سرویس نمی‌دهد، ولی mirror اوبونتوی آروان کار می‌کند).
2. اگر رم کم باشد swap می‌سازد.
3. فایل `deploy/arvan/.env` را با رمز تصادفی دیتابیس می‌سازد (حالت آزمایشی روشن، آگهی‌های نمونه روشن).
4. ایمیج SQL Server را می‌گیرد و ایمیج سایت را روی سرور می‌سازد.
5. سایت را روی پورت 80 بالا می‌آورد و تا جواب دادن `/healthz` صبر می‌کند.

در پایان آدرس را چاپ می‌کند: `http://IP_SERVER`. نوار زرد بالای سایت → «ورود مدیر» / «ورود کاربر عادی».

## اگر گفت «Could not download ...» (سرورهای مایکروسافت از ایران در دسترس نیستند)

ابرک‌های داخل ایران معمولاً به `mcr.microsoft.com` (ایمیج SQL Server و .NET) و NuGet راه ندارند، ولی GitHub در دسترس است.
پس ایمیج‌ها را GitHub Actions می‌سازد و در GitHub Container Registry همین حساب می‌گذارد:

1. در GitHub: **Actions → Arvan images → Run workflow** (با هر تغییر در `src/` روی main هم خودکار اجرا می‌شود). حدود ۵ دقیقه.
2. یک توکن بسازید: GitHub → Settings → Developer settings → **Personal access tokens (classic)** → Generate، فقط تیک **read:packages**. توکن را کپی کنید (فقط یک بار نشان داده می‌شود).
3. روی سرور:
   ```bash
   cd ~/Amalki && git pull
   docker login ghcr.io -u AhmadrezaHamidi        # Password: همان توکن
   IMAGE_REGISTRY=ghcr.io/ahmadrezahamidi sudo -E bash deploy/arvan/install.sh
   ```
   اسکریپت هر دو ایمیج را از GitHub می‌گیرد و چیزی از مایکروسافت دانلود نمی‌کند. `IMAGE_REGISTRY` در `.env` ذخیره می‌شود، پس دفعهٔ بعد فقط `sudo bash deploy/arvan/install.sh` کافی است.

بستهٔ ایمیج‌ها خصوصی است؛ فقط با توکن خودتان دانلود می‌شوند.

راه دیگر (اگر GitHub هم در دسترس نبود): روی کامپیوتر خودتان با Docker Desktop و VPN، در پوشهٔ پروژه (WSL یا Git Bash) `bash deploy/arvan/ship-images.sh root@IP_SERVER` و بعد دوباره install.sh روی سرور.

## تنظیمات بعدی

فایل `deploy/arvan/.env` روی سرور را ویرایش کنید (`nano deploy/arvan/.env`) و دوباره `sudo bash deploy/arvan/install.sh` بزنید.

| تنظیم | توضیح |
|---|---|
| `SITE_URL` | بعد از وصل کردن دامنه: `https://royalfinehomes.ir` |
| `DEMO_MODE` / `SEED_DEMO` | پیش از راه‌اندازی واقعی `false` |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | حساب مدیر واقعی (رمز ۱۰+ کاراکتر) |
| `SMS_PROVIDER=Kavenegar` + `SMS_API_KEY` + `SMS_TEMPLATE` | پیامک واقعی (حالت آزمایشی خودبه‌خود خاموش می‌شود) |
| `BALE_*` | ربات بله در ایران مستقیم کار می‌کند |
| `TELEGRAM_*` | تلگرام در ایران فیلتر است؛ `TELEGRAM_API_BASE` باید یک واسط خودتان خارج از ایران باشد ([CHANNELS.md](CHANNELS.md)) |

## دامنه و HTTPS

ساده‌ترین راه: دامنه را در **CDN آروان** اضافه کنید، رکورد A را روی IP سرور بگذارید (پروکسی روشن) و در بخش HTTPS گواهی رایگان را فعال کنید. بعد `SITE_URL=https://دامنه` و دوباره install.sh.

## نگهداری

| کار | دستور (در پوشهٔ `Amalki/deploy/arvan`) |
|---|---|
| لاگ سایت | `docker compose logs -f web` |
| به‌روزرسانی به آخرین نسخهٔ main | `cd ~/Amalki && git pull && sudo bash deploy/arvan/install.sh` (با `IMAGE_REGISTRY` ایمیج تازه از GitHub گرفته می‌شود) |
| ری‌استارت | `docker compose restart web` |
| پشتیبان دیتابیس | `sudo bash backup.sh` (فایل در `deploy/arvan/backups/`) |

اگر ایمیج سایت را با `ship-images.sh` فرستاده‌اید، برای به‌روزرسانی هم همان را دوباره اجرا کنید (سرور خودش ایمیج نمی‌سازد). برای ساخت روی خود سرور: `docker rmi amalki-web:shipped`.

داده‌ها در volume های Docker هستند (`amalki_db-data`، `amalki_uploads`) و با به‌روزرسانی یا ری‌استارت پاک نمی‌شوند.
