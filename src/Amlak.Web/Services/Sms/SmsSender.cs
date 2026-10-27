namespace Amlak.Web.Services.Sms;

/// <summary>Sends the one-time login code. Chosen by Sms:Provider: "Kavenegar", or "Log" (default, development).</summary>
public interface ISmsSender
{
    Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default);
}

/// <summary>Development sender: writes the code to the log instead of sending an SMS.</summary>
public class LogSmsSender(ILogger<LogSmsSender> log) : ISmsSender
{
    public Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default)
    {
        log.LogWarning("SMS code for {Mobile}: {Code} (Sms:Provider is Log; no SMS was sent)", mobile, code);
        return Task.FromResult(true);
    }
}

/// <summary>
/// Kavenegar "verify lookup" API: a pre-approved template with one %token%, so the message passes the
/// operators' advertising filter. Needs Sms:ApiKey and Sms:Template.
/// </summary>
public class KavenegarSmsSender(HttpClient http, string apiKey, string template, ILogger<KavenegarSmsSender> log) : ISmsSender
{
    public async Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default)
    {
        var url = $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}/verify/lookup.json" +
                  $"?receptor={mobile}&token={code}&template={Uri.EscapeDataString(template)}";
        try
        {
            using var res = await http.PostAsync(url, null, ct);
            if (res.IsSuccessStatusCode) return true;
            log.LogWarning("Kavenegar returned {Status}", (int)res.StatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // Never log the URL: it carries the API key.
            log.LogWarning("Kavenegar request failed: {Error}", e.GetType().Name);
        }
        return false;
    }
}
