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

    public static string Deed(DeedType d) => d switch
    {
        DeedType.Single => "تک‌برگ",
        DeedType.Manguleh => "منگوله‌دار",
        DeedType.Promissory => "قول‌نامه‌ای",
        _ => "اوقافی",
    };

    public static string Deal(DealType d) => d switch
    {
        DealType.Sale => "فروش",
        DealType.ShortTerm => "اجارهٔ کوتاه‌مدت",
        _ => "اجاره",
    };

    public static string Status(ListingStatus s) => s switch
    {
        ListingStatus.Published => "منتشرشده",
        ListingStatus.Closed => "بسته‌شده",
        _ => "پیش‌نویس",
    };

    public static string Category(TicketCategory c) => c switch
    {
        TicketCategory.Listing => "دربارهٔ یک آگهی",
        TicketCategory.Technical => "مشکل فنی سایت",
        TicketCategory.Complaint => "شکایت یا پیشنهاد",
        _ => "سؤال عمومی",
    };

    public static string Status(TicketStatus s) => s switch
    {
        TicketStatus.Answered => "پاسخ داده شد",
        TicketStatus.Closed => "بسته",
        _ => "در انتظار پاسخ",
    };
}
