using System.Net;
using System.Text.Json;
using FluentValidation;

namespace OrderFlow.Catalog.Api.Middleware;

public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorResponse) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse(
                    "Erro de Validação",
                    validationEx.Errors.Select(e => e.ErrorMessage).ToArray())),

            KeyNotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ErrorResponse("Não Encontrado", [notFoundEx.Message])),

            InvalidOperationException invalidOpEx => (
                HttpStatusCode.Conflict,
                new ErrorResponse("Violação de Regra de Negócio", [invalidOpEx.Message])),

            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse("Argumento Inválido", [argEx.Message])),

            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse("Erro Interno do Servidor", ["Ocorreu um erro inesperado."]))
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            LogUnhandledException(logger, exception, exception.Message);
        }
        else
        {
            LogHandledException(logger, exception, (int)statusCode, exception.Message);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);

        await context.Response.WriteAsync(json);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Exceção não tratada: {ExceptionMessage}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string exceptionMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exceção tratada: {StatusCode} - {ExceptionMessage}")]
    private static partial void LogHandledException(ILogger logger, Exception exception, int statusCode, string exceptionMessage);
}

public sealed record ErrorResponse(string Title, string[] Errors);
