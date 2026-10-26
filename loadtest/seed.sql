-- Bulk sample data for load testing (SQL Server). Safe to re-run: removes earlier load-test rows first.
-- Usage: sqlcmd -I -S localhost -U sa -P <pw> -d Amlak -v Listings=20000 -i loadtest/seed.sql
-- -I (QUOTED_IDENTIFIER ON) is required: Properties has an indexed view and a computed column.
SET NOCOUNT ON;
DECLARE @n int = $(Listings);

DELETE FROM Properties WHERE Slug LIKE N'lt-%';

;WITH nums AS (
    SELECT TOP (@n) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
),
shaped AS (
    SELECT i,
        CASE WHEN i % 5 < 3 THEN 1 ELSE 2 END AS Deal,
        1 + (i * 7) % 5 AS Kind,
        CHOOSE(1 + (i * 13) % 8, N'تهران', N'تهران', N'تهران', N'کرج', N'شیراز', N'اصفهان', N'مشهد', N'تبریز') AS City,
        CHOOSE(1 + (i * 11) % 6, N'سعادت‌آباد', N'ونک', N'پونک', N'مرکز شهر', N'شمال شهر', N'غرب') AS District,
        40 + (i * 37) % 460 AS Area,
        -- About one in ten is a draft, so admin pages have both.
        CASE WHEN i % 10 = 0 THEN 0 ELSE 1 END AS Status
    FROM nums
)
INSERT INTO Properties (Slug, Title, Description, Deal, Kind, Status, Price, Deposit, MonthlyRent, AreaM2, Bedrooms,
    Floor, Parking, YearBuilt, City, District, Address, Latitude, Longitude, CreatedAtUtc, PublishedAtUtc)
SELECT N'lt-' + CAST(i AS nvarchar(10)),
    N'آگهی آزمایشی ' + CAST(i AS nvarchar(10)) + N' ' + District,
    REPLICATE(N'توضیح نمونه برای تست بار. ', 20),
    Deal, Kind, Status,
    CASE WHEN Deal = 1 THEN CAST(Area AS bigint) * (60000000 + (i % 50) * 2000000) END,
    CASE WHEN Deal = 2 THEN CAST(Area AS bigint) * (3000000 + (i % 40) * 100000) END,
    CASE WHEN Deal = 2 THEN CAST(Area AS bigint) * (100000 + (i % 30) * 5000) END,
    Area, CASE WHEN Kind IN (1, 2) THEN 1 + i % 4 END, i % 12, i % 3, 1380 + i % 23,
    City, District, NULL, 35.7 + (i % 100) / 1000.0, 51.4 + (i % 100) / 1000.0,
    DATEADD(minute, -i * 7, SYSUTCDATETIME()),
    CASE WHEN Status = 1 THEN DATEADD(minute, -i * 7, SYSUTCDATETIME()) END
FROM shaped;

-- Five photos per listing (URLs only; the files do not need to exist for page rendering).
INSERT INTO PropertyPhotos (PropertyId, Url, Alt, SortOrder)
SELECT p.Id, N'/uploads/lt/' + CAST(p.Id AS nvarchar(10)) + N'-' + CAST(s.k AS nvarchar(2)) + N'.jpg', p.Title, s.k
FROM Properties p CROSS JOIN (VALUES (0), (1), (2), (3), (4)) s(k)
WHERE p.Slug LIKE N'lt-%';

SELECT COUNT(*) AS Properties, (SELECT COUNT(*) FROM PropertyPhotos) AS Photos FROM Properties;
