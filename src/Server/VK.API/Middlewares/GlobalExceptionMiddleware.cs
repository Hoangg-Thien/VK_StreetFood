using System.Net;
using System.Text.Json;
using VK.Core.Exceptions;

namespace VK.API.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred while executing the request.");
            await HandleExceptionAsync(context, ex, _env);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, IWebHostEnvironment env)
    {
        var (statusCode, message) = exception switch
        {
            EntityNotFoundException nf => (StatusCodes.Status404NotFound, nf.Message),
            BusinessRuleViolationException br => (StatusCodes.Status400BadRequest, br.Message),
            ForbiddenOperationException fo => (StatusCodes.Status403Forbidden, fo.Message),
            _ => (StatusCodes.Status500InternalServerError, "An internal server error occurred.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new
        {
            StatusCode = statusCode,
            Message = message,
            Detailed = env.IsDevelopment() && statusCode == 500 ? exception.Message : null
        };

    return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
