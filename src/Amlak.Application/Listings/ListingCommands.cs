using Amlak.Application.Abstractions;

namespace Amlak.Application.Listings;

/// <summary>
/// Write use cases for listings. Each one loads the aggregate, calls its methods (which keep the rules and
/// raise the domain events) and commits. Side effects (messenger posts, admin alerts, file cleanup, SMS
/// alerts) follow from the saved events in the background, never from here.
/// </summary>
public sealed class ListingCommands(IListingRepository listings, IUnitOfWork uow, TimeProvider clock)
{
    /// <summary>Open submissions one phone may have waiting for an admin at a time.</summary>
    public const int MaxPendingPerOwner = 5;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Creates (<paramref name="id"/> null) or updates a listing from the admin form.</summary>
    /// <param name="newPhotos">Files already stored; on failure they are not attached and the caller deletes them.</param>
    /// <returns>The listing id.</returns>
    public async Task<Result<int>> SaveAsync(int? id, ListingForm form, ListingStatus status, PriceDisplay priceDisplay,
        IReadOnlyList<PropertyPhoto> newPhotos, CancellationToken ct = default)
    {
        Property? p = null;
        if (id is not null && (p = await listings.GetAsync(id.Value, ct)) is null) return Result<int>.NotFound();
        var now = Now;
        try
        {
            var details = form.Details();
            var pricing = form.Pricing();
            if (p is null)
            {
                p = Property.Create(await ListingSlugs.UniqueAsync(listings, details.Title, ct), details, pricing, now);
                p.ChangeStatus(status, now);
                listings.Add(p);
            }
            else if (status == ListingStatus.Published)
            {
                p.Update(details, pricing, now);
                p.ChangeStatus(status, now);
            }
            else
            {
                // Leave the live state first, so a draft may be saved without a complete price.
                p.ChangeStatus(status, now);
                p.Update(details, pricing, now);
            }
            p.ShowPriceAs(priceDisplay, now);
            foreach (var photo in newPhotos) p.AddPhoto(photo, now);
        }
        catch (DomainException ex)
        {
            return Result<int>.Fail(ex.Message);
        }
        await uow.SaveChangesAsync(ct);
        return p.Id;
    }

    /// <summary>A visitor's listing from /ثبت-آگهی; it waits as Pending until an admin publishes it.</summary>
    /// <param name="ownerPhone">The mobile the visitor proved with an SMS code.</param>
    /// <param name="photos">Files already stored; on failure the caller deletes them.</param>
    public async Task<Result<int>> SubmitAsync(string ownerName, string ownerPhone, ListingForm form,
        IReadOnlyList<PropertyPhoto> photos, CancellationToken ct = default)
    {
        if (await listings.CountPendingByOwnerAsync(ownerPhone, ct) >= MaxPendingPerOwner)
            return Result<int>.Fail("چند آگهی شما هنوز در انتظار بررسی است. پس از بررسی آن‌ها آگهی تازه بفرستید.");
        var now = Now;
        Property p;
        try
        {
            var details = form.Details();
            p = Property.Submit(await ListingSlugs.UniqueAsync(listings, details.Title, ct), details, form.Pricing(), ownerName, ownerPhone, now);
            foreach (var photo in photos) p.AddPhoto(photo, now);
        }
        catch (DomainException ex)
        {
            return Result<int>.Fail(ex.Message);
        }
        listings.Add(p);
        await uow.SaveChangesAsync(ct);
        return p.Id;
    }

    public Task<Result> TogglePublishedAsync(int id, CancellationToken ct = default) =>
        ChangeAsync(id, p => p.TogglePublished(Now), ct);

    public async Task<Result> MovePhotoAsync(int id, int photoId, PhotoOrder.Move move, CancellationToken ct = default)
    {
        var p = await listings.GetAsync(id, ct);
        if (p is null || !p.MovePhoto(photoId, move, Now)) return Result.NotFound();
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <returns>The removed photo: the caller deletes its file now that the database no longer points at it.</returns>
    public async Task<Result<PropertyPhoto>> RemovePhotoAsync(int id, int photoId, CancellationToken ct = default)
    {
        var p = await listings.GetAsync(id, ct);
        if (p?.RemovePhoto(photoId, Now) is not { } photo) return Result<PropertyPhoto>.NotFound();
        await uow.SaveChangesAsync(ct);
        return photo;
    }

    /// <summary>Deletes the listing. Its messenger messages and photo files are removed after the save, from ListingDeleted.</summary>
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var p = await listings.GetAsync(id, ct);
        if (p is null) return Result.NotFound();
        p.MarkDeleted(Now);
        listings.Remove(p);
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<Result> ChangeAsync(int id, Action<Property> change, CancellationToken ct)
    {
        var p = await listings.GetAsync(id, ct);
        if (p is null) return Result.NotFound();
        try
        {
            change(p);
        }
        catch (DomainException ex)
        {
            return Result.Fail(ex.Message);
        }
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
