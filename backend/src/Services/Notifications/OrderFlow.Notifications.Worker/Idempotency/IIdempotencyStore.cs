using System.Collections.Concurrent;

namespace OrderFlow.Notifications.Worker.Idempotency;

/// <summary>
/// Store de idempotência para deduplicação de mensagens já processadas.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Tenta marcar a mensagem como processada.
    /// </summary>
    /// <returns>true se foi a primeira vez (deve processar), false se já processada.</returns>
    Task<bool> TryMarkAsProcessedAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Implementação em memória — adequada para desenvolvimento e instâncias únicas.
/// Em produção/multi-instância, substituir por Redis ou tabela no banco com índice único em MessageId.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _processed = new();

    public Task<bool> TryMarkAsProcessedAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var added = _processed.TryAdd(key, 0);
        return Task.FromResult(added);
    }
}
