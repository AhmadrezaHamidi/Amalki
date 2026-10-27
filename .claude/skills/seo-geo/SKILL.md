---
name: seo-geo
description: SEO and GEO (AI-search visibility) checklist and conventions for the Amlak (آشیانه) Razor Pages site. Use when adding or changing any public page (home, /املاک list, /ملک/{slug} detail, future city/district or FAQ pages), when touching titles, meta, canonical, Open Graph, JSON-LD, sitemap.xml, robots.txt or llms.txt, or when the user asks for "سئو", "SEO", "GEO" or AI-search work.
---

# SEO + GEO for Amlak

SEO = ranking in Google. GEO = being quoted and cited by AI answer engines (ChatGPT, Gemini, Perplexity, Claude).
Both reward the same things: complete server-rendered HTML, correct structured data, and short factual text
a model can lift verbatim. Background and the status table live in `docs/SEO-GEO.md`; keep it in sync.

## Where things live

| Concern | File |
|---|---|
| `<title>`, description, canonical, robots, OG/Twitter tags | `Pages/Shared/_Layout.cshtml` reads `ViewData["Title" / "Description" / "Canonical" / "Robots" / "OgImage"]` |
| Public base URL | `SeoEndpoints.BaseUrl(cfg, req)` — reads `Site:BaseUrl`, never trust the Host header |
| Absolute/encoded URLs | `Taxonomy.EncodedPath` (landing pages), `SeoFiles.PropertyUrl`, `SeoFiles.Absolute` |
| JSON-LD builders | `Services/Seo/JsonLd.cs` (pure, unit-tested) |
| Quotable listing text | `Services/Seo/SeoText.cs` |
| sitemap / robots / llms.txt | `Services/Seo/SeoFiles.cs`, mapped in `SeoEndpoints.MapSeo` |
| Tests | `tests/Amlak.Tests/SeoTests.cs` |

## Rules

1. **Absolute URLs from `Site:BaseUrl`** for canonical, `og:url`, `og:image`, every JSON-LD `url`/`item`/`image`.
   Build them with `SeoFiles` helpers so Persian path segments are percent-encoded the same way everywhere
   (canonical, sitemap and breadcrumbs must match byte for byte).
2. **One canonical per indexable page.** List pages are Divar-style landing pages `/املاک/{city}/{category}/{district}`;
   the canonical is that path, plus `?p=N` on page 2+ (never canonical to page 1). Query filters and sort orders collapse to it.
   Empty result pages get `noindex, follow`. Admin/Account pages are `noindex`.
3. **Unique title and description.** Include the facts people search for: deal type, kind, district/city,
   size, price; add "صفحهٔ N" on paginated pages. Descriptions ~120–160 chars, no keyword stuffing.
4. **JSON-LD must be valid schema.org and honest.**
   - Render the tag with `@Html.Raw(JsonLd.Script(...))` (a literal Razor `<script type="application/ld+json">` comes out as `ld&#x2B;json`); it serializes through `JsonLd.Serialize` (escapes `</`). Drop null fields; never emit `null`.
   - Prices: schema.org needs ISO 4217. Toman is not a currency code — store Toman, emit **IRR = Toman × 10**.
   - Rent: monthly rent as `UnitPriceSpecification` with `unitCode: "MON"`, deposit (رهن) as a separate `PriceSpecification`.
   - Short-term rent: `Price` is per night, emitted as `UnitPriceSpecification` with `unitCode: "DAY"`.
   - Use only facts we have. No invented phone, address, rating, review or "best price" claims.
   - Do not publish the exact street address or coordinates of a listing (privacy); city + district only.
   - Dates in `yyyy-MM-dd` with `CultureInfo.InvariantCulture` (fa-IR culture would print a Persian-calendar year).
5. **Visible content matches structured data.** Visible breadcrumbs = `BreadcrumbList`; visible price/specs = Offer data.
6. **GEO-friendly content.**
   - Each listing starts with a standalone one-sentence summary (what, where, how big, how much) — `SeoText.Summary`.
   - Specs as a labelled `<dl>` with fixed labels (متراژ، خواب، طبقه، پارکینگ، سال ساخت), not free text.
   - Show publish date in a `<time datetime="...">` element (trust signal).
   - List pages open with a factual stats sentence from our own data (count, price range).
7. **Internal links.** Detail → its city/category/district landing pages and similar listings; breadcrumbs link to the landing pages.
8. **Images.** Every `<img>` has meaningful `alt`. The first/LCP image is `loading="eager" fetchpriority="high"`;
   the rest `loading="lazy"`. Give width/height when known to avoid CLS.
9. **Discovery files.** New public URL types go into `sitemap.xml` (and `llms.txt` if useful). Keep AI crawlers allowed in
   `robots.txt` unless the owner decides otherwise (see `docs/SEO-GEO.md`).

## Workflow for a change

1. Read the page and `docs/SEO-GEO.md`; list which rules above it breaks.
2. Put logic in the pure helpers (`JsonLd`, `SeoText`, `SeoFiles`) and add/adjust xunit tests in `SeoTests.cs`.
3. Keep Razor thin: compute in the helper, render in the view.
4. Build and test: `dotnet test tests/Amlak.Tests`.
5. Check rendered HTML (run the app, `curl` the page) for: one `<h1>`, canonical, description, valid JSON-LD
   (paste into https://validator.schema.org and Google Rich Results Test before release).
6. Update the status table and plan in `docs/SEO-GEO.md` and the line in `PLANNER.md`.
