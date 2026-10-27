using System.Text.Json;

namespace Amlak.Tests;

/// <summary>The installable-app files in wwwroot: a broken manifest or icon path silently disables "install".</summary>
public class PwaTests
{
    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Amlak.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Amlak.Web", "wwwroot");
    }

    [Fact]
    public void Manifest_is_valid_rtl_standalone_and_every_icon_exists()
    {
        var root = WebRoot();
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.webmanifest")));
        var m = doc.RootElement;
        Assert.Equal("rtl", m.GetProperty("dir").GetString());
        Assert.Equal("fa", m.GetProperty("lang").GetString());
        Assert.Equal("standalone", m.GetProperty("display").GetString());
        Assert.StartsWith(m.GetProperty("scope").GetString()!, m.GetProperty("start_url").GetString()!);

        var icons = m.GetProperty("icons").EnumerateArray().ToList();
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "192x192");
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "512x512");
        Assert.Contains(icons, i => i.TryGetProperty("purpose", out var p) && p.GetString() == "maskable");
        foreach (var src in icons.Select(i => i.GetProperty("src").GetString()!))
            Assert.True(File.Exists(Path.Combine(root, src.TrimStart('/'))), $"missing icon {src}");
    }

    [Fact]
    public void Service_worker_precaches_offline_page_and_never_caches_admin_or_posts()
    {
        var root = WebRoot();
        Assert.True(File.Exists(Path.Combine(root, "offline.html")));
        var sw = File.ReadAllText(Path.Combine(root, "sw.js"));
        Assert.Contains("'/offline.html'", sw);
        Assert.Contains("if (req.method !== 'GET') return;", sw);
        Assert.Contains("(admin|account)", sw);
    }
}
