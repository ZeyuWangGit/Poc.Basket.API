using Poc.EventBus.Core.Events;

namespace Poc.EventBus.Core.Interfaces;

public interface IEventBus
{
    Task Publish(IntegrationEvent @event);

    void Subscribe<T, TH>() 
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>;

    void Unsubscribe<T, TH>()
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>;

    void SubscribeDynamic<TH>(string eventName) where TH : IDynamicIntegrationEventHandler;
    void UnsubscribeDynamic<TH>(string eventName) where TH : IDynamicIntegrationEventHandler;
}