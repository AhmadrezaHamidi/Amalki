namespace Amlak.Web.Services.Sms;

/// <summary>Sends SMS. Chosen by Sms:Provider: "Kavenegar", or "Log" (default, development).</summary>
public interface ISmsSender
{
    /// <summary>The one-time login code, through a template so it is never held back as advertising.</summary>
    Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default);

    /// <summary>Free text: alerts and campaigns. Callers go through <see cref="SmsGateway"/>, which adds the opt-out rules.</summary>
    Task<bool> SendTextAsync(string mobile, string text, CancellationToken ct = default);
}

/// <summary>Development sender: writes the code to the log instead of sending an SMS.</summary>
public class LogSmsSender(ILogger<LogSmsSender> log) : ISmsSender
{
    public Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default)
    {
        log.LogWarning("SMS code for {Mobile}: {Code} (Sms:Provider is Log; no SMS was sent)", mobile, code);
        return Task.FromResult(true);
    }

    public Task<bool> SendTextAsync(string mobile, string text, CancellationToken ct = default)
    {
        log.LogWarning("SMS to {Mobile}: {Text} (Sms:Provider is Log; no SMS was sent)", mobile, text);
        return Task.FromResult(true);
    }
}

/// <summary>
/// Kavenegar "verify lookup" API: a pre-approved template with one %token%, so the message passes the
/// operators' advertising filter. Needs Sms:ApiKey and Sms:Template. Other texts use "sms/send" from
/// Sms:Sender (the line number), or the account's default line when it is not set.
/// </summary>
public class KavenegarSmsSender(HttpClient http, string apiKey, string template, string? sender, ILogger<KavenegarSmsSender> log) : ISmsSender
{
    private string Base => $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(apiKey)}";

    public Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default) =>
        PostAsync($"{Base}/verify/lookup.json?receptor={mobile}&token={code}&template={Uri.EscapeDataString(template)}", ct);

    public Task<bool> SendTextAsync(string mobile, string text, CancellationToken ct = default) =>
        PostAsync($"{Base}/sms/send.json?receptor={mobile}&message={Uri.EscapeDataString(text)}"
                  + (string.IsNullOrWhiteSpace(sender) ? "" : $"&sender={Uri.EscapeDataString(sender)}"), ct);

    private async Task<bool> PostAsync(string url, CancellationToken ct)
    {
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
