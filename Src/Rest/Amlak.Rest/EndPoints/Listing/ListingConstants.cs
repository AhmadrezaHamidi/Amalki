namespace Amlak.Rest.EndPoints.Listing;

public static class ListingConstants
{
    public static class Routes
    {
        // سایت عمومی
        public const string PublicBase = ApiRoutes.Prefix + "/Properties";
        public const string Search     = "";
        public const string BySlug     = "/Slug/{slug}";
        public const string Similar    = "/{id:long}/Similar";
        public const string Locations  = "/Locations";
        public const string Sitemap    = "/Sitemap";

        // پنل مدیریت
        public const string AdminBase    = ApiRoutes.Admin + "/Properties";
        public const string AdminList    = "";
        public const string AdminGet     = "/{id:long}";
        public const string Create       = "";
        public const string Update       = "/{id:long}";
        public const string Publish      = "/{id:long}/Publish";
        public const string Unpublish    = "/{id:long}/Unpublish";
        public const string Close        = "/{id:long}/Close";
        public const string Delete       = "/{id:long}";
        public const string AddPhoto     = "/{id:long}/Photos";
        public const string RemovePhoto  = "/{id:long}/Photos/{photoId:long}";
        public const string ReorderPhoto = "/{id:long}/Photos/Order";
        public const string CoverPhoto   = "/{id:long}/Photos/{photoId:long}/Cover";
        public const string PhotoAlt     = "/{id:long}/Photos/{photoId:long}/Alt";

        // فایل عکس
        public const string PhotoFile    = ApiRoutes.Prefix + "/Photos/{fileName}";
    }

    public static class Names
    {
        private const string P = "Property_";
        public const string Search       = P + "Search";
        public const string BySlug       = P + "BySlug";
        public const string Similar      = P + "Similar";
        public const string Locations    = P + "Locations";
        public const string Sitemap      = P + "Sitemap";
        public const string AdminList    = P + "AdminList";
        public const string AdminGet     = P + "AdminGet";
        public const string Create       = P + "Create";
        public const string Update       = P + "Update";
        public const string Publish      = P + "Publish";
        public const string Unpublish    = P + "Unpublish";
        public const string Close        = P + "Close";
        public const string Delete       = P + "Delete";
        public const string AddPhoto     = P + "AddPhoto";
        public const string RemovePhoto  = P + "RemovePhoto";
        public const string ReorderPhoto = P + "ReorderPhotos";
        public const string CoverPhoto   = P + "SetCoverPhoto";
        public const string PhotoAlt     = P + "ChangePhotoAlt";
        public const string PhotoFile    = "Photo_File";
    }

    public const string Tag      = "Properties";
    public const string AdminTag = "Admin · Properties";

    public static class Docs
    {
        public static class Search       { public const string Summary = "جست‌وجوی آگهی‌ها";     public const string Description = "آگهی‌های منتشرشده با فیلتر معامله، نوع، شهر، محله، قیمت، متراژ و خواب؛ صفحه‌بندی‌شده"; }
        public static class BySlug       { public const string Summary = "جزئیات آگهی";          public const string Description = "آگهی منتشرشده با نشانی صفحه؛ پیش‌نویس، بسته‌شده یا ناموجود ۴۰۴ می‌دهد"; }
        public static class Similar      { public const string Summary = "آگهی‌های مشابه";       public const string Description = "آگهی‌های منتشرشدهٔ هم‌شهر و هم‌نوع معامله"; }
        public static class Locations    { public const string Summary = "شهرها و محله‌ها";      public const string Description = "شهر و محله‌های دارای آگهی منتشرشده با تعداد"; }
        public static class Sitemap      { public const string Summary = "داده نقشه سایت";       public const string Description = "نشانی، عنوان، آخرین تغییر و عکس اصلی آگهی‌های منتشرشده"; }
        public static class AdminList    { public const string Summary = "لیست مدیریت آگهی‌ها";  public const string Description = "همهٔ آگهی‌ها با وضعیت، تعداد عکس، درخواست تماس جدید و خطای کانال"; }
        public static class AdminGet     { public const string Summary = "جزئیات آگهی (مدیر)";   public const string Description = "آگهی در هر وضعیتی"; }
        public static class Create       { public const string Summary = "ثبت آگهی";             public const string Description = "ثبت آگهی پیش‌نویس؛ نشانی صفحه خودکار و یکتا ساخته می‌شود"; }
        public static class Update       { public const string Summary = "ویرایش آگهی";          public const string Description = "ویرایش مشخصات؛ پست کانال‌های آگهی منتشرشده هم ویرایش می‌شود"; }
        public static class Publish      { public const string Summary = "انتشار";               public const string Description = "انتشار در سایت و ارسال به تلگرام و بله"; }
        public static class Unpublish    { public const string Summary = "لغو انتشار";           public const string Description = "برگشت به پیش‌نویس و حذف پیام از کانال‌ها"; }
        public static class Close        { public const string Summary = "بستن آگهی";            public const string Description = "فروخته/اجاره رفت؛ از سایت و کانال‌ها برداشته می‌شود"; }
        public static class Delete       { public const string Summary = "حذف آگهی";             public const string Description = "حذف آگهی، عکس‌ها و پیام کانال‌ها"; }
        public static class AddPhoto     { public const string Summary = "افزودن عکس";           public const string Description = "آپلود JPG/PNG/WebP تا ۵ مگابایت (multipart: file, alt)"; }
        public static class RemovePhoto  { public const string Summary = "حذف عکس";              public const string Description = "حذف عکس و فایل آن"; }
        public static class ReorderPhoto { public const string Summary = "ترتیب عکس‌ها";         public const string Description = "فهرست کامل شناسهٔ عکس‌ها به ترتیب دلخواه؛ اولی عکس اصلی است"; }
        public static class CoverPhoto   { public const string Summary = "انتخاب عکس اصلی";      public const string Description = "عکس به ابتدای فهرست می‌رود"; }
        public static class PhotoAlt     { public const string Summary = "متن جایگزین عکس";      public const string Description = "alt برای سئو و دسترس‌پذیری؛ خالی یعنی عنوان آگهی"; }
        public static class PhotoFile    { public const string Summary = "فایل عکس";             public const string Description = "دریافت فایل عکس آگهی"; }
    }
}
