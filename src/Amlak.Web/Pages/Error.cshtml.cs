using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Amlak.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }
    public int Code { get; private set; } = 500;

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet(int? code)
    {
        Code = code ?? 500;
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }

    // Status-code re-execution keeps the original method, so a failed POST lands here.
    public void OnPost(int? code) => OnGet(code);
}
