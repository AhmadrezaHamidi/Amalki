using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amlak.Web.Migrations
{
    /// <inheritdoc />
    public partial class ReadViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyPhotos_PropertyId",
                table: "PropertyPhotos");

            migrationBuilder.DropIndex(
                name: "IX_Properties_Status_Deal_City",
                table: "Properties");

            migrationBuilder.AddColumn<long>(
                name: "SortPrice",
                table: "Properties",
                type: "bigint",
                nullable: false,
                computedColumnSql: "ISNULL([Price], ISNULL([Deposit], CAST(0 AS bigint)))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotos_PropertyId_SortOrder",
                table: "PropertyPhotos",
                columns: new[] { "PropertyId", "SortOrder" })
                .Annotation("SqlServer:Include", new[] { "Url", "Alt", "Width", "Height", "Variants" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Status_Deal_City_Kind_PublishedAtUtc",
                table: "Properties",
                columns: new[] { "Status", "Deal", "City", "Kind", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Status_PublishedAtUtc",
                table: "Properties",
                columns: new[] { "Status", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Status_SortPrice",
                table: "Properties",
                columns: new[] { "Status", "SortPrice" });

            // Read side (CQRS). Card rows: published listings flattened with their cover photo.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_ListingCards AS
                SELECT p.Id, p.Slug, p.Title, p.Deal, p.Kind, p.City, p.District, p.AreaM2, p.Bedrooms, p.Floor, p.Parking,
                       p.YearBuilt, p.HasElevator, p.HasStorage, p.HasBalcony, p.Deed, p.Convertible,
                       p.Price, p.Deposit, p.MonthlyRent, p.SortPrice, p.PublishedAtUtc,
                       c.Url AS CoverUrl, c.Alt AS CoverAlt, c.Width AS CoverWidth, c.Height AS CoverHeight, c.Variants AS CoverVariants,
                       (SELECT COUNT(*) FROM dbo.PropertyPhotos x WHERE x.PropertyId = p.Id) AS PhotoCount
                FROM dbo.Properties p
                OUTER APPLY (SELECT TOP (1) f.Url, f.Alt, f.Width, f.Height, f.Variants FROM dbo.PropertyPhotos f
                             WHERE f.PropertyId = p.Id ORDER BY f.SortOrder, f.Id) c
                WHERE p.Status = 1;
                """);

            // Facets: an indexed (materialized) view. SQL Server keeps the totals current on every write,
            // so counts per kind/city/district are a lookup instead of a scan. SCHEMABINDING means the
            // columns it uses (Status, Deal, Kind, City, District, AreaM2, SortPrice) cannot be altered
            // without dropping and recreating this view in the same migration.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_ListingFacets WITH SCHEMABINDING AS
                SELECT p.Deal, p.Kind, p.City, p.District, COUNT_BIG(*) AS Listings,
                       SUM(p.SortPrice) AS SumPrice, SUM(CAST(p.AreaM2 AS bigint)) AS SumArea
                FROM dbo.Properties p
                WHERE p.Status = 1
                GROUP BY p.Deal, p.Kind, p.City, p.District;
                """);
            migrationBuilder.Sql("CREATE UNIQUE CLUSTERED INDEX IX_vw_ListingFacets ON dbo.vw_ListingFacets (Deal, Kind, City, District);");

            // One row with every counter the admin dashboard and sidebar badges show.
            migrationBuilder.Sql("""
                CREATE VIEW vw_AdminCounters AS
                SELECT
                    (SELECT COUNT(*) FROM Properties WHERE Status = 1) AS Published,
                    (SELECT COUNT(*) FROM Properties WHERE Status = 0) AS Drafts,
                    (SELECT COUNT(*) FROM Leads WHERE Handled = 0) AS NewLeads,
                    (SELECT COUNT(*) FROM Tickets WHERE Status = 0) AS OpenTickets,
                    (SELECT COUNT(*) FROM Tickets WHERE Status = 1) AS AnsweredTickets,
                    (SELECT COUNT(*) FROM ChannelPosts WHERE Error IS NOT NULL) AS ChannelErrors;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_AdminCounters;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_ListingFacets;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_ListingCards;");

            migrationBuilder.DropIndex(
                name: "IX_PropertyPhotos_PropertyId_SortOrder",
                table: "PropertyPhotos");

            migrationBuilder.DropIndex(
                name: "IX_Properties_Status_Deal_City_Kind_PublishedAtUtc",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_Status_PublishedAtUtc",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_Status_SortPrice",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "SortPrice",
                table: "Properties");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotos_PropertyId",
                table: "PropertyPhotos",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Status_Deal_City",
                table: "Properties",
                columns: new[] { "Status", "Deal", "City" });
        }
    }
}
