using Amlak.Domain.Events.Publishing;
using Amlak.Domain.Publishing.Aggregates;
using Amlak.Domain.Publishing.Enums;

namespace Amlak.Domain.Tests.Publishing;

public class ChannelPostTests
{
    [Fact]
    public void Failure_is_recorded_truncated_and_cleared_by_success()
    {
        var post = ChannelPost.Create(1, 5, Channel.Telegram);
        post.Failed(new string('x', 1000));

        post.Error!.Length.Should().Be(ChannelPost.MaxErrorLength);
        post.DomainEvents.Should().ContainSingle(e => e is ChannelSyncFailedEvent);

        post.MarkSent("42", hasPhoto: true);
        post.Error.Should().BeNull();
        post.IsSent.Should().BeTrue();
        post.HasPhoto.Should().BeTrue();
    }
}
