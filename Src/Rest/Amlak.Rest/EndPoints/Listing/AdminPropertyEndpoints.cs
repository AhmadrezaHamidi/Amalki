using Amlak.Rest.EndPoints.Listing;

namespace Amlak.Rest.EndPoints;

/// <summary>Endpoints مدیریت آگهی و عکس‌ها — فقط مدیر</summary>
public class AdminPropertyEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ListingConstants.Routes.AdminBase)
            .WithApiVersionSet()
            .WithTags(ListingConstants.AdminTag)
            .RequireRoles(AmlakRoles.Admin);

        group.MapGetEndpoint<PagedResult<PropertyAdminListItem>>(ListingConstants.Routes.AdminList, List, ListingConstants.Names.AdminList, ListingConstants.Docs.AdminList.Summary, ListingConstants.Docs.AdminList.Description);
        group.MapGetEndpoint<PropertyDetail>(                    ListingConstants.Routes.AdminGet,  Get,  ListingConstants.Names.AdminGet,  ListingConstants.Docs.AdminGet.Summary,  ListingConstants.Docs.AdminGet.Description);

        group.MapPostEndpoint<CreatePropertyResponse>(ListingConstants.Routes.Create,    Post,        ListingConstants.Names.Create,    ListingConstants.Docs.Create.Summary,    ListingConstants.Docs.Create.Description);
        group.MapPutEndpoint(                         ListingConstants.Routes.Update,    Put,         ListingConstants.Names.Update,    ListingConstants.Docs.Update.Summary,    ListingConstants.Docs.Update.Description);
        group.MapPutEndpoint(                         ListingConstants.Routes.Publish,   PutPublish,  ListingConstants.Names.Publish,   ListingConstants.Docs.Publish.Summary,   ListingConstants.Docs.Publish.Description);
        group.MapPutEndpoint(                         ListingConstants.Routes.Unpublish, PutUnpublish, ListingConstants.Names.Unpublish, ListingConstants.Docs.Unpublish.Summary, ListingConstants.Docs.Unpublish.Description);
        group.MapPutEndpoint(                         ListingConstants.Routes.Close,     PutClose,    ListingConstants.Names.Close,     ListingConstants.Docs.Close.Summary,     ListingConstants.Docs.Close.Description);
        group.MapDeleteEndpoint<bool>(                ListingConstants.Routes.Delete,    Delete,      ListingConstants.Names.Delete,    ListingConstants.Docs.Delete.Summary,    ListingConstants.Docs.Delete.Description);

        // آپلود multipart است و توکن JWT در هدر دارد، پس antiforgery لازم نیست
        group.MapPostEndpoint<long>(ListingConstants.Routes.AddPhoto, PostPhoto, ListingConstants.Names.AddPhoto, ListingConstants.Docs.AddPhoto.Summary, ListingConstants.Docs.AddPhoto.Description)
             .DisableAntiforgery();
        group.MapDeleteEndpoint<bool>(ListingConstants.Routes.RemovePhoto,  DeletePhoto,  ListingConstants.Names.RemovePhoto,  ListingConstants.Docs.RemovePhoto.Summary,  ListingConstants.Docs.RemovePhoto.Description);
        group.MapPutEndpoint(         ListingConstants.Routes.ReorderPhoto, PutOrder,     ListingConstants.Names.ReorderPhoto, ListingConstants.Docs.ReorderPhoto.Summary, ListingConstants.Docs.ReorderPhoto.Description);
        group.MapPutEndpoint(         ListingConstants.Routes.CoverPhoto,   PutCover,     ListingConstants.Names.CoverPhoto,   ListingConstants.Docs.CoverPhoto.Summary,   ListingConstants.Docs.CoverPhoto.Description);
        group.MapPutEndpoint(         ListingConstants.Routes.PhotoAlt,     PutAlt,       ListingConstants.Names.PhotoAlt,     ListingConstants.Docs.PhotoAlt.Summary,     ListingConstants.Docs.PhotoAlt.Description);
    }

    private static async Task<PagedResult<PropertyAdminListItem>> List([AsParameters] GetAdminPropertiesQuery query, IQueryBus bus, CancellationToken ct)
        => await bus.DispatchAsync<PagedResult<PropertyAdminListItem>>(query, ct);

    private static async Task<IResult> Get(long id, IQueryBus bus, CancellationToken ct)
        => (await bus.DispatchAsync<PropertyDetail?>(new GetPropertyQuery(id), ct)).OkOrNotFound();

    private static async Task<CreatePropertyResponse> Post([FromBody] CreatePropertyCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<CreatePropertyResponse>(cmd, ct);

    private static async Task<bool> Put(long id, [FromBody] UpdatePropertyCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(cmd with { Id = id }, ct);

    private static async Task<bool> PutPublish(long id, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(new PublishPropertyCommand(id), ct);

    private static async Task<bool> PutUnpublish(long id, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(new UnpublishPropertyCommand(id), ct);

    private static async Task<bool> PutClose(long id, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(new ClosePropertyCommand(id), ct);

    private static async Task<bool> Delete(long id, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(new DeletePropertyCommand(id), ct);

    private static async Task<long> PostPhoto(long id, IFormFile file, [FromForm] string? alt, ICommandBus bus, CancellationToken ct)
    {
        // فایل بزرگ پیش از خواندن در حافظه رد می‌شود؛ بررسی نوع و محتوا در دامین است
        if (file.Length > Amlak.Domain.Listing.PhotoFile.MaxBytes)
            throw new Amlak.Domain.Listing.Exceptions.PhotoTooLargeException();

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return await bus.Dispatch<long>(new AddPropertyPhotoCommand(id, file.FileName, buffer.ToArray(), alt), ct);
    }

    private static async Task<bool> DeletePhoto(long id, long photoId, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(new RemovePropertyPhotoCommand(id, photoId), ct);

    private static async Task<bool> PutOrder(long id, [FromBody] ReorderPropertyPhotosCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(cmd with { PropertyId = id }, ct);

    private static async Task<bool> PutCover(long id, long photoId, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(new SetCoverPhotoCommand(id, photoId), ct);

    private static async Task<bool> PutAlt(long id, long photoId, [FromBody] ChangePhotoAltCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(cmd with { PropertyId = id, PhotoId = photoId }, ct);
}
