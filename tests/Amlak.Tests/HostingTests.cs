using System.Net;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Amlak.Tests;

/// <summary>The proxy, host and header middleware, on a minimal in-memory server.</summary>
public class HostingTests
{
    private static async Task<HttpClient> Server(Dictionary<string, string?> cfg)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(cfg).Build();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProxySupport(config);
        var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseCanonicalHost(config);
        app.UseSecurityHeaders();
        app.MapGet("/", (HttpContext c) => $"{c.Connection.RemoteIpAddress}|{c.Request.Scheme}://{c.Request.Host}");
        await app.StartAsync();
        return app.GetTestClient();
    }

    [Fact]
    public async Task Forwarded_client_ip_is_used_when_the_proxy_is_trusted()
    {
        // TestServer's connection has no remote IP, which the middleware treats like a local proxy.
        var client = await Server(new() { ["Site:BaseUrl"] = "https://ashiyaneh.ir" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/");
        req.Headers.Add("X-Forwarded-For", "5.6.7.8");
        var body = await (await client.SendAsync(req)).Content.ReadAsStringAsync();
        Assert.Equal("5.6.7.8|https://ashiyaneh.ir", body);
    }

    [Fact]
    public async Task A_forged_host_header_is_replaced_by_the_configured_site()
    {
        var client = await Server(new() { ["Site:BaseUrl"] = "https://ashiyaneh.ir" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/");
        req.Headers.Host = "evil.example";
        var body = await (await client.SendAsync(req)).Content.ReadAsStringAsync();
        Assert.EndsWith("|https://ashiyaneh.ir", body);
    }

    [Fact]
    public async Task Security_headers_are_sent()
    {
        var client = await Server(new());
        var res = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("frame-ancestors 'self'", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("strict-origin-when-cross-origin", res.Headers.GetValues("Referrer-Policy").Single());
    }
}
