using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using SkiaSharp;

namespace Amlak.Web.Services;

/// <summary>
/// Resized WebP copies of listing photos, served through srcset so phones download a small file
/// instead of the camera original (SEO-07, Core Web Vitals / LCP). The copy of ".../abc.jpg" at
/// width w lives next to it at ".../abc-{w}.webp"; the original stays as the fallback and for channels.
/// </summary>
public static class PhotoVariants
{
    /// <summary>Card, detail-gallery and full-screen widths. Larger originals are never upscaled.</summary>
    public static readonly int[] Widths = [480, 960, 1600];
    private const int Quality = 78;

    public sealed record Variant(int Width, byte[] Webp);
    public sealed record Result(int Width, int Height, IReadOnlyList<Variant> Variants);

    public static string UrlFor(string url, int width)
    {
        var dot = url.LastIndexOf('.');
        var stem = dot > url.LastIndexOf('/') ? url[..dot] : url;
        return $"{stem}-{width}.webp";
    }

    /// <returns>Size of the (upright) photo and its WebP copies, or null when it cannot be decoded.</returns>
    public static Result? Make(Stream input)
    {
        using var codec = SKCodec.Create(input);
        if (codec is null) return null;
        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null) return null;
        using var upright = Orient(decoded, codec.EncodedOrigin);

        var max = Widths[^1];
        var targets = Widths.Where(w => w < upright.Width).Append(Math.Min(upright.Width, max)).Distinct().Order().ToList();
        var variants = new List<Variant>();
        foreach (var w in targets)
        {
            var h = Math.Max(1, (int)Math.Round(upright.Height * (w / (double)upright.Width)));
            using var resized = w == upright.Width ? upright.Copy() : upright.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (resized is null) continue;
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Webp, Quality);
            if (data is not null) variants.Add(new Variant(w, data.ToArray()));
        }
        return new Result(upright.Width, upright.Height, variants);
    }

    /// <summary>
    /// &lt;picture&gt; with the WebP srcset when the photo has copies, else a plain &lt;img&gt;.
    /// The picture element is display:contents in CSS, so layouts written for a bare img keep working.
    /// </summary>
    public static IHtmlContent Img(PropertyPhoto p, string sizes, int width, int height, bool eager = false)
    {
        var e = HtmlEncoder.Default;
        var img = $"<img src=\"{e.Encode(p.Url)}\" alt=\"{e.Encode(p.Alt)}\" width=\"{p.Width ?? width}\" height=\"{p.Height ?? height}\" "
            + (eager ? "fetchpriority=\"high\" decoding=\"async\" />" : "loading=\"lazy\" decoding=\"async\" />");
        var widths = ParseWidths(p.Variants);
        if (widths.Count == 0) return new HtmlString(img);
        var srcset = string.Join(", ", widths.Select(w => $"{e.Encode(UrlFor(p.Url, w))} {w}w"));
        return new HtmlString($"<picture><source type=\"image/webp\" srcset=\"{srcset}\" sizes=\"{e.Encode(sizes)}\" />{img}</picture>");
    }

    /// <returns>URL of the largest WebP copy, or the original when there is none.</returns>
    public static string Largest(PropertyPhoto p) => ParseWidths(p.Variants) is { Count: > 0 } w ? UrlFor(p.Url, w[^1]) : p.Url;

    public static List<int> ParseWidths(string? variants) =>
        string.IsNullOrWhiteSpace(variants) ? [] : variants.Split(',').Select(s => int.TryParse(s, out var w) ? w : 0).Where(w => w > 0).Order().ToList();

    // Phone cameras store rotation in EXIF instead of rotating pixels; bake it in so copies are upright.
    private static SKBitmap Orient(SKBitmap src, SKEncodedOrigin origin)
    {
        var (deg, swap) = origin switch
        {
            SKEncodedOrigin.RightTop => (90, true),
            SKEncodedOrigin.BottomRight => (180, false),
            SKEncodedOrigin.LeftBottom => (270, true),
            _ => (0, false),
        };
        if (deg == 0) return src.Copy();
        var dst = new SKBitmap(swap ? src.Height : src.Width, swap ? src.Width : src.Height);
        using var canvas = new SKCanvas(dst);
        canvas.Translate(dst.Width / 2f, dst.Height / 2f);
        canvas.RotateDegrees(deg);
        canvas.Translate(-src.Width / 2f, -src.Height / 2f);
        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }
}
