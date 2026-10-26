using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Amlak.Web.Services.Storage;

public sealed class MinioOptions
{
    /// <summary>host:port of the MinIO (or any S3-compatible) server, without scheme, e.g. "minio.example.ir:9000".</summary>
    public string Endpoint { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "amlak";
    public bool UseSsl { get; set; } = true;
    /// <summary>Folder (object-key prefix) inside the bucket.</summary>
    public string Prefix { get; set; } = "uploads";
    /// <summary>
    /// Public base URL browsers load files from, e.g. "https://cdn.example.ir/amlak". Defaults to
    /// http(s)://Endpoint/Bucket. The bucket (or prefix) must allow anonymous download.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public string BaseUrl => (string.IsNullOrWhiteSpace(PublicBaseUrl) ? $"{(UseSsl ? "https" : "http")}://{Endpoint}/{Bucket}" : PublicBaseUrl).TrimEnd('/');
}

/// <summary>Files in a MinIO bucket. URLs look like {PublicBaseUrl}/{Prefix}/{name}.</summary>
public class MinioFileStore : IFileStore
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _o;
    private bool _bucketChecked;

    public MinioFileStore(MinioOptions options)
    {
        _o = options;
        _client = new MinioClient().WithEndpoint(options.Endpoint).WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(options.UseSsl).Build();
    }

    public bool Owns(string url) => url.StartsWith(_o.BaseUrl + "/", StringComparison.Ordinal);

    public async Task<string> PutAsync(string name, Stream content, long length, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        var key = KeyFor(name);
        await _client.PutObjectAsync(new PutObjectArgs().WithBucket(_o.Bucket).WithObject(key)
            .WithStreamData(content).WithObjectSize(length).WithContentType(contentType), ct);
        return $"{_o.BaseUrl}/{key}";
    }

    public async Task<Stream?> OpenAsync(string url, CancellationToken ct = default)
    {
        if (KeyOf(url) is not { } key) return null;
        var buffer = new MemoryStream();
        try
        {
            await _client.GetObjectAsync(new GetObjectArgs().WithBucket(_o.Bucket).WithObject(key)
                .WithCallbackStream((s, c) => s.CopyToAsync(buffer, c)), ct);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (KeyOf(url) is { } key)
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_o.Bucket).WithObject(key), ct);
    }

    private string KeyFor(string name) => string.IsNullOrWhiteSpace(_o.Prefix) ? name : $"{_o.Prefix.Trim('/')}/{name}";

    // Only keys this store would have written: no "..", no nested folders beyond the prefix.
    private string? KeyOf(string url)
    {
        if (!Owns(url)) return null;
        var name = Path.GetFileName(url[(_o.BaseUrl.Length + 1)..]);
        return name.Length == 0 ? null : KeyFor(name);
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketChecked) return;
        if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_o.Bucket), ct))
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_o.Bucket), ct);
            // A bucket we created only holds site photos: let browsers download them (read-only, this prefix only).
            // An existing bucket's policy is left to its owner (see docs/STORAGE.md).
            var resource = $"arn:aws:s3:::{_o.Bucket}/{(string.IsNullOrWhiteSpace(_o.Prefix) ? "" : _o.Prefix.Trim('/') + "/")}*";
            var policy = $$"""{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["{{resource}}"]}]}""";
            await _client.SetPolicyAsync(new SetPolicyArgs().WithBucket(_o.Bucket).WithPolicy(policy), ct);
        }
        _bucketChecked = true;
    }
}
