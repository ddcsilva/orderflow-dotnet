namespace OrderFlow.SharedKernel;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}