# Amlak — Real estate site (sale + rent)

## Current status
- Backend: DDD + CQRS with the usual structure (same as DentV2 / Salmandan, `AhmadBase.*` packages in `Nuget/`): `Src/{Domain,Application,Infrastructure,Rest,Host}`, .NET 8. See `docs/BACKEND.md`.
- Frontend: React (Vite/TS/Tailwind/React Query/Zustand) in `Front/`, Salmandan structure. Razor site deleted. See `docs/FRONTEND.md`.
- Design chosen: Option 1 "Modern & Trustworthy" (teal/blue, light, Vazirmatn, RTL). Mockup: `design-options.html`. Design system: `design-system/amlak/MASTER.md`.
- Stack: API = .NET 8 DDD (AhmadBase, EF Core 8 + Dapper, Autofac, JWT/OTP). Front = React 18 SPA.
- SEO regression: SPA has no server rendering (SEO-01). Needs prerender or SSR before launch (options in docs/FRONTEND.md).

## Done
- Design options (3) built and one chosen.
- Solution scaffold `src/Amlak.Web` (net9, EF Core 9, Identity).
- Domain (Property, PropertyPhoto, ChannelPost), AppDbContext, `Initial` migration applied to localhost\SQLEXPRESS / DB Amlak.
- Public RTL pages with design tokens + base SEO: home, `/املاک` list with filters + paging, `/ملک/{slug}` detail with canonical, OG and JSON-LD (RealEstateListing). Dev seed with 4 sample listings. Verified: build 0 errors, pages return 200 and render.
- Admin panel `/Admin` (role Admin policy): login `/Account/Login`, list, create/edit, photo upload (JPG/PNG/WebP <=5MB, magic-byte check), delete photo, publish toggle, delete. Admin user seeded from config Admin:Email / Admin:Password (dev values in user-secrets). Verified by HTTP: anon->redirect, wrong pw rejected, login ok, CSRF required, fake image rejected, valid upload served.
- Channel publisher (Telegram + Bale, same Bot API client, `Services/Publishing`): publish on go-live (photo+caption or text), edit on change, delete message on unpublish/delete, errors stored per channel and retried via admin button; token scrubbed from errors; HttpClient URL logging silenced. Migration `ChannelPostUniqueAndPhoto`. 8 xunit tests (`tests/Amlak.Tests`) pass against a stub Bot API + SQLite. NOT yet verified against the real Telegram/Bale servers.

- SEO/GEO files: `/sitemap.xml`, `/robots.txt` (AI crawlers allowed), `/llms.txt`; JSON-LD RealEstateAgent+WebSite (home), BreadcrumbList (list, detail); login noindex; base URL from `Site:BaseUrl`. 6 new xunit tests (14 total pass); endpoints verified over HTTP. Not yet: Search Console submission, FAQPage, WebP/srcset, city/district pages.

- DDD backend API (net8): Listing (Property + photos: create/edit/publish/unpublish/close/delete, photo upload with signature check, reorder, cover, alt), Publishing (ChannelPost aggregate; sync to Telegram/Bale through domain events, errors per post, manual re-sync, failed list), Lead (new: public contact request, 3 per phone per 10 min, SMS to admins, admin status workflow), Identity (OTP + JWT, admin only from `Platform:AdminPhones`, OTP cooldown 1 min + burn after 5 wrong tries, refresh rotation). Dapper read side: search with advanced filters, by-slug (404 for non-published), similar, locations, sitemap data, admin lists. EF migration `Initial` (DB `AmlakApi`). 62 tests pass (31 domain, 31 application on SQLite with real event handlers + fake Bot API). Host starts; Swagger lists 33 routes; admin routes 401 without token; validation 422 in Persian. NOT verified against a real SQL Server (Dapper SQL untested) or real bots.

- React front: public home/list (advanced filters, sort, paging)/detail (gallery, JSON-LD, lead form, similar), OTP login, admin dashboard, listings CRUD with photo manager (upload, order, cover, alt) and channel status/re-sync, leads workflow. sitemap/robots/llms moved into the API. Public by-slug API no longer returns the exact address. Verified: tsc + vite build, 7 vitest tests, 35-check Playwright run against a mocked API (incl. 375px no horizontal scroll). Razor project and its tests deleted.

## Next task
- Run `dotnet ef database update` for `AmlakApi` on SQL Server, then run API + `npm run dev` together and click through every page (Dapper SQL and the real API wiring are untested).
- Decide the SEO fix for the SPA (prerender vs SSR).
- Data migration from the old `Amlak` DB (Razor) to `AmlakApi`, incl. photos from `wwwroot/uploads` → `storage/photos`, if the old DB holds real listings.
- Real-bot check: set Telegram/Bale tokens + chat ids in user-secrets, publish one listing, confirm message appears, edit, unpublish. (Needs user's bot tokens; Telegram is filtered in Iran so the server may need Telegram:ApiBase pointing to a reachable mirror/proxy.)
- Then: FAQPage + city/district pages, image WebP/srcset.

## Backlog
- Multi-photo albums in channels (sendMediaGroup); currently first photo + caption only.
- SEO: SSR, unique title/meta, canonical, sitemap.xml, robots.txt, JSON-LD (RealEstateListing/Offer), OG tags, clean slugs, image alt, CWV.
- GEO (AI search): llms.txt, structured data, clear FAQ/answer blocks, allow AI crawlers in robots.txt.
- Map (Leaflet/Neshan), favorites, contact/lead form.
- Deploy.

Last updated: 2026-10-25 20:40
