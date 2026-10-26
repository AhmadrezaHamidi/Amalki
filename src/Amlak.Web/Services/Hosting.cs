using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Amlak.Web.Services;

/// <summary>Middleware for running behind a reverse proxy (nginx) on a public host.</summary>
public static class Hosting
{
    /// <summary>
    /// Trust X-Forwarded-For / X-Forwarded-Proto from the proxies in <c>Proxy:KnownProxies</c> (IPs) and
    /// <c>Proxy:KnownNetworks</c> (CIDR), plus loopback. Without this every visitor behind nginx has the
    /// proxy's IP, so one visitor's rate limit would block everyone.
    /// </summary>
    public static IServiceCollection AddProxySupport(this IServiceCollection services, IConfiguration cfg) =>
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var ip in cfg.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
                o.KnownProxies.Add(IPAddress.Parse(ip));
            foreach (var net in cfg.GetSection("Proxy:KnownNetworks").Get<string[]>() ?? [])
                o.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(net));
        });

    /// <summary>
    /// Pins the request's scheme and host to <c>Site:BaseUrl</c>, so canonical links, sitemaps and JSON-LD
    /// built from the request can never carry a forged Host header.
    /// </summary>
    public static IApplicationBuilder UseCanonicalHost(this IApplicationBuilder app, IConfiguration cfg)
    {
        if (!Uri.TryCreate(cfg["Site:BaseUrl"], UriKind.Absolute, out var site)) return app;
        var host = HostString.FromUriComponent(site.Authority); // Authority leaves out a default port
        return app.Use((ctx, next) =>
        {
            ctx.Request.Scheme = site.Scheme;
            ctx.Request.Host = host;
            return next(ctx);
        });
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h.XContentTypeOptions = "nosniff";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        // No script/style policy yet: the pages use inline scripts and Google Fonts. These parts are safe now.
        h.ContentSecurityPolicy = "frame-ancestors 'self'; base-uri 'self'; object-src 'none'; form-action 'self'";
        h.XFrameOptions = "SAMEORIGIN";
        return next(ctx);
    });
}
