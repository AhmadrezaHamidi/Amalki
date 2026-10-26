namespace Amlak.Rest.EndPoints.Identity;

public static class IdentityConstants
{
    public static class Routes
    {
        public const string BaseRoute     = ApiRoutes.Prefix + "/Auth";
        public const string SendOtp       = "/SendOtp";
        public const string VerifyOtp     = "/VerifyOtp";
        public const string Refresh       = "/Refresh";
        public const string Logout        = "/Logout";
        public const string UpdateProfile = "/Profile";
    }

    public static class Names
    {
        private const string P = "Auth_";
        public const string SendOtp       = P + "SendOtp";
        public const string VerifyOtp     = P + "VerifyOtp";
        public const string Refresh       = P + "Refresh";
        public const string Logout        = P + "Logout";
        public const string UpdateProfile = P + "UpdateProfile";
    }

    public const string Tag = "Auth";

    public static class Docs
    {
        public static class SendOtp       { public const string Summary = "ارسال کد تأیید";    public const string Description = "ارسال کد یکبار مصرف به شماره موبایل (حداکثر یک بار در دقیقه)"; }
        public static class VerifyOtp     { public const string Summary = "تأیید کد و ورود";   public const string Description = "تأیید کد و دریافت توکن؛ کاربر جدید خودکار ساخته می‌شود و نقش مدیر از تنظیمات می‌آید"; }
        public static class Refresh       { public const string Summary = "تمدید توکن";        public const string Description = "دریافت توکن دسترسی جدید با توکن تمدید"; }
        public static class Logout        { public const string Summary = "خروج";              public const string Description = "ابطال توکن تمدید"; }
        public static class UpdateProfile { public const string Summary = "بروزرسانی پروفایل"; public const string Description = "تکمیل نام کاربر جاری"; }
    }
}
