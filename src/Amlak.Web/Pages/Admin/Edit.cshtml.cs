using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

public class EditModel(AppDbContext db, PhotoStorage storage, ChannelPublisher publisher) : PageModel
{
    public class InputModel
    {
        [Required(ErrorMessage = "عنوان لازم است"), StringLength(200)] public string Title { get; set; } = "";
        [Required(ErrorMessage = "توضیحات لازم است"), StringLength(4000)] public string Description { get; set; } = "";
        public DealType Deal { get; set; } = DealType.Sale;
        public PropertyKind Kind { get; set; } = PropertyKind.Apartment;
        public ListingStatus Status { get; set; } = ListingStatus.Draft;
        [Range(0, long.MaxValue)] public long? Price { get; set; }
        [Range(0, long.MaxValue)] public long? Deposit { get; set; }
        [Range(0, long.MaxValue)] public long? MonthlyRent { get; set; }
        [Range(1, 1_000_000, ErrorMessage = "متراژ نامعتبر است")] public int AreaM2 { get; set; }
        [Range(0, 50)] public int? Bedrooms { get; set; }
        [Range(-5, 200)] public int? Floor { get; set; }
        [Range(0, 50)] public int? Parking { get; set; }
        [Range(1200, 1500)] public int? YearBuilt { get; set; }
        public bool? HasElevator { get; set; }
        public bool? HasStorage { get; set; }
        public bool? HasBalcony { get; set; }
        public DeedType? Deed { get; set; }
        public bool Convertible { get; set; }
        [Required(ErrorMessage = "شهر لازم است"), StringLength(80)] public string City { get; set; } = "";
        [Required(ErrorMessage = "محله لازم است"), StringLength(80)] public string District { get; set; } = "";
        [StringLength(300)] public string? Address { get; set; }
        [Range(-90, 90)] public double? Latitude { get; set; }
        [Range(-180, 180)] public double? Longitude { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public List<IFormFile> Uploads { get; set; } = [];
    public List<PropertyPhoto> Photos { get; private set; } = [];
    public int? Id { get; private set; }
    public string? Slug { get; private set; }
    public List<ChannelPost> Posts { get; private set; } = [];
    public IReadOnlyCollection<Channel> Configured => publisher.ConfiguredChannels;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return Page();
        var p = await db.Properties.AsNoTracking().Include(x => x.Photos.OrderBy(f => f.SortOrder)).Include(x => x.ChannelPosts).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        Id = p.Id; Slug = p.Slug; Photos = p.Photos; Posts = p.ChannelPosts;
        Input = new InputModel
        {
            Title = p.Title, Description = p.Description, Deal = p.Deal, Kind = p.Kind, Status = p.Status,
            Price = p.Price, Deposit = p.Deposit, MonthlyRent = p.MonthlyRent, AreaM2 = p.AreaM2, Bedrooms = p.Bedrooms,
            Floor = p.Floor, Parking = p.Parking, YearBuilt = p.YearBuilt, City = p.City, District = p.District,
            HasElevator = p.HasElevator, HasStorage = p.HasStorage, HasBalcony = p.HasBalcony, Deed = p.Deed, Convertible = p.Convertible,
            Address = p.Address, Latitude = p.Latitude, Longitude = p.Longitude,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Validate();
        Property? p = null;
        if (id is not null)
        {
            p = await db.Properties.Include(x => x.Photos).Include(x => x.ChannelPosts).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return NotFound();
        }
        if (!ModelState.IsValid) return await Redisplay(p);

        var isNew = p is null;
        p ??= new Property();
        Apply(p);
        if (isNew)
        {
            p.Slug = await UniqueSlugAsync(Slugger.Make(Input.Title));
            db.Properties.Add(p);
        }

        var order = p.Photos.Count == 0 ? 0 : p.Photos.Max(x => x.SortOrder) + 1;
        foreach (var file in Uploads.Where(f => f.Length > 0))
        {
            var url = await storage.SaveAsync(file);
            if (url is null)
            {
                ModelState.AddModelError("", $"فایل «{Path.GetFileName(file.FileName)}» معتبر نیست (فقط JPG، PNG یا WebP تا ۵ مگابایت).");
                return await Redisplay(isNew ? null : p);
            }
            p.Photos.Add(new PropertyPhoto { Url = url, Alt = $"{p.Title} - {p.District} {p.City}", SortOrder = order++ });
        }

        await db.SaveChangesAsync();
        await publisher.SyncAsync(p.Id);
        return RedirectToPage("Edit", new { id = p.Id });
    }

    public async Task<IActionResult> OnPostDeletePhotoAsync(int id, int photoId)
    {
        var photo = await db.PropertyPhotos.FirstOrDefaultAsync(x => x.Id == photoId && x.PropertyId == id);
        if (photo is null) return NotFound();
        await storage.DeleteAsync(photo.Url);
        db.PropertyPhotos.Remove(photo);
        await db.SaveChangesAsync();
        // A published listing's channel album must lose the photo too.
        await publisher.SyncAsync(id);
        return RedirectToPage("Edit", new { id });
    }

    public async Task<IActionResult> OnPostRepublishAsync(int id)
    {
        await publisher.SyncAsync(id);
        return RedirectToPage("Edit", new { id });
    }

    private void Validate()
    {
        if (Input.Deal == DealType.Sale && Input.Price is null or 0)
            ModelState.AddModelError("Input.Price", "برای فروش، قیمت لازم است");
        if (Input.Deal == DealType.Rent && Input.Deposit is null && Input.MonthlyRent is null)
            ModelState.AddModelError("Input.Deposit", "رهن یا اجارهٔ ماهانه را وارد کنید");
    }

    private void Apply(Property p)
    {
        p.Title = Input.Title.Trim(); p.Description = Input.Description.Trim();
        p.Deal = Input.Deal; p.Kind = Input.Kind;
        p.Price = Input.Deal == DealType.Sale ? Input.Price : null;
        p.Deposit = Input.Deal == DealType.Rent ? Input.Deposit : null;
        p.MonthlyRent = Input.Deal == DealType.Rent ? Input.MonthlyRent : null;
        p.AreaM2 = Input.AreaM2; p.Bedrooms = Input.Bedrooms; p.Floor = Input.Floor;
        p.Parking = Input.Parking; p.YearBuilt = Input.YearBuilt;
        p.HasElevator = Input.HasElevator; p.HasStorage = Input.HasStorage; p.HasBalcony = Input.HasBalcony;
        p.Deed = Input.Deed; p.Convertible = Input.Deal == DealType.Rent && Input.Convertible;
        p.City = Input.City.Trim(); p.District = Input.District.Trim(); p.Address = Input.Address?.Trim();
        p.Latitude = Input.Latitude; p.Longitude = Input.Longitude;
        if (Input.Status == ListingStatus.Published && p.Status != ListingStatus.Published)
            p.PublishedAtUtc ??= DateTime.UtcNow;
        p.Status = Input.Status;
    }

    private async Task<string> UniqueSlugAsync(string baseSlug)
    {
        if (baseSlug.Length == 0) baseSlug = "ملک";
        var slug = baseSlug; var n = 2;
        while (await db.Properties.AnyAsync(x => x.Slug == slug)) slug = $"{baseSlug}-{n++}";
        return slug;
    }

    private Task<IActionResult> Redisplay(Property? p)
    {
        Id = p?.Id; Slug = p?.Slug; Photos = p?.Photos.OrderBy(x => x.SortOrder).ToList() ?? []; Posts = p?.ChannelPosts ?? [];
        return Task.FromResult<IActionResult>(Page());
    }
}
