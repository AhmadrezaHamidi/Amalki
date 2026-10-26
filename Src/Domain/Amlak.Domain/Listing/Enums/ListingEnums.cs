namespace Amlak.Domain.Listing.Enums;

/// <summary>نوع معامله</summary>
public enum DealType
{
    /// <summary>فروش — قیمت کل لازم است</summary>
    Sale = 1,

    /// <summary>رهن و اجاره — رهن یا اجارهٔ ماهانه (یا هر دو) لازم است</summary>
    Rent = 2
}

/// <summary>نوع ملک</summary>
public enum PropertyKind
{
    Apartment = 1,
    Villa = 2,
    Land = 3,
    Commercial = 4,
    Office = 5
}

/// <summary>وضعیت آگهی — فقط «منتشرشده» برای عموم دیده می‌شود</summary>
public enum ListingStatus
{
    Draft = 0,
    Published = 1,

    /// <summary>فروخته یا اجاره رفته</summary>
    Closed = 2
}

/// <summary>ترتیب نمایش نتایج جست‌وجو</summary>
public enum ListingSort
{
    Newest = 1,
    PriceAsc = 2,
    PriceDesc = 3,
    AreaDesc = 4
}
