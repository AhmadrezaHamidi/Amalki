# Amlak — Real estate site (sale + rent)

## Current status
- Design chosen: Option 1 "Modern & Trustworthy" (teal/blue, light, Vazirmatn, RTL). Mockup: `design-options.html`. Design system: `design-system/amlak/MASTER.md`.
- Stack: ASP.NET Core 9 Razor Pages + EF Core (SQL Server) + Identity. Solution builds clean.

## Done
- Design options (3) built and one chosen.
- Solution scaffold `src/Amlak.Web` (net9, EF Core 9, Identity).
- Domain (Property, PropertyPhoto, ChannelPost), AppDbContext, `Initial` migration applied to localhost\SQLEXPRESS / DB Amlak.
- Public RTL pages with design tokens + base SEO: home, `/املاک` list with filters + paging, `/ملک/{slug}` detail with canonical, OG and JSON-LD (RealEstateListing). Dev seed with 4 sample listings. Verified: build 0 errors, pages return 200 and render.
- Admin panel `/Admin` (role Admin policy): login `/Account/Login`, list, create/edit, photo upload (JPG/PNG/WebP <=5MB, magic-byte check), delete photo, publish toggle, delete. Admin user seeded from config Admin:Email / Admin:Password (dev values in user-secrets). Verified by HTTP: anon->redirect, wrong pw rejected, login ok, CSRF required, fake image rejected, valid upload served.
- Channel publisher (Telegram + Bale, same Bot API client, `Services/Publishing`): publish on go-live (photo+caption or text), edit on change, delete message on unpublish/delete, errors stored per channel and retried via admin button; token scrubbed from errors; HttpClient URL logging silenced. Migration `ChannelPostUniqueAndPhoto`. 8 xunit tests (`tests/Amlak.Tests`) pass against a stub Bot API + SQLite. NOT yet verified against the real Telegram/Bale servers.

- SEO/GEO files: `/sitemap.xml`, `/robots.txt` (AI crawlers allowed), `/llms.txt`; JSON-LD RealEstateAgent+WebSite (home), BreadcrumbList (list, detail); login noindex; base URL from `Site:BaseUrl`. 6 new xunit tests (14 total pass); endpoints verified over HTTP. Not yet: Search Console submission, FAQPage, WebP/srcset, city/district pages.

- UI/UX v2 redesign: design canvas (`design/canvas/`, 4 web pages + 3 mobile-app screens), then backend + frontend to match. Lead entity + `Leads` migration, `/تماس` contact form (mobile validation, honeypot, 5 posts/10 min/IP rate limit), `/Admin/Leads`, listing sort + beds/area filters + active-filter chips, similar listings, OSM map embed, favorites `/نشان‌شده` (localStorage), FAQPage JSON-LD, Persian 404, admin dashboard stats. Bots: inline "view listing" button on posts (toggle `{Section}:InlineButton`), lead alert to `{Section}:AdminChatId`. 24 tests pass. Visually checked at 390px and 1440px against SQLite locally (SQL Server migration not applied in this environment).

- Admin panel v2 + support desk + storage (2026-10-26): canvas boards AdminDashboard/AdminTickets/Support; `/Admin` dashboard, listings at `/Admin/Listings`, sidebar with pending counts; tickets (`Ticket`/`TicketMessage`, migration `Tickets`): public `/پشتیبانی` new + track (code+mobile), `/پشتیبانی/{code}` thread, `/Admin/Tickets` inbox with reply/close; admin bot alerts for tickets (`AdminNotifier`). Storage: `IFileStore` with Local + MinIO (`Storage:Provider`, docs/STORAGE.md), verified against moto S3, not real MinIO. Channels: albums via sendMediaGroup, repost on photo change, delete all album messages (migration `ChannelAlbums`). 29 tests pass.

- SEO speed pass (2026-10-26, SEO-07): OutputCache for home/list/detail/sitemap/robots/llms (10 min, evicted on any Property/PropertyPhoto save via `ListingCacheInterceptor`; admins never cached); detail contact form fetches its antiforgery token from `/antiforgery-token` so the page can be cached. Photos: WebP copies at 480/960/1600 on upload (SkiaSharp, EXIF rotation baked in), `<picture>` srcset everywhere, real width/height; migration `PhotoVariants`. Self-hosted variable Vazirmatn with preload (no Google Fonts). `/uploads` immutable cache headers. Absolute og:image/JSON-LD images, canonical from `Site:BaseUrl`. 35 tests pass. Checked end to end against SQLite: second request served from cache, upload creates 3 WebP files and evicts the page, token flow 302 / no token 400.
- Divar-style structure (2026-10-26): landing pages `/املاک/{city}/{category}/{district}` (Services/Taxonomy.cs, 12 categories, "ایران" = all cities), old `?deal=&city=` links 301 to them, self-canonical + breadcrumb per landing, district/city link blocks, avg price per m² (Melkradar idea). Property fields HasElevator/HasStorage/HasBalcony/Deed/Convertible (migration `ListingFeatures`), filters for price/area ranges, max rent, building age, amenities, has photo. Sitemap lists landing pages with listings. Comparison doc: https://claude.ai/artifact/2oDapv7FwxA1Am4PqcDaCb. 41 tests pass (with SEO pass).
- UI/UX v3 (2026-10-26): installable PWA (manifest, icons, service worker with offline page, install button), mobile bottom tab bar, phone home page without hero photo and with swipe rails, admin stat grid fills the row. Offline navigation verified in headless Chromium. 31 tests pass. Docs: docs/DESIGN.md section 3.

- Photo order + WebP backfill (2026-10-26, ADM-09): admin Edit page lets you move a photo earlier/later or make it the cover (`PhotoOrder`, renumbers SortOrder, re-syncs channel album); dashboard button makes WebP copies for photos uploaded before copies existed (`PhotoStorage.BackfillAsync`, 40 per click, unreadable/missing originals marked done). 41 tests pass; checked in the browser against SQLite.
- Dark mode (2026-10-26): automatic via prefers-color-scheme for site, admin and offline page; dark tokens in site.css, `--on-primary` for text on primary, theme-color per scheme. Checked at 390px and 1440px.
- UI/UX v4 (2026-10-26): Divar-style redesign (direction A on the design canvas). New tokens, header search box (`q` filter on title/district/city), center post button in tab bar, row listing card, sticky filter bar + full filter panel (full-screen sheet on phones), detail key facts/price table/amenities/safety tip, new home (category grid, busy districts, latest), admin pending count. 55 tests pass. Checked at 390px and 1440px, light and dark. Docs: docs/DESIGN.md section 4.
- SMS alerts and bulk SMS (2026-10-27, rebuilt from draft PR #3 on the current domain): `/پیگیری` lets a visitor log in with the same SMS code as `/ثبت-آگهی`, save up to 5 searches (deal, kind, city, districts, budget, bedrooms) and cancel them or stop all SMS; `AlertService` runs from the outbox on ListingPublished/ListingChanged and texts a matching listing once when it goes live after the sign-up, then on each drop below the lowest price told (rent compared by full deposit, 3%/month); `/Admin/Sms` bulk campaigns to subscribers or a pasted list, preview with part count, scheduled sends in Tehran time (`CampaignWorker`), «لغو۱۱» footer, opted-out numbers skipped, every text in `SmsLogs`; short link `/p/{id}` for SMS. Price display per listing (full + per m², total only, negotiable) on site, JSON-LD and channel posts. Kavenegar texts use `Sms:Sender`. Migration `SmsAlertsAndPriceDisplay`. 104 tests pass; checked end to end against SQL Server 2022.

## Next task
- Real-bot check: set Telegram/Bale tokens + chat ids in user-secrets, publish one listing, confirm message appears, edit, unpublish. (Needs user's bot tokens; Telegram is filtered in Iran so the server may need Telegram:ApiBase pointing to a reachable mirror/proxy.)
- Apply the `Leads` migration on the real SQL Server (`dotnet ef database update`).
- Apply the `PhotoVariants` migration too.
- SMS: set `Sms:Provider=Kavenegar`, `Sms:ApiKey`, `Sms:Template` and `Sms:Sender` (an advertising line) before alerts and campaigns reach real phones.
- After deploy: press "ساخت نسخه‌های WebP" on the admin dashboard once for old photos; measure Core Web Vitals on the real server.
- Then: city/district pages.

## Backlog
- Multi-photo albums in channels (sendMediaGroup); currently first photo + caption only.
- SEO: SSR, unique title/meta, canonical, sitemap.xml, robots.txt, JSON-LD (RealEstateListing/Offer), OG tags, clean slugs, image alt, CWV.
- GEO (AI search): llms.txt, structured data, clear FAQ/answer blocks, allow AI crawlers in robots.txt.
- Map (Leaflet/Neshan), favorites, contact/lead form.
- Deploy.

Last updated: 2026-10-25 20:00
