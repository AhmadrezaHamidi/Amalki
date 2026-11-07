using Microsoft.AspNetCore.Html;

namespace Amlak.Web.Services;

/// <summary>
/// The Royal Fine Homes emblem (arc, tower, roof and five stars from the client's logo), redrawn as one-colour SVG
/// so it takes the colour of its text. The full logo is wwwroot/img/brand/royal-logo.jpg; app icons are in wwwroot/icons.
/// </summary>
public static class Brand
{
    private const string Inner = "<g fill='currentColor' stroke='none'><path d='M42.4 14.7A44 44 0 0 0 11.9 80M57.6 14.7A44 44 0 0 1 88.1 80' fill='none' stroke='currentColor' stroke-width='3.4' stroke-linecap='round'/><rect x='49.3' y='2' width='1.4' height='24'/><path d='M45 70V31l2-2.5V26h6v2.5l2 2.5v39z'/><rect x='40' y='38' width='1.7' height='31'/><rect x='37' y='42' width='1.7' height='28'/><rect x='34' y='46' width='1.7' height='25'/><rect x='58.3' y='38' width='1.7' height='31'/><rect x='61.3' y='42' width='1.7' height='28'/><rect x='64.3' y='46' width='1.7' height='25'/><path d='M27 74.5l5-13v11.7zM73 74.5l-5-13v11.7z'/><path d='M12 83.5L50 69l38 14.5' fill='none' stroke='currentColor' stroke-width='3.2' stroke-linejoin='round'/><rect x='47.7' y='73.4' width='2' height='2'/><rect x='50.3' y='73.4' width='2' height='2'/><rect x='47.7' y='76' width='2' height='2'/><rect x='50.3' y='76' width='2' height='2'/><polygon points='22.00,83.80 22.54,85.25 24.09,85.32 22.88,86.29 23.29,87.78 22.00,86.92 20.71,87.78 21.12,86.29 19.91,85.32 21.46,85.25'/><polygon points='35.00,83.40 35.64,85.12 37.47,85.20 36.04,86.34 36.53,88.10 35.00,87.09 33.47,88.10 33.96,86.34 32.53,85.20 34.36,85.12'/><polygon points='50.00,82.80 50.79,84.91 53.04,85.01 51.28,86.42 51.88,88.59 50.00,87.34 48.12,88.59 48.72,86.42 46.96,85.01 49.21,84.91'/><polygon points='65.00,83.40 65.64,85.12 67.47,85.20 66.04,86.34 66.53,88.10 65.00,87.09 63.47,88.10 63.96,86.34 62.53,85.20 64.36,85.12'/><polygon points='78.00,83.80 78.54,85.25 80.09,85.32 78.88,86.29 79.29,87.78 78.00,86.92 76.71,87.78 77.12,86.29 75.91,85.32 77.46,85.25'/><path d='M8 96Q50 89 92 96Q50 92.6 8 96z'/></g>";

    public static IHtmlContent Mark(int size = 28) =>
        new HtmlString($"<svg class='brand-emblem' width='{size}' height='{size}' viewBox='0 0 100 100' aria-hidden='true' focusable='false'>{Inner}</svg>");
}
