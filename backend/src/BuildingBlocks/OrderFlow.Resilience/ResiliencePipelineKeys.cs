namespace OrderFlow.Resilience;

/// <summary>
/// Chaves canônicas das pipelines de resiliência registradas no <see cref="ResiliencePipelineProvider{TKey}"/>.
/// Centralizar aqui evita "magic strings" espalhadas pelos serviços consumidores.
/// </summary>
public static class ResiliencePipelineKeys
{
    /// <summary>Pipeline para chamadas HTTP ao Catalog API (Orders → Catalog).</summary>
    public const string CatalogClient = "catalog-client";

    /// <summary>Pipeline para chamadas HTTP ao Identity API (Gateway/Orders → Identity).</summary>
    public const string IdentityClient = "identity-client";

    /// <summary>Pipeline genérica para qualquer downstream HTTP de baixo risco.</summary>
    public const string DefaultHttp = "default-http";
}
