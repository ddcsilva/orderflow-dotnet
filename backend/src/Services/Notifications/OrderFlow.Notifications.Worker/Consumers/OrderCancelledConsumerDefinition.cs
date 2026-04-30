using MassTransit;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderCancelledConsumerDefinition : ConsumerDefinition<OrderCancelledConsumer>
{
    public OrderCancelledConsumerDefinition()
    {
        EndpointName = "order-cancelled-notifications";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderCancelledConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(endpointConfigurator);

        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15)));
    }
}
