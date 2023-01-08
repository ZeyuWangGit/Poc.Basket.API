using Poc.EventBus.Core.Events;

namespace Poc.EventBus.Core.Interfaces;

public interface IIntegrationEventHandler<in TIntegrationEvent> : IIntegrationEventHandler where TIntegrationEvent : IntegrationEvent
{
    Task Handle(TIntegrationEvent integrationEvent);
}

public interface IIntegrationEventHandler {}