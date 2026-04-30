namespace OrderFlow.SharedKernel;

public abstract class AggregateRoot : AuditableEntity
{
    // Aggregate root herda Entity (que já tem DomainEvents).
    // A diferença semântica é que apenas AggregateRoots são "raízes" de repositórios.
}
