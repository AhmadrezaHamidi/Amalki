using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json.Serialization;

namespace Amlak.Web.Services;

/// <summary>
/// Real-user speed monitoring (RUM): each visitor's browser reports its Core Web Vitals once per page view
/// (site.js, on leaving the page). They are kept in memory for the admin dashboard (p75 of the last 24 hours)
/// and exported as OpenTelemetry histograms when a collector is configured.
/// </summary>
public sealed class WebVitals
{
    public const string MeterName = "Amlak.WebVitals";
    public const string Path = "/rum";

    /// <summary>Google's thresholds: good up to the first value, poor above the second.</summary>
    public static readonly IReadOnlyDictionary<string, (double Good, double Poor, string Unit)> Metrics = new Dictionary<string, (double, double, string)>
    {
        ["lcp"] = (2500, 4000, "ms"),
        ["inp"] = (200, 500, "ms"),
        ["cls"] = (0.1, 0.25, ""),
        ["fcp"] = (1800, 3000, "ms"),
        ["ttfb"] = (800, 1800, "ms"),
    };

    public static readonly string[] PageTypes = ["home", "list", "detail", "other"];

    private const int MaxSamples = 5000; // per metric and page type; older ones fall off
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<(string Metric, string Page), ConcurrentQueue<(DateTime At, double Value)>> _samples = new();
    private readonly Dictionary<string, Histogram<double>> _histograms;
    private readonly TimeProvider _clock;

    public WebVitals(IMeterFactory meters, TimeProvider clock)
    {
        _clock = clock;
        var meter = meters.Create(MeterName);
        _histograms = Metrics.ToDictionary(m => m.Key, m => meter.CreateHistogram<double>($"webvitals.{m.Key}", m.Value.Unit == "" ? null : m.Value.Unit));
    }

    public sealed record Report(
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("lcp")] double? Lcp,
        [property: JsonPropertyName("inp")] double? Inp,
        [property: JsonPropertyName("cls")] double? Cls,
        [property: JsonPropertyName("fcp")] double? Fcp,
        [property: JsonPropertyName("ttfb")] double? Ttfb);

    /// <summary>Page type from the path, so the numbers group by template and not by every URL.</summary>
    public static string PageType(string? path) => path switch
    {
        null or "" => "other",
        "/" => "home",
        _ when path.StartsWith(Taxonomy.Root, StringComparison.Ordinal) => "list",
        _ when path.StartsWith("/ملک/", StringComparison.Ordinal) => "detail",
        _ => "other",
    };

    public void Record(Report r)
    {
        var page = PageType(r.Path);
        if (r.Path?.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) == true) return;
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var (name, value) in new[] { ("lcp", r.Lcp), ("inp", r.Inp), ("cls", r.Cls), ("fcp", r.Fcp), ("ttfb", r.Ttfb) })
        {
            // Drop missing and absurd values (a tab left open for an hour, a forged report).
            if (value is not { } v || double.IsNaN(v) || v < 0 || v > (name == "cls" ? 10 : 120_000)) continue;
            var q = _samples.GetOrAdd((name, page), _ => new());
            q.Enqueue((now, v));
            while (q.Count > MaxSamples && q.TryDequeue(out _)) { }
            _histograms[name].Record(v, new KeyValuePair<string, object?>("page", page));
        }
    }

    public sealed record Summary(string Metric, string Page, int Samples, double P75, string Rating);

    /// <summary>75th percentile per metric and page type over the last 24 hours, the way Google scores a site.</summary>
    public List<Summary> Summarize()
    {
        var since = _clock.GetUtcNow().UtcDateTime - Window;
        var list = new List<Summary>();
        foreach (var ((metric, page), q) in _samples)
        {
            var values = q.Where(s => s.At >= since).Select(s => s.Value).Order().ToArray();
            if (values.Length == 0) continue;
            var p75 = values[(int)Math.Ceiling(values.Length * 0.75) - 1];
            var (good, poor, _) = Metrics[metric];
            list.Add(new Summary(metric, page, values.Length, p75, p75 <= good ? "good" : p75 <= poor ? "ni" : "poor"));
        }
        return list;
    }
}

public static class WebVitalsEndpoints
{
    public static IServiceCollection AddWebVitals(this IServiceCollection services) => services.AddSingleton<WebVitals>();

    public static void MapWebVitals(this WebApplication app) =>
        // navigator.sendBeacon posts text/plain JSON; no cookies or tokens are needed and nothing is stored on disk.
        app.MapPost(WebVitals.Path, async (HttpRequest req, WebVitals vitals, CancellationToken ct) =>
        {
            if (req.ContentLength is > 2048) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            try
            {
                var report = await System.Text.Json.JsonSerializer.DeserializeAsync<WebVitals.Report>(req.Body, cancellationToken: ct);
                if (report is not null) vitals.Record(report);
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest();
            }
            return Results.NoContent();
        }).DisableAntiforgery();
}
