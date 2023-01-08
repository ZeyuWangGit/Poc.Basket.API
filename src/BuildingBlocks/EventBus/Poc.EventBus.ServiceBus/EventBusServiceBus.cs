using System.Text;
using System.Text.Json;
using Autofac;
using Microsoft.Extensions.Logging;
using Poc.EventBus.Core;
using Poc.EventBus.Core.Events;
using Poc.EventBus.Core.Interfaces;

namespace Poc.EventBus.ServiceBus;

public class EventBusServiceBus: IEventBus, IAsyncDisposable
{
    private readonly IServiceBusPersisterConnection _serviceBusPersisterConnection;
    private readonly ILogger<EventBusServiceBus> _logger;
    private readonly IEventBusSubscriptionsManager _subscriptionsManager;
    private readonly ILifetimeScope _autofacScope;

    private readonly ServiceBusSender _sender;
    private readonly ServiceBusProcessor _processor;

    private readonly string _topicName = "eshop_event_bus";
    private readonly string _subscriptionName;
    private readonly string AUTOFAC_SCOPE_NAME = "eshop_event_bus";
    private const string INTEGRATION_EVENT_SUFFIX = "IntegrationEvent";

    public EventBusServiceBus(IServiceBusPersisterConnection serviceBusPersisterConnection, ILogger<EventBusServiceBus> logger,
        IEventBusSubscriptionsManager subscriptionsManager, ILifetimeScope autofacScope, string subscriptionName)
    {
        _serviceBusPersisterConnection = serviceBusPersisterConnection;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subscriptionsManager = subscriptionsManager ?? new InMemoryEventBusSubscriptionsManager();
        _autofacScope = autofacScope;
        _subscriptionName = subscriptionName;
        _sender = _serviceBusPersisterConnection.TopicClient.CreateSender(_topicName);
        _processor = _serviceBusPersisterConnection.TopicClient.CreateProcessor(_topicName,
            _subscriptionName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 10,
                AutoCompleteMessages = false
            });

        RemoveDefaultRule();
        RegisterSubscriptionClientMessageHandlerAsync().GetAwaiter().GetResult();
    }
    public async Task Publish(IntegrationEvent @event)
    {
        var eventName = @event.GetType().Name.Replace(INTEGRATION_EVENT_SUFFIX, "");
        var jsonMessage = JsonSerializer.Serialize(@event, @event.GetType());
        var body = Encoding.UTF8.GetBytes(jsonMessage);

        var message = new ServiceBusMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Body = new BinaryData(body),
            Subject = eventName
        };
        await _sender.SendMessageAsync(message);
    }

    public void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        var eventName = typeof(T).Name.Replace(INTEGRATION_EVENT_SUFFIX, "");

        var containsKey = _subscriptionsManager.HasSubscriptionsForEvent<T>();
        if (!containsKey)
        {
            try
            {
                _serviceBusPersisterConnection.AdministrationClient.CreateRuleAsync(_topicName, _subscriptionName, new CreateRuleOptions
                {
                    Filter = new CorrelationRuleFilter() { Subject = eventName },
                    Name = eventName
                }).GetAwaiter().GetResult();
            }
            catch (ServiceBusException)
            {
                _logger.LogWarning("The messaging entity {eventName} already exists.", eventName);
            }
        }

        _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

        _subscriptionsManager.AddSubscription<T, TH>();
    }

    public void Unsubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        var eventName = typeof(T).Name.Replace(INTEGRATION_EVENT_SUFFIX, "");

        try
        {
            _serviceBusPersisterConnection
                .AdministrationClient
                .DeleteRuleAsync(_topicName, _subscriptionName, eventName)
                .GetAwaiter()
                .GetResult();
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            _logger.LogWarning("The messaging entity {eventName} Could not be found.", eventName);
        }

        _logger.LogInformation("Unsubscribing from event {EventName}", eventName);

        _subscriptionsManager.RemoveSubscription<T, TH>();
    }

    public void SubscribeDynamic<TH>(string eventName) where TH : IDynamicIntegrationEventHandler
    {
        _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

        _subscriptionsManager.AddDynamicSubscription<TH>(eventName);
    }

    public void UnsubscribeDynamic<TH>(string eventName) where TH : IDynamicIntegrationEventHandler
    {
        _logger.LogInformation("Unsubscribing from dynamic event {EventName}", eventName);

        _subscriptionsManager.RemoveDynamicSubscription<TH>(eventName);
    }

    public async ValueTask DisposeAsync()
    {
        _subscriptionsManager.Clear();
        await _processor.CloseAsync();
    }

    private void RemoveDefaultRule()
    {
        try
        {
            _serviceBusPersisterConnection
                .AdministrationClient
                .DeleteRuleAsync(_topicName, _subscriptionName, RuleProperties.DefaultRuleName)
                .GetAwaiter()
                .GetResult();
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            _logger.LogWarning("The messaging entity {DefaultRuleName} Could not be found.", RuleProperties.DefaultRuleName);
        }
    }
    private async Task RegisterSubscriptionClientMessageHandlerAsync()
    {
        _processor.ProcessMessageAsync +=
            async (args) =>
            {
                var eventName = $"{args.Message.Subject}{INTEGRATION_EVENT_SUFFIX}";
                string messageData = args.Message.Body.ToString();
                if (await ProcessEvent(eventName, messageData))
                {
                    await args.CompleteMessageAsync(args.Message);
                }
            };
        _processor.ProcessErrorAsync += ErrorHandler;
        await _processor.StartProcessingAsync();
    }
    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        var ex = args.Exception;
        var context = args.ErrorSource;

        _logger.LogError(ex, "ERROR handling message: {ExceptionMessage} - Context: {@ExceptionContext}", ex.Message, context);

        return Task.CompletedTask;
    }
    private async Task<bool> ProcessEvent(string eventName, string message)
    {
        var processed = false;
        if (_subscriptionsManager.HasSubscriptionsForEvent(eventName))
        {
            var scope = _autofacScope.BeginLifetimeScope(AUTOFAC_SCOPE_NAME);
            var subscriptions = _subscriptionsManager.GetHandlersForEvent(eventName);
            foreach (var subscription in subscriptions)
            {
                if (subscription.IsDynamic)
                {
                    if (scope.ResolveOptional(subscription.HandlerType) is not IDynamicIntegrationEventHandler handler) { continue; }
                    using dynamic eventDate = JsonDocument.Parse(message);
                    await handler.Handle(eventDate);
                }
                else
                {
                    var handler = scope.ResolveOptional(subscription.HandlerType);
                    if (handler == null) { continue; }

                    var eventType = _subscriptionsManager.GetEventTypeByName(eventName);
                    var integrationEvent = JsonSerializer.Deserialize(message, eventType);
                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                    await (Task) concreteType.GetMethod("handler").Invoke(handler, new[] {integrationEvent});
                }
            }
        }
        processed = true;
        return processed;
    }
}