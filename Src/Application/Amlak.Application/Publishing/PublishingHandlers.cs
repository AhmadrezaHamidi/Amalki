using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Publishing.Aggregates;
using Amlak.Domain.Publishing.Enums;
using Microsoft.Extensions.Configuration;

namespace Amlak.Application.Publishing;

/// <summary>
/// همگام نگه داشتن کانال‌های تلگرام و بله با آگهی: انتشار → ارسال، تغییر → ویرایش، لغو/بستن/حذف → حذف پیام.
/// شکست هر کانال کنار همان پست ثبت می‌شود و هرگز عملیات مدیر را برنمی‌گرداند.
/// </summary>
public sealed class PublishingHandlers(
    IPropertyRepository    propertyRepo,
    IChannelPostRepository postRepo,
    IChannelGateway        gateway,
    IPhotoStorage          photoStorage,
    IConfiguration         configuration,
    AmlakDbContext         db) :
    ICommandHandler<SyncPropertyChannelsCommand, bool>,
    ICommandHandler<RemovePropertyChannelsCommand, bool>
{
    private string SiteBaseUrl => configuration["Site:BaseUrl"] ?? "http://localhost";

    public async Task<bool> Handle(SyncPropertyChannelsCommand command, CancellationToken token)
    {
        if (gateway.EnabledChannels.Count == 0) return true;

        // آگهی حذف‌شده (یا ناموجود) مثل آگهی غیرمنتشر رفتار می‌کند: پست‌ها پاک می‌شوند
        var property = await propertyRepo.GetByIdAsync(command.PropertyId, token);
        var posts    = await postRepo.GetByPropertyAsync(command.PropertyId, token);

        foreach (var channel in gateway.EnabledChannels)
        {
            var post = posts.FirstOrDefault(p => p.Channel == channel);

            if (property is { IsPublished: true })
                await PublishOrEditAsync(property, post, channel, token);
            else if (post is not null)
                await RemoveAsync(post, token);
        }

        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task<bool> Handle(RemovePropertyChannelsCommand command, CancellationToken token)
    {
        var posts = await postRepo.GetByPropertyAsync(command.PropertyId, token);
        foreach (var post in posts.Where(p => gateway.EnabledChannels.Contains(p.Channel)))
            await RemoveAsync(post, token);

        await db.SaveChangesAsync(token);
        return true;
    }

    private async Task PublishOrEditAsync(Property property, ChannelPost? post, Channel channel, CancellationToken ct)
    {
        var caption = CaptionBuilder.Build(property, SiteBaseUrl);

        if (post is { IsSent: true })
        {
            var edit = await gateway.EditAsync(channel, post.ExternalMessageId!, caption, post.HasPhoto, ct);
            if (edit.Ok || edit.NotModified) post.Succeeded();
            else post.Failed(edit.Error ?? "ویرایش ناموفق");
            return;
        }

        if (post is null)
        {
            post = ChannelPost.Create(await postRepo.GetNextIdAsync(), property.Id, channel);
            await postRepo.AddAsync(post, ct);
        }

        // اولین عکسی که فایلش واقعاً روی دیسک هست؛ وگرنه پیام متنی
        var photoPath = property.Photos.Select(p => photoStorage.PathOf(p.FileName)).FirstOrDefault(p => p is not null);
        var send      = await gateway.SendAsync(channel, caption, photoPath, ct);

        if (send is { Ok: true, MessageId: not null })
            post.MarkSent(send.MessageId, hasPhoto: photoPath is not null);
        else
            post.Failed(send.Error ?? "شناسهٔ پیام از سرویس برنگشت");
    }

    private async Task RemoveAsync(ChannelPost post, CancellationToken ct)
    {
        if (post.IsSent)
        {
            var delete = await gateway.DeleteAsync(post.Channel, post.ExternalMessageId!, ct);
            if (!delete.Ok && !delete.NotFound)
            {
                post.Failed(delete.Error ?? "حذف ناموفق");
                return;
            }
        }

        await postRepo.RemoveAsync(post, ct);
    }
}
