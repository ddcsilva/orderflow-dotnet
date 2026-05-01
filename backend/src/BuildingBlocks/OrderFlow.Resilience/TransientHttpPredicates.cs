using System.Net;
using Polly;

namespace OrderFlow.Resilience;

/// <summary>
/// Predicados reutilizáveis para detectar falhas transientes em respostas HTTP.
/// </summary>
public static class TransientHttpPredicates
{
    /// <summary>
    /// True para falhas transientes "clássicas": exceções de transporte, timeout do Polly,
    /// 5xx (servidor) e 408 RequestTimeout. NÃO inclui 429 — esse vai pelo rate limiter.
    /// </summary>
    public static ValueTask<bool> IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutException)
        {
            return ValueTask.FromResult(true);
        }

        if (outcome.Result is { } response)
        {
            var status = (int)response.StatusCode;
            return ValueTask.FromResult(
                status >= 500 ||
                response.StatusCode == HttpStatusCode.RequestTimeout);
        }

        return ValueTask.FromResult(false);
    }
}
