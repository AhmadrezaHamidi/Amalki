using System.Text;
using Amlak.Application.Abstractions;
using Amlak.Infrastructure;
using Amlak.Infrastructure.Storage;
using Amlak.Web.Pages.Account;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Amlak.Tests;

/// <summary>The file-storage adapters behind <see cref="IFileStore"/>, and how configuration picks them.</summary>
public class FileStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("amlak-store").FullName;
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static MemoryStream Bytes(string s) => new(Encoding.UTF8.GetBytes(s));

    private static async Task<string> Read(Stream s)
    {
        using var reader = new StreamReader(s);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Local_store_puts_opens_and_deletes()
    {
        var store = new LocalFileStore(_root);
        var url = await store.PutAsync("a.txt", Bytes("hello"), 5, "text/plain");
        Assert.Equal("/uploads/a.txt", url);
        Assert.True(store.Owns(url));
        Assert.Equal("hello", await Read((await store.OpenAsync(url))!));
        await store.DeleteAsync(url);
        Assert.Null(await store.OpenAsync(url));
    }

    [Fact]
    public async Task Local_store_cannot_be_walked_out_of()
    {
        File.WriteAllText(Path.Combine(_root, "secret.txt"), "x");
        var store = new LocalFileStore(_root);
        Assert.Null(await store.OpenAsync("/uploads/../secret.txt"));
        Assert.False(store.Owns("https://evil.example/uploads/a.txt"));
    }

    [Fact]
    public void Minio_needs_its_settings_and_fails_at_startup_without_them()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Provider"] = "Minio" }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(cfg, _root));
    }

    [Fact]
    public void Minio_settings_select_the_minio_store()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "minio",
            ["Storage:Minio:Endpoint"] = "localhost:9000",
            ["Storage:Minio:AccessKey"] = "k",
            ["Storage:Minio:SecretKey"] = "s",
            ["Storage:Minio:UseSsl"] = "false",
            ["Storage:Minio:PublicBaseUrl"] = "https://cdn.example.ir/amlak/",
        }).Build();
        using var sp = new ServiceCollection().AddLogging().AddInfrastructure(cfg, _root).BuildServiceProvider();
        var store = Assert.IsType<MinioFileStore>(sp.GetRequiredService<IFileStore>());
        Assert.True(store.Owns("https://cdn.example.ir/amlak/uploads/x.webp"));
        Assert.False(store.Owns("/uploads/x.webp"));
        Assert.NotNull(sp.GetRequiredService<LocalFileStore>()); // older photos stay readable
    }

    [Fact]
    public void Without_settings_files_stay_on_local_disk_and_sms_goes_to_the_log()
    {
        var cfg = new ConfigurationBuilder().Build();
        using var sp = new ServiceCollection().AddLogging().AddInfrastructure(cfg, _root).BuildServiceProvider();
        Assert.IsType<LocalFileStore>(sp.GetRequiredService<IFileStore>());
        using var scope = sp.CreateScope();
        Assert.IsType<Amlak.Infrastructure.Sms.LogSmsSender>(scope.ServiceProvider.GetRequiredService<ISmsSender>());
    }
}

/// <summary>
/// Round trip against a real MinIO server. Set <c>AMLAK_TEST_MINIO</c> to "endpoint|accessKey|secretKey"
/// (e.g. "localhost:9000|minioadmin|minioadmin"); without it the test is skipped. docker-compose.yml starts one.
/// </summary>
public class MinioIntegrationTests
{
    [SkippableFact]
    public async Task Photos_round_trip_through_a_real_bucket_and_are_publicly_readable()
    {
        var env = Environment.GetEnvironmentVariable("AMLAK_TEST_MINIO");
        Skip.If(string.IsNullOrWhiteSpace(env), "AMLAK_TEST_MINIO is not set");
        var parts = env!.Split('|');
        var bucket = "amlak-test-" + Guid.NewGuid().ToString("N")[..8];
        var store = new MinioFileStore(new MinioOptions
        {
            Endpoint = parts[0], AccessKey = parts[1], SecretKey = parts[2], UseSsl = false, Bucket = bucket,
        });

        var url = await store.PutAsync("p.txt", new MemoryStream("photo"u8.ToArray()), 5, "text/plain");
        Assert.Equal($"http://{parts[0]}/{bucket}/uploads/p.txt", url);

        await using (var s = (await store.OpenAsync(url))!)
            Assert.Equal("photo", await new StreamReader(s).ReadToEndAsync());

        // The bucket policy the store set lets a browser load the photo without credentials.
        using var http = new HttpClient();
        Assert.Equal("photo", await http.GetStringAsync(url));

        await store.DeleteAsync(url);
        Assert.Null(await store.OpenAsync(url));
        Assert.Null(await store.OpenAsync($"http://{parts[0]}/{bucket}/uploads/../../etc/passwd"));
    }
}

public class OtpPageTests
{
    [Theory]
    [InlineData("/ثبت-آگهی", "/ثبت-آگهی")]
    [InlineData("/", "/")]
    [InlineData("//evil.example", null)]
    [InlineData("/\\evil.example", null)]
    [InlineData("https://evil.example", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Login_only_returns_to_pages_of_this_site(string? asked, string? expected) =>
        Assert.Equal(expected, OtpModel.SafeReturn(asked));

    [Fact]
    public void Persian_return_paths_are_percent_encoded_for_the_location_header()
    {
        var encoded = OtpModel.Encoded("/ثبت-آگهی?x=۱");
        Assert.True(encoded.All(char.IsAscii));
        Assert.Equal("/ثبت-آگهی?x=۱", Uri.UnescapeDataString(encoded));
    }
}
