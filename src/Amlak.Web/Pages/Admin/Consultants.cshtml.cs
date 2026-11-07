using System.ComponentModel.DataAnnotations;
using Amlak.Application.Consultants;
using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

/// <summary>Staff add, edit and hide the consultants shown on /مشاوران and on listing pages.</summary>
public class ConsultantsModel(AppDbContext db, ConsultantCommands consultants) : PageModel
{
    public class InputModel
    {
        public int? Id { get; set; }
        [Required(ErrorMessage = "نام را وارد کنید")] public string Name { get; set; } = "";
        [Required(ErrorMessage = "عنوان شغلی را وارد کنید")] public string Title { get; set; } = "مشاور املاک";
        [Required(ErrorMessage = "شمارهٔ موبایل را وارد کنید")] public string Phone { get; set; } = "";
        public string? Cities { get; set; }
        public int YearsOfExperience { get; set; }
        public string? Specialty { get; set; }
        public string? Bio { get; set; }
        public string? PhotoUrl { get; set; }
        public int SortOrder { get; set; }
    }

    public List<Consultant> Items { get; private set; } = [];
    /// <summary>Leads per consultant that nobody has handled yet.</summary>
    public Dictionary<int, int> OpenLeads { get; private set; } = [];
    [BindProperty] public InputModel Input { get; set; } = new();
    public bool Editing => Input.Id is not null;

    public async Task<IActionResult> OnGetAsync(int? edit)
    {
        await LoadAsync();
        if (edit is null) return Page();
        var c = Items.FirstOrDefault(x => x.Id == edit);
        if (c is null) return NotFound();
        Input = new InputModel
        {
            Id = c.Id, Name = c.Name, Title = c.Title, Phone = c.Phone, Cities = c.Cities, YearsOfExperience = c.YearsOfExperience,
            Specialty = c.Specialty, Bio = c.Bio, PhotoUrl = c.PhotoUrl, SortOrder = c.SortOrder,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (!PhoneNumber.TryParse(Input.Phone, out var phone) && !string.IsNullOrWhiteSpace(Input.Phone))
            ModelState.AddModelError("Input.Phone", "شمارهٔ موبایل معتبر نیست (مثل ۰۹۱۲۱۲۳۴۵۶۷)");
        if (ModelState.IsValid)
        {
            var profile = new ConsultantProfile(Input.Name, Input.Title, phone, Input.Cities, Input.YearsOfExperience,
                Input.Specialty, Input.Bio, Input.PhotoUrl, Input.SortOrder);
            string? error;
            if (Input.Id is { } id)
            {
                var updated = await consultants.UpdateAsync(id, profile, ct);
                if (updated.IsNotFound) return NotFound();
                error = updated.Error;
            }
            else error = (await consultants.CreateAsync(profile, ct)).Error;
            if (error is null) return RedirectToPage();
            ModelState.AddModelError("", error);
        }
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken ct) =>
        (await consultants.ToggleActiveAsync(id, ct)).Succeeded ? RedirectToPage() : NotFound();

    private async Task LoadAsync()
    {
        Items = await db.Consultants.AsNoTracking().OrderByDescending(c => c.Active).ThenBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync();
        OpenLeads = await db.Leads.AsNoTracking().Where(l => !l.Handled && l.ConsultantId != null)
            .GroupBy(l => l.ConsultantId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
    }
}
