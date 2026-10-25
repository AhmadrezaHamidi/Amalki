using Amlak.Domain.Listing.Enums;

namespace Listing.Application.Commands;

/// <summary>مشخصات مشترک ساخت و ویرایش آگهی — یک مجموعه قاعدهٔ اعتبارسنجی برای هر دو</summary>
public interface IPropertyDetails
{
    string       Title       { get; }
    string       Description { get; }
    DealType     Deal        { get; }
    PropertyKind Kind        { get; }
    long?        Price       { get; }
    long?        Deposit     { get; }
    long?        MonthlyRent { get; }
    int          AreaM2      { get; }
    int?         Bedrooms    { get; }
    int?         Floor       { get; }
    int?         Parking     { get; }
    int?         YearBuilt   { get; }
    string       City        { get; }
    string       District    { get; }
    string?      Address     { get; }
    double?      Latitude    { get; }
    double?      Longitude   { get; }
}

/// <summary>ثبت آگهی جدید — همیشه پیش‌نویس؛ نشانی صفحه خودکار و یکتا از عنوان ساخته می‌شود</summary>
public record CreatePropertyCommand(
    string       Title,
    string       Description,
    DealType     Deal,
    PropertyKind Kind,
    long?        Price,
    long?        Deposit,
    long?        MonthlyRent,
    int          AreaM2,
    int?         Bedrooms,
    int?         Floor,
    int?         Parking,
    int?         YearBuilt,
    string       City,
    string       District,
    string?      Address,
    double?      Latitude,
    double?      Longitude) : ICommand<CreatePropertyResponse>, IPropertyDetails;

public sealed record CreatePropertyResponse(long Id, string Slug);

/// <summary>ویرایش مشخصات آگهی؛ اگر منتشرشده باشد پست کانال‌ها هم ویرایش می‌شود</summary>
public record UpdatePropertyCommand(
    [property: JsonIgnore] long Id,
    string       Title,
    string       Description,
    DealType     Deal,
    PropertyKind Kind,
    long?        Price,
    long?        Deposit,
    long?        MonthlyRent,
    int          AreaM2,
    int?         Bedrooms,
    int?         Floor,
    int?         Parking,
    int?         YearBuilt,
    string       City,
    string       District,
    string?      Address,
    double?      Latitude,
    double?      Longitude) : ICommand<bool>, IPropertyDetails;

/// <summary>انتشار در سایت و کانال‌ها</summary>
public record PublishPropertyCommand(long Id) : ICommand<bool>;

/// <summary>لغو انتشار — برگشت به پیش‌نویس و حذف پست کانال‌ها</summary>
public record UnpublishPropertyCommand(long Id) : ICommand<bool>;

/// <summary>بستن آگهی (فروخته / اجاره رفت)</summary>
public record ClosePropertyCommand(long Id) : ICommand<bool>;

/// <summary>حذف آگهی همراه با عکس‌ها و پست کانال‌ها</summary>
public record DeletePropertyCommand(long Id) : ICommand<bool>;

/// <summary>افزودن عکس — محتوا پیش از ذخیره از نظر نوع و حجم بررسی می‌شود</summary>
public record AddPropertyPhotoCommand(
    long   PropertyId,
    string FileName,
    [property: JsonIgnore] byte[] Content,
    string? Alt) : ICommand<long>;

public record RemovePropertyPhotoCommand(long PropertyId, long PhotoId) : ICommand<bool>;

/// <summary>چیدن دوبارهٔ عکس‌ها؛ اولین عکس، عکس اصلی است</summary>
public record ReorderPropertyPhotosCommand(
    [property: JsonIgnore] long PropertyId,
    IReadOnlyList<long> PhotoIds) : ICommand<bool>;

public record SetCoverPhotoCommand(long PropertyId, long PhotoId) : ICommand<bool>;

public record ChangePhotoAltCommand(
    [property: JsonIgnore] long PropertyId,
    [property: JsonIgnore] long PhotoId,
    string? Alt) : ICommand<bool>;
