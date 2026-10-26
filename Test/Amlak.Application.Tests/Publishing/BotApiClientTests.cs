using System.Net;
using Amlak.Application.Publishing;
using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Args;
using Amlak.Domain.Listing.Enums;
using Amlak.Domain.Publishing.Enums;
using Amlak.Persistence.EF.Services;
using Microsoft.Extensions.Configuration;

namespace Amlak.Application.Tests.Publishing;

public sealed class BotApiClientTests
{
    private const string Token = "123:SECRET-TOKEN";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body) };

    [Fact]
    public async Task Message_id_is_read_from_the_result()
    {
        var client = new BotApiClient(new HttpClient(new StubHandler(_ => Json("""{"ok":true,"result":{"message_id":77}}"""))), "https://api.telegram.org", Token, "@chan");

        var r = await client.SendMessageAsync("سلام");
        r.Ok.Should().BeTrue();
        r.MessageId.Should().Be("77");
    }

    [Fact]
    public async Task Error_description_never_contains_the_token()
    {
        var client = new BotApiClient(new HttpClient(new StubHandler(_ =>
            Json($$"""{"ok":false,"description":"Unauthorized for bot{{Token}}"}""", HttpStatusCode.Unauthorized))), "https://x", Token, "1");

        var r = await client.SendMessageAsync("x");
        r.Ok.Should().BeFalse();
        r.Error.Should().NotContain("SECRET").And.Contain("***");
    }

    [Fact]
    public async Task Network_failure_does_not_leak_the_url()
    {
        var client = new BotApiClient(new HttpClient(new StubHandler(_ => throw new HttpRequestException($"failed https://x/bot{Token}/sendMessage"))), "https://x", Token, "1");

        var r = await client.DeleteAsync("5");
        r.Ok.Should().BeFalse();
        r.Error.Should().NotContain("SECRET");
    }

    [Fact]
    public async Task Gateway_enables_only_configured_channels_and_uses_custom_api_base()
    {
        var stub = new StubHandler(_ => Json("""{"ok":true,"result":{"message_id":1}}"""));
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bale:BotToken"] = "999:BALE", ["Bale:ChatId"] = "42",
            ["Telegram:BotToken"] = Token,  // بدون ChatId → غیرفعال
        }).Build();

        var gateway = new BotChannelGateway(cfg, new HttpClient(stub));
        gateway.EnabledChannels.Should().Equal(Channel.Bale);

        await gateway.SendAsync(Channel.Bale, "x", null);
        stub.Urls.Single().Should().Be("https://tapi.bale.ai/bot999:BALE/sendMessage");
    }

    [Fact]
    public void Caption_has_price_link_and_hashtags()
    {
        var p = Property.Create(new CreatePropertyArg(1, "آپارتمان-تست", new PropertyDetailsArg(
            "آپارتمان تست", "d", DealType.Sale, PropertyKind.Apartment, 5_000_000_000, null, null,
            90, 2, null, null, null, "تهران", "شهرک غرب", null, null, null)));

        var text = CaptionBuilder.Build(p, "https://example.ir/");

        text.Should().Contain("۵ میلیارد")
            .And.Contain("https://example.ir/ملک/" + Uri.EscapeDataString("آپارتمان-تست"))
            .And.Contain("#فروش").And.Contain("#شهرک_غرب");
    }
}
