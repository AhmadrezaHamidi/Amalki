using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Amlak.Web.Pages;

/// <summary>Side-by-side list of the ten UI variants (docs/UI-VARIANTS.md). Only while Ui:Preview is on.</summary>
public class DesignsModel(IConfiguration cfg) : PageModel
{
    public IActionResult OnGet() => UiVariants.Preview(cfg) ? Page() : NotFound();
}
