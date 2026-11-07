using System.Net;
using Amlak.Domain;
using Amlak.Web.Services.Publishing;
using Microsoft.Extensions.Configuration;

namespace Amlak.Tests;

public class BotDiagnosticsTests
{
    private const string Token = "123:SECRET-TOKEN";

    private static BotDiagnostics New(FakeBotHandler bot, Dictionary<string, string?>? extra = null)
    {
        var cfg = new Dictionary<string, string?> { ["Telegram:BotToken"] = Token };
        foreach (var (k, v) in extra ?? []) cfg[k] = v;
        return new BotDiagnostics(new Factory(bot), new ConfigurationBuilder().AddInMemoryCollection(cfg).Build());
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json) };

    [Fact]
    public async Task Check_reports_username_and_distinct_chats()
    {
        var bot = new FakeBotHandler
        {
            Override = m => m switch
            {
                "getMe" => Json("""{"ok":true,"result":{"id":123,"is_bot":true,"username":"amlak_test_bot"}}"""),
                "getUpdates" => Json("""
                    {"ok":true,"result":[
                      {"update_id":1,"message":{"message_id":5,"chat":{"id":555,"type":"private","first_name":"Mahsa","username":"mahsa"},"text":"/start"}},
                      {"update_id":2,"message":{"message_id":6,"chat":{"id":555,"type":"private","first_name":"Mahsa"},"text":"hi"}},
                      {"update_id":3,"channel_post":{"message_id":7,"chat":{"id":-1001234,"type":"channel","title":"Royal"}}}
                    ]}
                    """),
                _ => null,
            },
        };

        var r = await New(bot).CheckAsync(Messenger.Telegram);

        Assert.True(r.Ok);
        Assert.Equal("amlak_test_bot", r.Username);
        Assert.Equal(["555", "-1001234"], r.Chats.Select(c => c.Id));
        Assert.Equal("Mahsa (@mahsa)", r.Chats[0].Title);
        Assert.Equal("channel", r.Chats[1].Type);
        Assert.Contains(bot.Calls, c => c.Url == $"https://api.telegram.org/bot{Token}/getMe");
    }

    [Fact]
    public async Task Bad_token_error_does_not_leak_the_token()
    {
        var bot = new FakeBotHandler { Override = _ => Json($$"""{"ok":false,"description":"Unauthorized {{Token}}"}""", HttpStatusCode.Unauthorized) };
        var r = await New(bot).CheckAsync(Messenger.Telegram);
        Assert.False(r.Ok);
        Assert.DoesNotContain("SECRET", r.Error);
    }

    [Fact]
    public async Task Webhook_conflict_still_counts_as_connected()
    {
        var bot = new FakeBotHandler
        {
            Override = m => m == "getMe"
                ? Json("""{"ok":true,"result":{"username":"b"}}""")
                : Json("""{"ok":false,"description":"Conflict: can't use getUpdates method while webhook is active"}""", HttpStatusCode.Conflict),
        };
        var r = await New(bot).CheckAsync(Messenger.Telegram);
        Assert.True(r.Ok);
        Assert.Empty(r.Chats);
        Assert.Contains("webhook", r.Error);
    }

    [Fact]
    public async Task Test_message_goes_to_admin_chat()
    {
        var bot = new FakeBotHandler();
        var r = await New(bot, new() { ["Telegram:AdminChatId"] = "555" }).SendTestAsync(Messenger.Telegram, admin: true, "سلام");
        Assert.True(r.Ok);
        var call = Assert.Single(bot.Calls);
        Assert.Equal("sendMessage", call.Method);
        Assert.Contains("555", call.Body);
    }

    [Fact]
    public async Task Missing_settings_fail_without_calling_the_api()
    {
        var bot = new FakeBotHandler();
        Assert.False((await New(bot).SendTestAsync(Messenger.Telegram, admin: false, "x")).Ok);
        Assert.False((await New(bot).CheckAsync(Messenger.Bale)).Ok);
        Assert.Empty(bot.Calls);
    }

    [Fact]
    public void Settings_mask_the_token()
    {
        var s = New(new FakeBotHandler()).All().First(x => x.Messenger == Messenger.Telegram);
        Assert.True(s.HasToken);
        Assert.Equal("123:•••", s.MaskedToken);
    }
}
