using System.Net;
using FluentAssertions;
using OrderFlow.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Testing;
using Polly.Timeout;

namespace OrderFlow.Resilience.Tests;

public sealed class HttpResiliencePipelineTests
{
    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline(HttpResilienceOptions? options = null)
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        ResiliencePipelineRegistrationExtensions.BuildHttpPipeline(builder, options ?? new HttpResilienceOptions());
        return builder.Build();
    }

    [Fact]
    public void Pipeline_HasExpectedStrategies_InCanonicalOrder()
    {
        var pipeline = BuildPipeline();

        var descriptor = pipeline.GetPipelineDescriptor();

        descriptor.Strategies.Should().HaveCount(5);
        descriptor.Strategies[0].Options.Should().BeOfType<TimeoutStrategyOptions>()
            .Which.Name.Should().Be("outer-timeout");
        descriptor.Strategies[1].Options.Should().BeOfType<RateLimiterStrategyOptions>()
            .Which.Name.Should().Be("bulkhead");
        descriptor.Strategies[2].Options.Should().BeOfType<RetryStrategyOptions<HttpResponseMessage>>()
            .Which.Name.Should().Be("retry");
        descriptor.Strategies[3].Options.Should().BeOfType<CircuitBreakerStrategyOptions<HttpResponseMessage>>()
            .Which.Name.Should().Be("circuit-breaker");
        descriptor.Strategies[4].Options.Should().BeOfType<TimeoutStrategyOptions>()
            .Which.Name.Should().Be("attempt-timeout");
    }

    [Fact]
    public async Task Pipeline_RetriesTransientFailures_AndEventuallySucceeds()
    {
        var attempts = 0;
        var pipeline = BuildPipeline(new HttpResilienceOptions
        {
            MaxRetryAttempts = 3,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
            CircuitBreakerMinimumThroughput = 100, // evita CB abrir nos testes
        });

        var result = await pipeline.ExecuteAsync(async _ =>
        {
            await Task.Yield();
            attempts++;
            return attempts < 3
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        attempts.Should().Be(3);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pipeline_DoesNotRetry_OnNonTransientStatus()
    {
        var attempts = 0;
        var pipeline = BuildPipeline(new HttpResilienceOptions
        {
            MaxRetryAttempts = 5,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        });

        var result = await pipeline.ExecuteAsync(async _ =>
        {
            await Task.Yield();
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        attempts.Should().Be(1);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pipeline_CircuitBreaker_OpensAfterRepeatedFailures()
    {
        var pipeline = BuildPipeline(new HttpResilienceOptions
        {
            MaxRetryAttempts = 0, // simplifica observação do CB
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(2),
            CircuitBreakerMinimumThroughput = 4,
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30),
        });

        // Dispara 8 falhas consecutivas
        for (var i = 0; i < 8; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(async _ =>
                {
                    await Task.Yield();
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                });
            }
            catch (BrokenCircuitException)
            {
                // CB abriu — sucesso esperado
                return;
            }
        }

        // Próxima execução deve lançar BrokenCircuitException
        var act = async () => await pipeline.ExecuteAsync(async _ =>
        {
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await act.Should().ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task Pipeline_AttemptTimeout_FailsFast()
    {
        var pipeline = BuildPipeline(new HttpResilienceOptions
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(50),
            MaxRetryAttempts = 0,
            CircuitBreakerMinimumThroughput = 100,
        });

        var act = async () => await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    [Fact]
    public void Pipeline_WithChaosEnabled_AppendsChaosStrategies()
    {
        var pipeline = BuildPipeline(new HttpResilienceOptions
        {
            Chaos = new ChaosOptions { Enabled = true },
        });

        var descriptor = pipeline.GetPipelineDescriptor();

        descriptor.Strategies.Should().HaveCount(7);
        descriptor.Strategies[5].Options.Should().NotBeNull();
        descriptor.Strategies[6].Options.Should().NotBeNull();
    }
}
