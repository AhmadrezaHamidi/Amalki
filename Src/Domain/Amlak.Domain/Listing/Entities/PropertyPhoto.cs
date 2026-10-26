namespace Amlak.Domain.Listing.Entities;

/// <summary>عکس آگهی — ترتیب صفر عکس اصلی (کاور) است</summary>
public sealed class PropertyPhoto
{
    public long   Id         { get; private set; }
    public long   PropertyId { get; private set; }

    /// <summary>نام فایل ذخیره‌شده (تصادفی) — آدرس عمومی در سمت خواندن ساخته می‌شود</summary>
    public string FileName   { get; private set; } = string.Empty;

    public string Alt        { get; private set; } = string.Empty;
    public int    SortOrder  { get; private set; }

    private PropertyPhoto() { }

    internal PropertyPhoto(long id, long propertyId, string fileName, string alt, int sortOrder)
    {
        Id         = id;
        PropertyId = propertyId;
        FileName   = fileName;
        Alt        = alt;
        SortOrder  = sortOrder;
    }

    internal void MoveTo(int sortOrder) => SortOrder = sortOrder;

    internal void ChangeAlt(string alt) => Alt = alt;
}
