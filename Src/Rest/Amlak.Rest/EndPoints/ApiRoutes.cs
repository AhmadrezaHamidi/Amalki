namespace Amlak.Rest.EndPoints;

public static class ApiRoutes
{
    public const string Prefix = "api/v{version:apiVersion}";

    /// <summary>پیشوند مسیرهای پنل مدیریت</summary>
    public const string Admin = Prefix + "/Admin";
}
