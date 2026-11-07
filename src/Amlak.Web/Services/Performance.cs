using System.IO.Compression;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.ResponseCompression;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Amlak.Web.Services;

/// <summary>
/// Speed and scale settings in one place (docs/PERFORMANCE.md): compressed pages, request time limits,
/// browser caching of static files, Redis for several app servers behind a load balancer, OpenTelemetry.
/// </summary>
public static class Performance
{
    public static WebApplicationBuilder AddPerformance(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var cfg = builder.Configuration;

        // Brotli, then gzip, for HTML, JSON, XML and text made per request. CSS/JS/SVG are already stored
        // pre-compressed by MapStaticAssets. HTTPS too: the pages that carry secrets (forms with antiforgery
        // tokens, admin) are not reflected-input pages, and nginx can still take this over (docs/PERFORMANCE.md).
        services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true;
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
            o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml", "application/manifest+json"]);
        });
        // Fastest: on a 40 KB page it gives nearly the same size as Optimal at a fraction of the CPU.
        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

        // A stuck database or file store must not hold a visitor (and a server thread) forever: the request is
        // cancelled and answered 504. Pages with photo uploads opt out (slow phones need longer to send them).
        var seconds = cfg.GetValue("Performance:RequestTimeoutSeconds", 20);
        services.AddRequestTimeouts(o =>
        {
            o.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(seconds), TimeoutStatusCode = StatusCodes.Status504GatewayTimeout };
        });

        // Several app servers behind a load balancer must share two things: the output cache (or each server
        // keeps its own stale copy after an edit) and the Data Protection keys (or a login cookie or form token
        // made on one server is rejected by the next). Redis holds both when Redis:Connection is set.
        if (cfg["Redis:Connection"] is { Length: > 0 } redis)
        {
            var mux = ConnectionMultiplexer.Connect(redis);
            services.AddSingleton<IConnectionMultiplexer>(mux);
            services.AddStackExchangeRedisOutputCache(o =>
            {
                o.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(mux);
                o.InstanceName = "amlak:";
            });
            services.AddDataProtection().SetApplicationName("amlak").PersistKeysToStackExchangeRedis(mux, "amlak:dp-keys");
        }
        // Shared Windows hosting (IIS without a loaded user profile, e.g. MonsterASP) keeps the keys only in memory,
        // so every app-pool recycle would sign everyone out. DataProtection:KeysPath (relative to the app folder,
        // e.g. "App_Data/keys") stores them on disk instead. Docs: docs/DEPLOY-FREE.md.
        else if (cfg["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        {
            services.AddDataProtection().SetApplicationName("amlak")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, keysPath)));
        }

        // Traces and metrics (requests, SQL, outgoing HTTP to bots/SMS, runtime, visitors' Web Vitals) go to
        // any OpenTelemetry collector when OTEL_EXPORTER_OTLP_ENDPOINT is set (Jaeger, Grafana, SigNoz...).
        if (!string.IsNullOrWhiteSpace(cfg["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(cfg["OTEL_SERVICE_NAME"] ?? "amlak-web"))
                .WithTracing(t => t
                    .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/healthz"))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddOtlpExporter())
                .WithMetrics(m => m
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(WebVitals.MeterName)
                    .AddOtlpExporter());
        }
        return builder;
    }

    /// <summary>
    /// Static files that are not fingerprinted (fonts, demo photos, icons, uploads). Fingerprinted CSS/JS
    /// (site.abc123.css) are served by MapStaticAssets with a one-year immutable header and pre-compressed.
    /// </summary>
    public static StaticFileOptions StaticFiles() => new()
    {
        OnPrepareResponse = ctx =>
        {
            var path = ctx.Context.Request.Path;
            var headers = ctx.Context.Response.Headers;
            // Upload names are random GUIDs and never reused.
            if (path.StartsWithSegments("/uploads"))
                headers.CacheControl = "public, max-age=31536000, immutable";
            // Rarely change; a week in the browser, then a quick ETag check while the old copy is shown.
            else if (path.StartsWithSegments("/fonts") || path.StartsWithSegments("/img") || path.StartsWithSegments("/icons"))
                headers.CacheControl = "public, max-age=604800, stale-while-revalidate=86400";
            // sw.js, manifest and offline page must update as soon as they change.
            else
                headers.CacheControl = "no-cache";
        },
    };
}
