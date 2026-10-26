using Amlak.Application.Contract.Services;
using Amlak.Domain.Listing;
using Amlak.Rest.EndPoints.Listing;

namespace Amlak.Rest.EndPoints;

/// <summary>دریافت فایل عکس آگهی‌ها — نام فایل تصادفی است و فهرست‌پذیر نیست</summary>
public class PhotoEndpoints : IEndpoint
{
    /// <summary>نام فایل هرگز عوض نمی‌شود (هر آپلود نام تازه دارد)، پس کش طولانی امن است</summary>
    private const string CacheControl = "public, max-age=31536000, immutable";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGroup("")
           .WithApiVersionSet()
           .WithTags(ListingConstants.Tag)
           .AllowAnonymous()
           .MapGetEndpoint(ListingConstants.Routes.PhotoFile, Get, ListingConstants.Names.PhotoFile, ListingConstants.Docs.PhotoFile.Summary, ListingConstants.Docs.PhotoFile.Description);
    }

    private static IResult Get(string fileName, IPhotoStorage storage, HttpContext http)
    {
        if (storage.PathOf(fileName) is not { } path) return Results.NotFound();

        http.Response.Headers.CacheControl = CacheControl;
        return Results.File(path, PhotoFile.ContentTypeOf(path), enableRangeProcessing: false);
    }
}
