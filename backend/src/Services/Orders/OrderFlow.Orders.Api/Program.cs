using System.Globalization;
using System.Text;
using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Orders.Application;
using OrderFlow.Orders.Infrastructure;
using OrderFlow.SharedKernel.Auth;
using OrderFlow.SharedKernel.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddOrderFlowSerilog();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication (validates tokens issued by Identity API)
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// Redis distributed cache
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "OrderFlow.Orders:";
});

// Output caching
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("Orders", policy => policy
        .Expire(TimeSpan.FromMinutes(2))
        .Tag("orders"));
});

// OpenTelemetry
builder.Services.AddOrderFlowOpenTelemetry(builder.Configuration, "OrderFlow.Orders.Api");

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("OrdersDb")!,
        name: "sqlserver",
        tags: ["db", "ready", "startup"])
    .AddRedis(
        redisConnectionString,
        name: "redis",
        tags: ["cache", "ready"]);
// NOTA: HealthChecks.Rabbitmq 9.x requer registro de IConnection — simplificado por ora.
// Reintroduzir após registrar IConnectionFactory de forma compartilhada.

var app = builder.Build();

app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Validation Error",
                status = 400,
                errors
            });
        }
    });
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("startup"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

await app.RunAsync();

public partial class Program;
