# راه‌اندازی کامل با Docker (سایت + SQL Server + MinIO)

```bash
cp .env.example .env      # همهٔ رمزها را عوض کنید
docker compose up -d --build
```

- سایت: `http://localhost:8080` (طرح **دلتا** پیش‌فرض است: `Ui__Variant=delta`)
- کنسول MinIO: `http://localhost:9001`
- عکس‌های آگهی در سطل `amlak` روی MinIO ذخیره می‌شوند؛ برنامه سطل را می‌سازد و فقط پوشهٔ `uploads/` را برای دانلود عمومی باز می‌کند.
- migrationها هنگام شروع اعمال می‌شوند (`Database__Migrate=true`). `SEED_DEMO=true` چند آگهی نمونه می‌سازد؛ روی سرور واقعی آن را `false` کنید.
- ورود کاربران با کد پیامکی است (`/ورود`). با `SMS_PROVIDER=Log` کد فقط در لاگ کانتینر `web` چاپ می‌شود (`docker compose logs web`)؛ برای پیامک واقعی `Kavenegar` و کلید و الگو را بگذارید.
- مدیر با ایمیل و رمز (`ADMIN_EMAIL`/`ADMIN_PASSWORD`) از `/Account/Login` وارد می‌شود.

## ایمیج MinIO

ایمیج رسمی `minio/minio` دیگر در Docker Hub منتشر نمی‌شود. یکی از این دو:

1. ایمیج خودتان را از سورس رسمی بسازید (`go install github.com/minio/minio@latest` و یک Dockerfile ساده) و در رجیستری خودتان بگذارید.
2. آدرس یک mirror مطمئن را در `.env` بدهید: `MINIO_IMAGE=registry.example.ir/minio:latest`.

## پشت دامنه و HTTPS

یک reverse proxy (nginx/Caddy) جلوی `web:8080` بگذارید و `SITE_URL=https://ashiyaneh.ir` کنید. برای عکس‌ها یا زیردامنهٔ جدا (`PHOTOS_URL=https://cdn.ashiyaneh.ir/amlak`) یا مسیر `/amlak/` روی همان proxy به `minio:9000`.

## راستی‌آزمایی (۲۰۲۶-۱۱-۰۴)

در محیط توسعه، سایت منتشرشده روی SQL Server 2022 و MinIO واقعی (ساخته‌شده از سورس رسمی) اجرا شد: ورود با کد پیامکی، ثبت آگهی با عکس، ساخت سه نسخهٔ WebP و خواندن عمومی عکس از MinIO سرتاسری کار کرد. ساخت ایمیج با `docker compose` در آن محیط امتحان نشد.
