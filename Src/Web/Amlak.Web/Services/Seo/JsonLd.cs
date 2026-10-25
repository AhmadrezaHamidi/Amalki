using System.Text.Encodings.Web;
using System.Text.Json;

namespace Amlak.Web.Services.Seo;

public static class JsonLd
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes for embedding in a script tag; "&lt;/" is escaped so content cannot close the tag.</summary>
    public static string Serialize(object data) => JsonSerializer.Serialize(data, Options).Replace("</", "<\\/");

    public static object Breadcrumbs(params (string Name, string Url)[] items) => new Dictionary<string, object>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "BreadcrumbList",
        ["itemListElement"] = items.Select((x, i) => new Dictionary<string, object>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["name"] = x.Name,
            ["item"] = x.Url,
        }).ToArray(),
    };

    /// <summary>Organization + WebSite for the home page. Only facts we actually have; no invented contact data.</summary>
    public static object[] Site(string name, string baseUrl) =>
    [
        new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org", ["@type"] = "RealEstateAgent",
            ["name"] = name, ["url"] = baseUrl + "/", ["areaServed"] = "IR", ["inLanguage"] = "fa",
        },
        new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org", ["@type"] = "WebSite",
            ["name"] = name, ["url"] = baseUrl + "/", ["inLanguage"] = "fa",
        },
    ];
}
