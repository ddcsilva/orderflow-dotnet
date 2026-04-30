using MediatR;

namespace OrderFlow.SharedKernel;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}