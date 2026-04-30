using MassTransit;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderConfirmedConsumerDefinition : ConsumerDefinition<OrderConfirmedConsumer>
{
    public OrderConfirmedConsumerDefinition()
    {
        EndpointName = "order-confirmed-notifications";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderConfirmedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(endpointConfigurator);

        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15)));
    }
}
