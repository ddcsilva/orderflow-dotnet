using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace OrderFlow.IntegrationTests;

/// <summary>
/// Demonstra a infraestrutura de Testcontainers: sobe SQL Server e Redis reais,
/// valida conectividade e descarta os containers ao fim. Serve de baseline para
/// futuros testes end-to-end por bounded context.
/// </summary>
public sealed class TestcontainersInfraTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _redis.StartAsync()).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(_sql.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask())
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task SqlServer_Container_AcceptsConnections()
    {
        await using var conn = new SqlConnection(_sql.GetConnectionString());
        await conn.OpenAsync().ConfigureAwait(false);

        conn.State.Should().Be(ConnectionState.Open);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);

        result.Should().Be(1);
    }

    [Fact]
    public void Redis_Container_HasConnectionString()
    {
        var cs = _redis.GetConnectionString();

        cs.Should().NotBeNullOrEmpty();
    }
}
