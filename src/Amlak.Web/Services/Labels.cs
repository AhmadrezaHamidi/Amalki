namespace Amlak.Web.Services;

/// <summary>Persian display names for domain enums, shared by pages and captions.</summary>
public static class Labels
{
    public static string Kind(PropertyKind k) => k switch
    {
        PropertyKind.Apartment => "آپارتمان",
        PropertyKind.Villa => "ویلا",
        PropertyKind.Land => "زمین",
        PropertyKind.Commercial => "تجاری",
        _ => "اداری",
    };

    public static string Deal(DealType d) => d == DealType.Sale ? "فروش" : "اجاره";

    public static string Status(ListingStatus s) => s switch
    {
        ListingStatus.Published => "منتشرشده",
        ListingStatus.Closed => "بسته‌شده",
        _ => "پیش‌نویس",
    };
}
