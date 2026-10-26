using System.Text;
using Amlak.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace Amlak.Tests;

public sealed class HardeningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "amlak-" + Guid.NewGuid().ToString("N"));

    public HardeningTests() => Directory.CreateDirectory(Path.Combine(_root, "uploads"));

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    /// <summary>A JPEG with an EXIF block (APP1) right after the start marker, like a phone photo.</summary>
    private static byte[] JpegWithExif()
    {
        using var bmp = new SKBitmap(64, 48);
        bmp.Erase(SKColors.SteelBlue);
        var jpeg = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Jpeg, 90).ToArray();
        var body = Encoding.ASCII.GetBytes("Exif\0\0GPSLatitude=35.7219;GPSLongitude=51.3347;Model=Phone");
        var len = body.Length + 2;
        byte[] app1 = [0xFF, 0xE1, (byte)(len >> 8), (byte)len, .. body];
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }

    [Fact]
    public async Task Uploaded_photos_are_stored_without_exif()
    {
        var bytes = JpegWithExif();
        Assert.Contains("GPSLatitude", Encoding.ASCII.GetString(bytes));
        var storage = TestStores.Photos(_root);

        var photo = await storage.SaveAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "f", "home.jpg"));

        Assert.NotNull(photo);
        var stored = File.ReadAllBytes(Path.Combine(_root, "uploads", Path.GetFileName(photo.Url)));
        Assert.DoesNotContain("GPSLatitude", Encoding.ASCII.GetString(stored));
        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(stored));
        using var decoded = SKBitmap.Decode(stored);
        Assert.Equal((64, 48), (decoded.Width, decoded.Height));
    }

    private static TicketAccess Access() => new(new EphemeralDataProtectionProvider());

    [Fact]
    public void A_ticket_opens_only_in_a_browser_that_created_or_tracked_it()
    {
        var access = Access();
        var first = new DefaultHttpContext();
        Assert.False(access.Has(first, "K7Q2M9XA"));

        access.Grant(first, "K7Q2M9XA");
        var cookie = first.Response.Headers.SetCookie.ToString();
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("K7Q2M9XA", cookie); // encrypted, not readable or forgeable

        var next = new DefaultHttpContext();
        next.Request.Headers.Cookie = cookie.Split(';')[0];
        Assert.True(access.Has(next, "K7Q2M9XA"));
        Assert.False(access.Has(next, "AAAABBBB"));
    }

    [Fact]
    public void A_forged_cookie_grants_nothing()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = $"{TicketAccess.Cookie}=K7Q2M9XA";
        Assert.False(Access().Has(ctx, "K7Q2M9XA"));
    }
}
