namespace Amlak.Rest.EndPoints.Lead;

public static class LeadConstants
{
    public static class Routes
    {
        public const string PublicBase = ApiRoutes.Prefix + "/Leads";
        public const string Submit     = "";

        public const string AdminBase  = ApiRoutes.Admin + "/Leads";
        public const string GetPaged   = "";
        public const string GetById    = "/{id:long}";
        public const string Stats      = "/Stats";
        public const string Status     = "/{id:long}/Status";
    }

    public static class Names
    {
        private const string P = "Lead_";
        public const string Submit   = P + "Submit";
        public const string GetPaged = P + "GetPaged";
        public const string GetById  = P + "GetById";
        public const string Stats    = P + "Stats";
        public const string Status   = P + "ChangeStatus";
    }

    public const string Tag      = "Leads";
    public const string AdminTag = "Admin · Leads";

    public static class Docs
    {
        public static class Submit   { public const string Summary = "ثبت درخواست تماس";    public const string Description = "بازدیدکننده برای یک آگهی (یا به‌طور کلی) شماره می‌گذارد؛ حداکثر ۳ بار در ۱۰ دقیقه از یک شماره"; }
        public static class GetPaged { public const string Summary = "لیست درخواست‌ها";      public const string Description = "درخواست‌های تماس با فیلتر وضعیت و آگهی"; }
        public static class GetById  { public const string Summary = "جزئیات درخواست";      public const string Description = "یک درخواست تماس"; }
        public static class Stats    { public const string Summary = "آمار درخواست‌ها";      public const string Description = "تعداد درخواست‌ها به تفکیک وضعیت"; }
        public static class Status   { public const string Summary = "تغییر وضعیت";         public const string Description = "تماس گرفته شد / بسته شد / هرزنامه، با یادداشت"; }
    }
}
