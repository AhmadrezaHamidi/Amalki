using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Amlak.Tests;

public sealed class PhotoAndCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "amlak-" + Guid.NewGuid().ToString("N"));

    public PhotoAndCacheTests() => Directory.CreateDirectory(Path.Combine(_root, "uploads"));

    private static byte[] Jpeg(int w, int h)
    {
        using var bmp = new SKBitmap(w, h);
        using (var c = new SKCanvas(bmp)) c.Clear(SKColors.SteelBlue);
        using var img = SKImage.FromBitmap(bmp);
        return img.Encode(SKEncodedImageFormat.Jpeg, 80).ToArray();
    }

    [Fact]
    public void Large_photo_gets_webp_copies_at_each_width_without_upscaling()
    {
        var r = PhotoVariants.Make(new MemoryStream(Jpeg(2000, 1000)));
        Assert.NotNull(r);
        Assert.Equal((2000, 1000), (r.Width, r.Height));
        Assert.Equal([480, 960, 1600], r.Variants.Select(v => v.Width));
        Assert.All(r.Variants, v => Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(v.Webp, 8, 4)));
        using var decoded = SKBitmap.Decode(r.Variants[0].Webp);
        Assert.Equal((480, 240), (decoded.Width, decoded.Height));
    }

    [Fact]
    public void Small_photo_gets_one_copy_at_its_own_width_and_garbage_gets_none()
    {
        Assert.Equal([300], PhotoVariants.Make(new MemoryStream(Jpeg(300, 200)))!.Variants.Select(v => v.Width));
        Assert.Null(PhotoVariants.Make(new MemoryStream([0xFF, 0xD8, 0xFF, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13])));
    }

    [Fact]
    public void Copy_urls_sit_next_to_the_original_for_local_and_minio()
    {
        Assert.Equal("/uploads/abc-960.webp", PhotoVariants.UrlFor("/uploads/abc.jpg", 960));
        Assert.Equal("https://cdn.example.ir/amlak/uploads/abc-480.webp", PhotoVariants.UrlFor("https://cdn.example.ir/amlak/uploads/abc.png", 480));
    }

    [Fact]
    public void Markup_uses_picture_with_srcset_only_when_copies_exist_and_encodes_alt()
    {
        var legacy = new PropertyPhoto { Url = "/uploads/a.jpg", Alt = "a\"<b" };
        var html = PhotoVariants.Img(legacy, "100vw", 640, 480).ToString()!;
        Assert.StartsWith("<img ", html);
        Assert.Contains("loading=\"lazy\"", html);
        Assert.DoesNotContain("a\"<b", html);

        var modern = new PropertyPhoto { Url = "/uploads/a.jpg", Alt = "x", Width = 2000, Height = 1000, Variants = "480,960,1600" };
        html = PhotoVariants.Img(modern, "100vw", 640, 480, eager: true).ToString()!;
        Assert.Contains("<picture><source type=\"image/webp\" srcset=\"/uploads/a-480.webp 480w, /uploads/a-960.webp 960w, /uploads/a-1600.webp 1600w\"", html);
        Assert.Contains("width=\"2000\" height=\"1000\"", html);
        Assert.Contains("fetchpriority=\"high\"", html);
        Assert.DoesNotContain("loading=", html);
        Assert.Equal("/uploads/a-1600.webp", PhotoVariants.Largest(modern));
    }

    [Fact]
    public async Task Upload_stores_original_and_copies_and_delete_removes_them_all()
    {
        var storage = TestStores.Photos(_root);
        var bytes = Jpeg(1200, 900);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "f", "room.jpg");

        var photo = await storage.SaveAsync(file);

        Assert.NotNull(photo);
        Assert.Equal("480,960,1200", photo.Variants);
        Assert.Equal((1200, 900), (photo.Width, photo.Height));
        var files = Directory.GetFiles(Path.Combine(_root, "uploads"));
        Assert.Equal(4, files.Length);

        await storage.DeleteAsync(photo);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "uploads")));
    }

    [Fact]
    public async Task Saving_a_listing_evicts_cached_pages_but_other_saves_do_not()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var store = new CountingStore();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn)
            .AddInterceptors(new ListingCacheInterceptor(store)).Options);
        await db.Database.EnsureCreatedAsync();

        db.Leads.Add(new Lead { Name = "n", Phone = "09120000000" });
        await db.SaveChangesAsync();
        Assert.Equal(0, store.Evictions);

        var p = new Property { Title = "t", Slug = "t", City = "c", District = "d", AreaM2 = 1 };
        db.Properties.Add(p);
        await db.SaveChangesAsync();
        Assert.Equal(1, store.Evictions);

        p.Photos.Add(new PropertyPhoto { Url = "/uploads/x.jpg" });
        await db.SaveChangesAsync();
        Assert.Equal(2, store.Evictions);
        Assert.Equal([SiteCache.Listings], store.Tags.Distinct());
    }

    [Fact]
    public async Task Backfill_makes_copies_for_an_old_photo_and_marks_unreadable_ones_done()
    {
        var bytes = Jpeg(1000, 750);
        File.WriteAllBytes(Path.Combine(_root, "uploads", "old.jpg"), bytes);
        File.WriteAllBytes(Path.Combine(_root, "uploads", "bad.jpg"), [0xFF, 0xD8, 0xFF, 0, 1, 2]);
        var storage = TestStores.Photos(_root);

        var old = new PropertyPhoto { Url = "/uploads/old.jpg" };
        Assert.True(await storage.BackfillAsync(old));
        Assert.Equal("480,960,1000", old.Variants);
        Assert.Equal((1000, 750), (old.Width, old.Height));
        Assert.True(File.Exists(Path.Combine(_root, "uploads", "old-960.webp")));

        var bad = new PropertyPhoto { Url = "/uploads/bad.jpg" };
        Assert.True(await storage.BackfillAsync(bad));
        Assert.Equal("", bad.Variants);
        Assert.StartsWith("<img ", PhotoVariants.Img(bad, "100vw", 640, 480).ToString());

        Assert.False(await storage.BackfillAsync(new PropertyPhoto { Url = "/uploads/gone.jpg" }));
    }

    [Theory]
    [InlineData(3, PhotoOrder.Move.First, "3,1,2,4")]
    [InlineData(3, PhotoOrder.Move.Earlier, "1,3,2,4")]
    [InlineData(3, PhotoOrder.Move.Later, "1,2,4,3")]
    [InlineData(1, PhotoOrder.Move.Earlier, "1,2,3,4")]
    [InlineData(4, PhotoOrder.Move.Later, "1,2,3,4")]
    public void Moving_a_photo_renumbers_the_whole_listing(int id, PhotoOrder.Move move, string expected)
    {
        // Gaps and a tie in SortOrder, as left behind by deletes and older uploads.
        var photos = new List<PropertyPhoto>
        {
            new() { Id = 2, SortOrder = 5 }, new() { Id = 1, SortOrder = 0 },
            new() { Id = 4, SortOrder = 9 }, new() { Id = 3, SortOrder = 5 },
        };
        Assert.True(PhotoOrder.Apply(photos, id, move));
        Assert.Equal(expected, string.Join(",", photos.OrderBy(x => x.SortOrder).Select(x => x.Id)));
        Assert.Equal([0, 1, 2, 3], photos.Select(x => x.SortOrder).Order());
        Assert.False(PhotoOrder.Apply(photos, 99, move));
    }

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private sealed class CountingStore : IOutputCacheStore
    {
        public int Evictions => Tags.Count;
        public List<string> Tags { get; } = [];
        public ValueTask EvictByTagAsync(string tag, CancellationToken ct) { Tags.Add(tag); return ValueTask.CompletedTask; }
        public ValueTask<byte[]?> GetAsync(string key, CancellationToken ct) => ValueTask.FromResult<byte[]?>(null);
        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken ct) => ValueTask.CompletedTask;
    }
}
