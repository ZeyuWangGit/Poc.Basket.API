namespace Poc.EventBus.Core.Interfaces;

public interface IDynamicIntegrationEventHandler
{
    Task Handle(dynamic eventData);
}