using Amlak.Rest.EndPoints.Identity;

namespace Amlak.Rest.EndPoints;

/// <summary>Endpoints احراز هویت با کد یکبار مصرف</summary>
public class AuthEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(IdentityConstants.Routes.BaseRoute)
            .WithApiVersionSet()
            .WithTags(IdentityConstants.Tag);

        group.MapPostEndpoint<RequestOtpResponse>(IdentityConstants.Routes.SendOtp,   PostSendOtp,   IdentityConstants.Names.SendOtp,   IdentityConstants.Docs.SendOtp.Summary,   IdentityConstants.Docs.SendOtp.Description).AllowAnonymous();
        group.MapPostEndpoint<LoginResponse>(     IdentityConstants.Routes.VerifyOtp, PostVerifyOtp, IdentityConstants.Names.VerifyOtp, IdentityConstants.Docs.VerifyOtp.Summary, IdentityConstants.Docs.VerifyOtp.Description).AllowAnonymous();
        group.MapPostEndpoint<LoginResponse>(     IdentityConstants.Routes.Refresh,   PostRefresh,   IdentityConstants.Names.Refresh,   IdentityConstants.Docs.Refresh.Summary,   IdentityConstants.Docs.Refresh.Description).AllowAnonymous();
        group.MapPostEndpoint<bool>(              IdentityConstants.Routes.Logout,    PostLogout,    IdentityConstants.Names.Logout,    IdentityConstants.Docs.Logout.Summary,    IdentityConstants.Docs.Logout.Description).RequireAuthorization();
        group.MapPutEndpoint(                     IdentityConstants.Routes.UpdateProfile, PutProfile, IdentityConstants.Names.UpdateProfile, IdentityConstants.Docs.UpdateProfile.Summary, IdentityConstants.Docs.UpdateProfile.Description).RequireAuthorization();
    }

    private static async Task<RequestOtpResponse> PostSendOtp([FromBody] RequestOtpCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<RequestOtpResponse>(cmd, ct);

    private static async Task<LoginResponse> PostVerifyOtp([FromBody] VerifyOtpCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<LoginResponse>(cmd, ct);

    private static async Task<LoginResponse> PostRefresh([FromBody] RefreshTokenCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<LoginResponse>(cmd, ct);

    private static async Task<bool> PostLogout([FromBody] LogoutCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(cmd, ct);

    private static async Task<bool> PutProfile([FromBody] UpdateProfileCommand cmd, IUserContext user, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(cmd with { UserId = user.UserId }, ct);
}
