using AhmadBase.Doamin;
using AhmadBase.Persistence.NHiLoHelper;
using Amlak.Domain.Publishing.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Amlak.Application.Tests;

internal sealed class FakeHiLo : IHiLoIdGenerator
{
    private long _next = 1000;
    public long GetNextId<T>() where T : IAggregateRoot => ++_next;
}

internal sealed class FakeSms : AhmadBase.External.Service.ISmsService
{
    public List<(string Phone, string Text)> Sent { get; } = [];

    public Task<bool> SendOtpAsync(string phoneNumber, string code, CancellationToken token = default)
    {
        Sent.Add((phoneNumber, code));
        return Task.FromResult(true);
    }

    public Task<bool> SendBulkAsync(IReadOnlyList<string> phones, string message, CancellationToken token = default)
    {
        foreach (var p in phones) Sent.Add((p, message));
        return Task.FromResult(true);
    }
}

/// <summary>Bot API ساختگی — هر فراخوانی ثبت می‌شود و پاسخ قابل تنظیم است</summary>
internal sealed class FakeGateway(params Channel[] channels) : IChannelGateway
{
    private int _nextId = 100;
    public List<(string Method, Channel Channel, string? Arg)> Calls { get; } = [];
    public Func<string, ChannelResult?>? Override { get; set; }

    public IReadOnlyCollection<Channel> EnabledChannels { get; } = channels;

    public Task<ChannelResult> SendAsync(Channel channel, string caption, string? photoPath, CancellationToken ct = default)
        => Record(photoPath is null ? "sendMessage" : "sendPhoto", channel, photoPath, () => new ChannelResult(true, (_nextId++).ToString(), null));

    public Task<ChannelResult> EditAsync(Channel channel, string messageId, string caption, bool isPhoto, CancellationToken ct = default)
        => Record(isPhoto ? "editMessageCaption" : "editMessageText", channel, messageId, () => new ChannelResult(true, null, null));

    public Task<ChannelResult> DeleteAsync(Channel channel, string messageId, CancellationToken ct = default)
        => Record("deleteMessage", channel, messageId, () => new ChannelResult(true, null, null));

    private Task<ChannelResult> Record(string method, Channel channel, string? arg, Func<ChannelResult> ok)
    {
        Calls.Add((method, channel, arg));
        return Task.FromResult(Override?.Invoke(method) ?? ok());
    }
}

/// <summary>دیتابیس SQLite درون‌حافظه و انبار عکس موقت برای هر تست</summary>
public abstract class DbTestBase : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    protected readonly string PhotoRoot = Path.Combine(Path.GetTempPath(), "amlak-test-" + Guid.NewGuid().ToString("N"));
    internal readonly FakeHiLo HiLo = new();

    protected DbTestBase()
    {
        _conn.Open();
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    protected AmlakDbContext NewDb()
        => new(new DbContextOptionsBuilder<AmlakDbContext>().UseSqlite(_conn).Options);

    protected IConfiguration Config(Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Site:BaseUrl"]          = "https://example.ir",
            ["Storage:PhotoPath"]     = PhotoRoot,
            ["Platform:AdminPhones:0"] = "09120000000",
            ["JWTOptions:Secret"]     = "TEST_SECRET_KEY_THAT_IS_LONG_ENOUGH_123456",
            ["UseFakeServices"]       = "true",
        };
        foreach (var (k, v) in extra ?? []) values[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    public void Dispose()
    {
        _conn.Dispose();
        if (Directory.Exists(PhotoRoot)) Directory.Delete(PhotoRoot, true);
    }
}
