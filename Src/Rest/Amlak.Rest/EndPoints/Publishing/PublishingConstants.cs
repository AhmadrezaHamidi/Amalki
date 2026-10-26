namespace Amlak.Rest.EndPoints.Publishing;

public static class PublishingConstants
{
    public static class Routes
    {
        public const string BaseRoute = ApiRoutes.Admin + "/Channels";
        public const string ByProperty = "/Properties/{id:long}";
        public const string Sync       = "/Properties/{id:long}/Sync";
        public const string Failed     = "/Failed";
    }

    public static class Names
    {
        private const string P = "Channel_";
        public const string ByProperty = P + "ByProperty";
        public const string Sync       = P + "Sync";
        public const string Failed     = P + "Failed";
    }

    public const string Tag = "Admin · Channels";

    public static class Docs
    {
        public static class ByProperty { public const string Summary = "پست‌های کانال آگهی";  public const string Description = "وضعیت پیام آگهی در تلگرام و بله، با آخرین خطا"; }
        public static class Sync       { public const string Summary = "همگام‌سازی دوباره";   public const string Description = "تلاش دوباره برای ارسال/ویرایش/حذف پیام آگهی در کانال‌ها"; }
        public static class Failed     { public const string Summary = "پست‌های ناموفق";      public const string Description = "پست‌هایی که آخرین همگام‌سازی‌شان خطا داشته است"; }
    }
}
