using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

public class EditModel(AppDbContext db, PhotoStorage storage, ChannelPublisher publisher, TimeProvider clock) : PageModel
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
    /// <summary>Who sent the listing in from /ثبت-آگهی; null for listings the admin created.</summary>
    public string? OwnerName { get; private set; }
    public string? OwnerPhone { get; private set; }
    public List<ChannelPost> Posts { get; private set; } = [];
    public IReadOnlyCollection<Channel> Configured => publisher.ConfiguredChannels;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return Page();
        var p = await db.Properties.AsNoTracking().Include(x => x.Photos.OrderBy(f => f.SortOrder)).Include(x => x.ChannelPosts).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        Id = p.Id; Slug = p.Slug; Photos = p.Photos.ToList(); Posts = p.ChannelPosts; OwnerName = p.OwnerName; OwnerPhone = p.OwnerPhone;
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

        // Check every upload before storing any, so a bad second file leaves no orphaned first file.
        var uploads = Uploads.Where(f => f.Length > 0).ToList();
        if (uploads.FirstOrDefault(f => !PhotoStorage.LooksValid(f)) is { } bad)
        {
            ModelState.AddModelError("", $"فایل «{Path.GetFileName(bad.FileName)}» معتبر نیست (فقط JPG، PNG یا WebP تا ۵ مگابایت).");
            return await Redisplay(p);
        }

        var isNew = p is null;
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            var details = ListingDetails.Of(Input.Title, Input.Description, Input.Kind, Input.AreaM2, Input.City, Input.District,
                Input.Bedrooms, Input.Floor, Input.Parking, Input.YearBuilt, Input.Address, Input.Latitude, Input.Longitude)
                with { HasElevator = Input.HasElevator, HasStorage = Input.HasStorage, HasBalcony = Input.HasBalcony, Deed = Input.Deed };
            var pricing = Pricing.For(Input.Deal, Input.Price, Input.Deposit, Input.MonthlyRent, Input.Convertible);
            if (p is null)
            {
                p = Property.Create(await Slugger.UniqueAsync(db, details.Title), details, pricing, now);
                p.ChangeStatus(Input.Status, now);
                db.Properties.Add(p);
            }
            else if (Input.Status == ListingStatus.Published)
            {
                p.Update(details, pricing, now);
                p.ChangeStatus(Input.Status, now);
            }
            else
            {
                // Leave the live state first, so a draft may be saved without a complete price.
                p.ChangeStatus(Input.Status, now);
                p.Update(details, pricing, now);
            }
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await Redisplay(isNew ? null : p);
        }

        foreach (var file in uploads)
        {
            var photo = await storage.SaveAsync(file);
            if (photo is null)
            {
                ModelState.AddModelError("", $"فایل «{Path.GetFileName(file.FileName)}» معتبر نیست (فقط JPG، PNG یا WebP تا ۵ مگابایت).");
                return await Redisplay(isNew ? null : p);
            }
            p.AddPhoto(photo, now);
        }

        // Channel posts follow from the saved events (OutboxDispatcher), outside this request.
        await db.SaveChangesAsync();
        return RedirectToPage("Edit", new { id = p.Id });
    }

    public async Task<IActionResult> OnPostDeletePhotoAsync(int id, int photoId)
    {
        var p = await db.Properties.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == id);
        if (p?.RemovePhoto(photoId, clock.GetUtcNow().UtcDateTime) is not { } photo) return NotFound();
        await db.SaveChangesAsync();
        // The file goes only after the database no longer points at it.
        await storage.DeleteAsync(photo);
        return RedirectToPage("Edit", new { id });
    }

    public async Task<IActionResult> OnPostMovePhotoAsync(int id, int photoId, PhotoOrder.Move move)
    {
        var p = await db.Properties.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null || !p.MovePhoto(photoId, move, clock.GetUtcNow().UtcDateTime)) return NotFound();
        await db.SaveChangesAsync();
        return RedirectToPage("Edit", new { id });
    }

    public async Task<IActionResult> OnPostRepublishAsync(int id)
    {
        await publisher.SyncAsync(id);
        return RedirectToPage("Edit", new { id });
    }

    private void Validate()
    {
        if (Input.Deal != DealType.Rent && Input.Price is null or 0)
            ModelState.AddModelError("Input.Price", Input.Deal == DealType.Sale ? "برای فروش، قیمت لازم است" : "قیمت هر شب لازم است");
        if (Input.Deal == DealType.Rent && Input.Deposit is null && Input.MonthlyRent is null)
            ModelState.AddModelError("Input.Deposit", "رهن یا اجارهٔ ماهانه را وارد کنید");
    }

    private Task<IActionResult> Redisplay(Property? p)
    {
        Id = p?.Id; Slug = p?.Slug; OwnerName = p?.OwnerName; OwnerPhone = p?.OwnerPhone; Photos = p?.Photos.OrderBy(x => x.SortOrder).ToList() ?? []; Posts = p?.ChannelPosts ?? [];
        return Task.FromResult<IActionResult>(Page());
    }
}
