using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using OrderFlow.Orders.Application.Common.Interfaces;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace OrderFlow.Orders.Infrastructure.Http;

/// <summary>
/// Cliente HTTP do Catalog API protegido por pipeline Polly v8 (retry + CB + timeout + bulkhead).
/// Implementa degradação graciosa: erros transientes são logados e devolvem <c>null</c>,
/// permitindo ao caller decidir entre "Pending Validation" e cache stale.
/// </summary>
internal sealed class CatalogHttpClient : ICatalogClient
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<CatalogHttpClient> _logger;

    public CatalogHttpClient(
        HttpClient http,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<CatalogHttpClient> logger)
    {
        _http = http;
        _pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>(
            OrderFlow.Resilience.ResiliencePipelineKeys.CatalogClient);
        _logger = logger;
    }

    public async Task<ProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _pipeline.ExecuteAsync(
                async ct => await _http.GetAsync(
                    new Uri($"/api/products/{productId}", UriKind.Relative), ct),
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProductSnapshot>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                 or TimeoutException
                                 or TimeoutRejectedException
                                 or BrokenCircuitException)
        {
            // Degradação graciosa — pipeline já fez retries; logar e devolver null.
            _logger.LogWarning(ex,
                "Catalog API unavailable for product {ProductId}. Falling back to null.", productId);
            return null;
        }
    }
}
