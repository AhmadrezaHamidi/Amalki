using Amlak.Domain.Events.Lead;
using Amlak.Domain.Lead.Aggregates;
using Amlak.Domain.Lead.Args;
using Amlak.Domain.Lead.Enums;
using Amlak.Domain.Lead.Exceptions;

namespace Amlak.Domain.Tests.Lead;

public class ContactLeadTests
{
    private static ContactLead NewLead() => ContactLead.Submit(new SubmitLeadArg(1, 7, " علی رضایی ", "09121234567", "  "));

    [Fact]
    public void Submit_trims_input_and_raises_event()
    {
        var lead = NewLead();

        lead.Status.Should().Be(LeadStatus.New);
        lead.FullName.Should().Be("علی رضایی");
        lead.Message.Should().BeNull();
        lead.DomainEvents.Should().ContainSingle(e => e is LeadSubmittedEvent);
    }

    [Fact]
    public void New_to_contacted_to_closed_is_allowed()
    {
        var lead = NewLead();
        lead.ChangeStatus(LeadStatus.Contacted, "فردا بازدید");
        lead.ChangeStatus(LeadStatus.Closed, null);

        lead.Status.Should().Be(LeadStatus.Closed);
        lead.AdminNote.Should().Be("فردا بازدید");
        lead.HandledAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(LeadStatus.New)]
    [InlineData(LeadStatus.Contacted)]
    public void Closed_is_terminal(LeadStatus target)
    {
        var lead = NewLead();
        lead.ChangeStatus(LeadStatus.Closed, null);

        FluentActions.Invoking(() => lead.ChangeStatus(target, null)).Should().Throw<InvalidLeadTransitionException>();
    }

    [Fact]
    public void Cannot_go_back_to_new()
    {
        var lead = NewLead();
        FluentActions.Invoking(() => lead.ChangeStatus(LeadStatus.New, null)).Should().Throw<InvalidLeadTransitionException>();
    }
}
