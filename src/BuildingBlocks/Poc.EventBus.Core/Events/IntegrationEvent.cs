using System.Text.Json.Serialization;

namespace Poc.EventBus.Core.Events;

public record IntegrationEvent
{
    public IntegrationEvent()
    {
        Id = Guid.NewGuid();
        CreateDateTime = DateTime.UtcNow;
    }

    [JsonConstructor]
    public IntegrationEvent(Guid id, DateTime createDateTime)
    {
        Id = id;
        CreateDateTime = createDateTime;
    }

    [JsonInclude]
    public Guid Id { get; private init; }
    [JsonInclude]
    public DateTime CreateDateTime { get; private init; }
}