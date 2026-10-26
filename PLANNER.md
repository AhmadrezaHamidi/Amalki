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

## Next task
- Real-bot check: set Telegram/Bale tokens + chat ids in user-secrets, publish one listing, confirm message appears, edit, unpublish. (Needs user's bot tokens; Telegram is filtered in Iran so the server may need Telegram:ApiBase pointing to a reachable mirror/proxy.)
- Apply the `Leads` migration on the real SQL Server (`dotnet ef database update`).
- Then: city/district pages, image WebP/srcset, photo reordering (ADM-09).

## Backlog
- Multi-photo albums in channels (sendMediaGroup); currently first photo + caption only.
- SEO: SSR, unique title/meta, canonical, sitemap.xml, robots.txt, JSON-LD (RealEstateListing/Offer), OG tags, clean slugs, image alt, CWV.
- GEO (AI search): llms.txt, structured data, clear FAQ/answer blocks, allow AI crawlers in robots.txt.
- Map (Leaflet/Neshan), favorites, contact/lead form.
- Deploy.

Last updated: 2026-10-25 20:00
