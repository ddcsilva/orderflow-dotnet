using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OrderFlow.SharedKernel.Observability;

public static class DiagnosticsConfig
{
    public const string ServiceName = "OrderFlow";

    public static readonly ActivitySource ActivitySource = new(ServiceName);

    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> OrdersCreated = Meter.CreateCounter<long>(
        "orderflow.orders.created", "orders", "Total orders created");

    public static readonly Counter<long> OrdersConfirmed = Meter.CreateCounter<long>(
        "orderflow.orders.confirmed", "orders", "Total orders confirmed");

    public static readonly Counter<long> OrdersCancelled = Meter.CreateCounter<long>(
        "orderflow.orders.cancelled", "orders", "Total orders cancelled");

    public static readonly Histogram<double> OrderProcessingDuration = Meter.CreateHistogram<double>(
        "orderflow.orders.processing_duration", "ms", "Order processing duration");

    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "orderflow.cache.hits", "operations", "Cache hit count");

    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "orderflow.cache.misses", "operations", "Cache miss count");
}
