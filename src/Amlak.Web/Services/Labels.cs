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
