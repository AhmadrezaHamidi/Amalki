using Microsoft.AspNetCore.Authorization;

namespace Amlak.Rest.EndPoints;

/// <summary>
/// نقش‌های سامانه — مقادیر دقیقاً برابر نام‌های <c>UserRole</c> در دامین است،
/// چون JwtService نقش را با <c>role.ToString()</c> داخل توکن می‌گذارد.
/// </summary>
public static class AmlakRoles
{
    public const string Admin    = "Admin";
    public const string Customer = "Customer";
}

public static class EndpointRoleExtensions
{
    /// <summary>محدود کردن endpoint به نقش‌های مشخص</summary>
    public static TBuilder RequireRoles<TBuilder>(this TBuilder builder, string roles)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(new AuthorizeAttribute { Roles = roles });
}

public static class ResultExtensions
{
    /// <summary>نتیجهٔ null یعنی ۴۰۴ — صفحهٔ آگهی پیش‌نویس یا ناموجود نباید ۲۰۰ خالی بدهد</summary>
    public static IResult OkOrNotFound<T>(this T? value) => value is null ? Results.NotFound() : Results.Ok(value);
}
