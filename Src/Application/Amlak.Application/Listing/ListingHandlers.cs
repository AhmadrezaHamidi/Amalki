using Amlak.Domain.Listing;
using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Args;
using Amlak.Domain.Listing.Exceptions;

namespace Amlak.Application.Listing;

/// <summary>Handler های مدیریت آگهی و عکس‌ها</summary>
public sealed class ListingHandlers(
    IPropertyRepository propertyRepo,
    IPhotoStorage       photoStorage,
    AmlakDbContext      db) :
    ICommandHandler<CreatePropertyCommand, CreatePropertyResponse>,
    ICommandHandler<UpdatePropertyCommand, bool>,
    ICommandHandler<PublishPropertyCommand, bool>,
    ICommandHandler<UnpublishPropertyCommand, bool>,
    ICommandHandler<ClosePropertyCommand, bool>,
    ICommandHandler<DeletePropertyCommand, bool>,
    ICommandHandler<AddPropertyPhotoCommand, long>,
    ICommandHandler<RemovePropertyPhotoCommand, bool>,
    ICommandHandler<ReorderPropertyPhotosCommand, bool>,
    ICommandHandler<SetCoverPhotoCommand, bool>,
    ICommandHandler<ChangePhotoAltCommand, bool>
{
    public async Task<CreatePropertyResponse> Handle(CreatePropertyCommand c, CancellationToken token)
    {
        var slug = await UniqueSlugAsync(Slug.From(c.Title), token);
        var id   = await propertyRepo.GetNextIdAsync();

        var property = Property.Create(new CreatePropertyArg(id, slug, new PropertyDetailsArg(
            c.Title, c.Description, c.Deal, c.Kind, c.Price, c.Deposit, c.MonthlyRent,
            c.AreaM2, c.Bedrooms, c.Floor, c.Parking, c.YearBuilt,
            c.City, c.District, c.Address, c.Latitude, c.Longitude)));

        await propertyRepo.AddAsync(property, token);
        await db.SaveChangesAsync(token);
        return new CreatePropertyResponse(id, slug);
    }

    public async Task<bool> Handle(UpdatePropertyCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.Id, token);
        property.Update(new PropertyDetailsArg(
            c.Title, c.Description, c.Deal, c.Kind, c.Price, c.Deposit, c.MonthlyRent,
            c.AreaM2, c.Bedrooms, c.Floor, c.Parking, c.YearBuilt,
            c.City, c.District, c.Address, c.Latitude, c.Longitude));
        return await SaveAsync(property, token);
    }

    public async Task<bool> Handle(PublishPropertyCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.Id, token);
        property.Publish();
        return await SaveAsync(property, token);
    }

    public async Task<bool> Handle(UnpublishPropertyCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.Id, token);
        property.Unpublish();
        return await SaveAsync(property, token);
    }

    public async Task<bool> Handle(ClosePropertyCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.Id, token);
        property.Close();
        return await SaveAsync(property, token);
    }

    public async Task<bool> Handle(DeletePropertyCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.Id, token);
        property.Delete();
        return await SaveAsync(property, token);
    }

    public async Task<long> Handle(AddPropertyPhotoCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.PropertyId, token);
        if (property.Photos.Count >= Property.MaxPhotos) throw new PhotoLimitExceededException();

        var extension = PhotoFile.Validate(c.FileName, c.Content);
        var fileName  = await photoStorage.SaveAsync(extension, c.Content, token);
        try
        {
            var photo = property.AddPhoto(await propertyRepo.GetNextPhotoIdAsync(), fileName, c.Alt);
            await SaveAsync(property, token);
            return photo.Id;
        }
        catch
        {
            // فایل بی‌صاحب روی دیسک نماند
            photoStorage.Delete(fileName);
            throw;
        }
    }

    public async Task<bool> Handle(RemovePropertyPhotoCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.PropertyId, token);
        var fileName = property.RemovePhoto(c.PhotoId);
        await SaveAsync(property, token);

        // فایل فقط پس از ثبت موفق پاک می‌شود
        photoStorage.Delete(fileName);
        return true;
    }

    public async Task<bool> Handle(ReorderPropertyPhotosCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.PropertyId, token);
        property.ReorderPhotos(c.PhotoIds);
        return await SaveAsync(property, token);
    }

    public async Task<bool> Handle(SetCoverPhotoCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.PropertyId, token);
        property.SetCoverPhoto(c.PhotoId);
        return await SaveAsync(property, token);
    }

    public async Task<bool> Handle(ChangePhotoAltCommand c, CancellationToken token)
    {
        var property = await GetAsync(c.PropertyId, token);
        property.ChangePhotoAlt(c.PhotoId, c.Alt);
        return await SaveAsync(property, token);
    }

    /// <summary>«عنوان»، و اگر گرفته شده «عنوان-2»، «عنوان-3» …</summary>
    private async Task<string> UniqueSlugAsync(string baseSlug, CancellationToken ct)
    {
        for (var n = 1; ; n++)
        {
            var candidate = Slug.WithSuffix(baseSlug, n);
            if (!await propertyRepo.SlugExistsAsync(candidate, ct)) return candidate;
        }
    }

    private async Task<Property> GetAsync(long id, CancellationToken ct)
        => await propertyRepo.GetByIdAsync(id, ct) ?? throw new PropertyNotFoundException();

    private async Task<bool> SaveAsync(Property property, CancellationToken ct)
    {
        await propertyRepo.UpdateAsync(property, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
