namespace Amlak.Web.Domain;

/// <summary>A business rule was broken. Pages show <see cref="Exception.Message"/> to the user, so it is Persian.</summary>
public sealed class DomainException(string message) : Exception(message);

/// <summary>Something that happened in the domain, raised by an aggregate and handled after it is saved.</summary>
public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}

/// <summary>Base for aggregate roots: collects the domain events raised while the aggregate changes.</summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent e) => _events.Add(e);

    public void ClearDomainEvents() => _events.Clear();

    protected static T Defined<T>(T value, string message) where T : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new DomainException(message);
}
