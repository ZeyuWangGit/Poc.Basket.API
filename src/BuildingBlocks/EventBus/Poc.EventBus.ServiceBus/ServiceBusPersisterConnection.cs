namespace Poc.EventBus.ServiceBus;

public class ServiceBusPersisterConnection: IServiceBusPersisterConnection
{
    private readonly string _serviceBusConnectionString;
    private ServiceBusClient _topicClient;
    private ServiceBusAdministrationClient _subscriptionClient;

    private bool _disposed;
    public ServiceBusPersisterConnection(string serviceBusConnectionString)
    {
        _serviceBusConnectionString = serviceBusConnectionString;
        _topicClient = new ServiceBusClient(_serviceBusConnectionString);
        _subscriptionClient = new ServiceBusAdministrationClient(_serviceBusConnectionString);
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _topicClient.DisposeAsync();
    }

    public ServiceBusClient TopicClient
    {
        get
        {
            if (_topicClient.IsClosed)
            {
                _topicClient = new ServiceBusClient(_serviceBusConnectionString);
            }
            return _topicClient;
        }
    }

    public ServiceBusAdministrationClient AdministrationClient => _subscriptionClient;
}