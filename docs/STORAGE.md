# ذخیره‌سازی فایل (عکس آگهی‌ها)

عکس‌هایی که در پنل آپلود می‌شوند، پس از بررسی نوع و اندازه (JPG/PNG/WebP تا ۵ مگابایت، با بررسی محتوای فایل)، در یکی از دو محل ذخیره می‌شوند:

| `Storage:Provider` | محل | نشانی عکس |
|---|---|---|
| `Local` (پیش‌فرض) | پوشهٔ `wwwroot/uploads` روی همان سرور | `/uploads/<نام>` |
| `Minio` | سطل (bucket) در MinIO یا هر سرویس سازگار با S3 | `<PublicBaseUrl>/<Prefix>/<نام>` |

عکس‌های قدیمیِ روی دیسک بعد از رفتن به MinIO هم نمایش داده می‌شوند، در کانال‌ها ارسال می‌شوند و قابل حذف‌اند؛ فقط آپلودهای تازه به MinIO می‌روند.

## راه‌اندازی MinIO

```bash
cd src/Amlak.Web
dotnet user-secrets set "Storage:Provider" "Minio"
dotnet user-secrets set "Storage:Minio:Endpoint" "minio.example.ir:9000"   # بدون http://
dotnet user-secrets set "Storage:Minio:AccessKey" "<کلید>"
dotnet user-secrets set "Storage:Minio:SecretKey" "<رمز>"
dotnet user-secrets set "Storage:Minio:Bucket" "amlak"            # پیش‌فرض amlak
dotnet user-secrets set "Storage:Minio:UseSsl" "true"             # پیش‌فرض true
dotnet user-secrets set "Storage:Minio:PublicBaseUrl" "https://cdn.example.ir/amlak"   # اختیاری
```

در سرور (متغیر محیطی): `Storage__Provider=Minio`، `Storage__Minio__Endpoint=...` و مانند آن.

- اگر `Provider` روی `Minio` باشد ولی Endpoint یا کلیدها خالی باشند، برنامه هنگام شروع خطا می‌دهد (به‌جای اینکه آپلودها بی‌صدا شکست بخورند).
- `PublicBaseUrl` نشانی‌ای است که مرورگر بازدیدکننده عکس را از آن می‌گیرد. اگر خالی باشد `http(s)://Endpoint/Bucket` استفاده می‌شود. اگر جلوی MinIO یک CDN یا reverse proxy دارید، همان را بگذارید.

## دسترسی عمومی به عکس‌ها

عکس آگهی باید بدون ورود قابل دیدن باشد.

- اگر سطل وجود نداشته باشد، برنامه آن را می‌سازد و **فقط اجازهٔ دانلود عمومی پوشهٔ عکس‌ها** (`<Bucket>/<Prefix>/*`) را می‌دهد؛ فهرست کردن فایل‌ها و نوشتن عمومی بسته می‌ماند.
- اگر سطل را خودتان از قبل ساخته‌اید، سیاست آن دست نمی‌خورد. یک بار با `mc` اجازهٔ دانلود بدهید:

```bash
mc alias set amlak https://minio.example.ir:9000 <کلید> <رمز>
mc anonymous set download amlak/amlak/uploads
```

## راستی‌آزمایی

نوشتن، خواندن، حذف و دانلود عمومی با یک سرور سازگار با S3 (moto) امتحان شده است؛ با سرور واقعی MinIO امتحان نشده است.
