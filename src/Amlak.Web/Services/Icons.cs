using Microsoft.AspNetCore.Html;

namespace Amlak.Web.Services;

/// <summary>Inline stroke icons (no icon font, no emoji in the UI). Decorative: hidden from screen readers.</summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["home"] = "<path d='M3 10.5 12 3l9 7.5'/><path d='M5 9.5V21h14V9.5'/><path d='M10 21v-6h4v6'/>",
        ["search"] = "<circle cx='11' cy='11' r='7'/><path d='m20 20-3.5-3.5'/>",
        ["heart"] = "<path d='M19 14c1.5-1.5 3-3.2 3-5.5A5.5 5.5 0 0 0 12 5.5 5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4 3 5.5l7 7Z'/>",
        ["phone"] = "<path d='M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .4 1.9.7 2.8a2 2 0 0 1-.5 2.1L8 9.9a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.4c.9.3 1.8.6 2.8.7a2 2 0 0 1 1.7 2Z'/>",
        ["pin"] = "<path d='M12 22s7-6.2 7-12a7 7 0 0 0-14 0c0 5.8 7 12 7 12Z'/><circle cx='12' cy='10' r='2.5'/>",
        ["shield"] = "<path d='M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z'/><path d='m9 12 2 2 4-4'/>",
        ["photo"] = "<rect x='3' y='5' width='18' height='14' rx='2'/><circle cx='9' cy='11' r='2'/><path d='m21 17-5-5-8 7'/>",
        ["user"] = "<circle cx='12' cy='8' r='4'/><path d='M4 21a8 8 0 0 1 16 0'/>",
        ["share"] = "<circle cx='18' cy='5' r='3'/><circle cx='6' cy='12' r='3'/><circle cx='18' cy='19' r='3'/><path d='m8.6 13.5 6.8 4M15.4 6.5l-6.8 4'/>",
        ["menu"] = "<path d='M4 6h16M4 12h16M4 18h16'/>",
        ["close"] = "<path d='M6 6l12 12M18 6 6 18'/>",
        ["chev-left"] = "<path d='m15 18-6-6 6-6'/>",
        ["chev-right"] = "<path d='m9 18 6-6-6-6'/>",
        ["area"] = "<path d='M3 3h18v18H3z'/><path d='M3 9h6V3'/>",
        ["bed"] = "<path d='M2 20v-8a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v8M2 16h20M6 10V6h12v4'/>",
        ["filter"] = "<path d='M3 5h18M6 12h12M10 19h4'/>",
        ["send"] = "<path d='M22 2 11 13M22 2l-7 20-4-9-9-4Z'/>",
        ["apartment"] = "<path d='M4 21V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v16M16 9h2a2 2 0 0 1 2 2v10M2 21h20M8 7h4M8 11h4M8 15h4'/>",
        ["villa"] = "<path d='M3 10.5 12 3l9 7.5M5 9.5V21h14V9.5M10 21v-6h4v6'/>",
        ["land"] = "<path d='M3 20h18M5 20l4-9 4 5 3-4 3 8M12 4v3'/>",
        ["commercial"] = "<path d='M3 9h18l-1.5-5h-15ZM4 9v11h16V9M9 20v-6h6v6'/>",
        ["office"] = "<path d='M3 7h18v13H3ZM8 7V4h8v3M3 13h18'/>",
    };

    public static IHtmlContent Get(string name, int size = 20, string? cls = null) =>
        new HtmlString($"<svg aria-hidden=\"true\" class=\"ico {cls}\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{Paths[name]}</svg>");

    public static IHtmlContent Kind(PropertyKind k, int size = 24) => Get(k switch
    {
        PropertyKind.Apartment => "apartment",
        PropertyKind.Villa => "villa",
        PropertyKind.Land => "land",
        PropertyKind.Commercial => "commercial",
        _ => "office",
    }, size);
}
