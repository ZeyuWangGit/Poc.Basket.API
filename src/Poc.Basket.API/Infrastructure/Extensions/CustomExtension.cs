using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Poc.Basket.API.Infrastructure.Models;
using StackExchange.Redis;

namespace Poc.Basket.API.Infrastructure.Extensions;

public static class CustomExtension
{
    public static IServiceCollection AddCustomHealthCheck(this IServiceCollection services,
        IConfiguration configuration)
    {
        var hcBuilder = services.AddHealthChecks();
        hcBuilder.AddCheck("self", () => HealthCheckResult.Healthy());
        hcBuilder.AddRedis(configuration["RedisConnectionString"], name: "redis-healthcheck", tags: new[] {"redis"});
        if (configuration.GetValue<bool>("AzureServiceBusEnabled"))
        {
            hcBuilder.AddAzureServiceBusTopic(
                configuration["ServiceBusConnectionString"],
                topicName: "eshop_event_bus",
                name: "basket-servicebus-check",
                tags: new[] {"servicebus"});
        }
        else
        {
            hcBuilder.AddRabbitMQ(
                $"amqp://{configuration["RabbitMQConnectionString"]}",
                name: "basket-rabbitmq-check",
                tags: new[] {"rabbitmq"}
            );
        }

        return services;
    }

    //By connecting here we are making sure that our service
    //cannot start until redis is ready. This might slow down startup,
    //but given that there is a delay on resolving the ip address
    //and then creating the connection it seems reasonable to move
    //that cost to startup instead of having the first request pay the
    //penalty.
    public static IServiceCollection AddRedisConnection(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BasketSettings>(configuration);

        services.AddSingleton<ConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BasketSettings>>().Value;
            var configuration = ConfigurationOptions.Parse(settings.RedisConnectionString, true);
            return ConnectionMultiplexer.Connect(configuration);
        });

        return services;
    }

    public static IServiceCollection AddEventBusConnection(this IServiceCollection service,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("AzureServiceBusEnabled"))
        {
            
        }
    }
}