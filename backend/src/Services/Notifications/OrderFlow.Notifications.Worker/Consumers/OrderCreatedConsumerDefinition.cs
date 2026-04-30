using MassTransit;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderCreatedConsumerDefinition : ConsumerDefinition<OrderCreatedConsumer>
{
    public OrderCreatedConsumerDefinition()
    {
        EndpointName = "order-created-notifications";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(endpointConfigurator);

        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15)));

        endpointConfigurator.UseCircuitBreaker(cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 5;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        });
    }
}
