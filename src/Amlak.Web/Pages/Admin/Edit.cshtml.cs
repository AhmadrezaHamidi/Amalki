using System.ComponentModel.DataAnnotations;
using Amlak.Application.Listings;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

/// <summary>Admin listing form. Reads the listing directly; every change goes through <see cref="ListingCommands"/>.</summary>
public class EditModel(AppDbContext db, ListingCommands listings, PhotoStorage storage, ChannelPublisher publisher) : PageModel
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
        public PriceDisplay PriceDisplay { get; set; } = PriceDisplay.Full;
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
    public IReadOnlyCollection<Messenger> Configured => publisher.ConfiguredChannels;

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
            HasElevator = p.HasElevator, HasStorage = p.HasStorage, HasBalcony = p.HasBalcony, Deed = p.Deed, Convertible = p.Convertible, PriceDisplay = p.PriceDisplay,
            Address = p.Address, Latitude = p.Latitude, Longitude = p.Longitude,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        Validate();
        if (!ModelState.IsValid) return await RedisplayAsync(id);

        // Check every upload before storing any, so a bad second file leaves no orphaned first file.
        var uploads = Uploads.Where(f => f.Length > 0).ToList();
        if (uploads.FirstOrDefault(f => !PhotoStorage.LooksValid(f)) is { } bad) return await RejectFileAsync(id, bad, []);
        var stored = new List<PropertyPhoto>();
        foreach (var file in uploads)
        {
            if (await storage.SaveAsync(file) is not { } photo) return await RejectFileAsync(id, file, stored);
            stored.Add(photo);
        }

        // Channel posts follow from the saved events (OutboxDispatcher), outside this request.
        var saved = await listings.SaveAsync(id, ToForm(), Input.Status, Input.PriceDisplay, stored, ct);
        if (saved.Succeeded) return RedirectToPage("Edit", new { id = saved.Value });
        foreach (var photo in stored) await storage.DeleteAsync(photo);
        if (saved.IsNotFound) return NotFound();
        ModelState.AddModelError("", saved.Error!);
        return await RedisplayAsync(id);
    }

    public async Task<IActionResult> OnPostDeletePhotoAsync(int id, int photoId, CancellationToken ct)
    {
        var removed = await listings.RemovePhotoAsync(id, photoId, ct);
        if (!removed.Succeeded) return NotFound();
        // The file goes only after the database no longer points at it.
        await storage.DeleteAsync(removed.Value!);
        return RedirectToPage("Edit", new { id });
    }

    public async Task<IActionResult> OnPostMovePhotoAsync(int id, int photoId, PhotoOrder.Move move, CancellationToken ct) =>
        (await listings.MovePhotoAsync(id, photoId, move, ct)).Succeeded ? RedirectToPage("Edit", new { id }) : NotFound();

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

    private ListingForm ToForm() => new()
    {
        Title = Input.Title, Description = Input.Description, Deal = Input.Deal, Kind = Input.Kind, AreaM2 = Input.AreaM2,
        City = Input.City, District = Input.District, Price = Input.Price, Deposit = Input.Deposit, MonthlyRent = Input.MonthlyRent,
        Convertible = Input.Convertible, Bedrooms = Input.Bedrooms, Floor = Input.Floor, Parking = Input.Parking, YearBuilt = Input.YearBuilt,
        Address = Input.Address, Latitude = Input.Latitude, Longitude = Input.Longitude,
        HasElevator = Input.HasElevator, HasStorage = Input.HasStorage, HasBalcony = Input.HasBalcony, Deed = Input.Deed,
    };

    private async Task<IActionResult> RejectFileAsync(int? id, IFormFile file, List<PropertyPhoto> stored)
    {
        foreach (var photo in stored) await storage.DeleteAsync(photo);
        ModelState.AddModelError("", $"فایل «{Path.GetFileName(file.FileName)}» معتبر نیست (فقط JPG، PNG یا WebP تا ۵ مگابایت).");
        return await RedisplayAsync(id);
    }

    /// <summary>Shows the form again with what was typed, next to the saved listing's photos and posts.</summary>
    private async Task<IActionResult> RedisplayAsync(int? id)
    {
        if (id is null) return Page();
        var p = await db.Properties.AsNoTracking().Include(x => x.Photos).Include(x => x.ChannelPosts).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        Id = p.Id; Slug = p.Slug; OwnerName = p.OwnerName; OwnerPhone = p.OwnerPhone; Photos = p.Photos.OrderBy(x => x.SortOrder).ToList(); Posts = p.ChannelPosts;
        return Page();
    }
}
