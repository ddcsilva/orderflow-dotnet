using OrderFlow.Catalog.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilog();
builder.AddApiServices();

var app = builder.Build();

app.ConfigurePipeline();
await app.ApplyMigrationsAsync();

app.Run();

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program;
