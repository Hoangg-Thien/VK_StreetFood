using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VK.API.Middlewares;
using VK.Core.Exceptions;

namespace VK.API.Tests.Unit;

public class GlobalExceptionMiddlewareTests
{
    private class ErrorResponseModel
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public string? Detailed { get; set; }
    }

    [Fact]
    public async Task InvokeAsync_WhenEntityNotFoundException_Returns404NotFound()
    {
        var context = CreateHttpContext();
        var env = CreateEnvironment("Production");
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new EntityNotFoundException("PointOfInterest", 123),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var body = await ReadResponseBodyAsync(context);
        var response = JsonSerializer.Deserialize<ErrorResponseModel>(body, JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal("PointOfInterest với id '123' không tồn tại.", response.Message);
        Assert.Null(response.Detailed);
    }

    [Fact]
    public async Task InvokeAsync_WhenBusinessRuleViolationException_Returns400BadRequest()
    {
        var context = CreateHttpContext();
        var env = CreateEnvironment("Production");
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new BusinessRuleViolationException("Đánh giá không hợp lệ"),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var body = await ReadResponseBodyAsync(context);
        var response = JsonSerializer.Deserialize<ErrorResponseModel>(body, JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Đánh giá không hợp lệ", response.Message);
        Assert.Null(response.Detailed);
    }

    [Fact]
    public async Task InvokeAsync_WhenForbiddenOperationException_Returns403Forbidden()
    {
        var context = CreateHttpContext();
        var env = CreateEnvironment("Production");
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new ForbiddenOperationException("Không có quyền thực hiện thao tác này"),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var body = await ReadResponseBodyAsync(context);
        var response = JsonSerializer.Deserialize<ErrorResponseModel>(body, JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal("Không có quyền thực hiện thao tác này", response.Message);
        Assert.Null(response.Detailed);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_InProduction_Returns500WithoutDetailed()
    {
        var context = CreateHttpContext();
        var env = CreateEnvironment("Production");
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("Internal db connection failure"),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var body = await ReadResponseBodyAsync(context);
        var response = JsonSerializer.Deserialize<ErrorResponseModel>(body, JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(500, response.StatusCode);
        Assert.Equal("An internal server error occurred.", response.Message);
        Assert.Null(response.Detailed);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_InDevelopment_Returns500WithDetailed()
    {
        var context = CreateHttpContext();
        var env = CreateEnvironment("Development");
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("Database crashed"),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var body = await ReadResponseBodyAsync(context);
        var response = JsonSerializer.Deserialize<ErrorResponseModel>(body, JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(500, response.StatusCode);
        Assert.Equal("An internal server error occurred.", response.Message);
        Assert.Equal("Database crashed", response.Detailed);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_CallsNextDelegate()
    {
        var context = CreateHttpContext();
        var env = CreateEnvironment("Production");
        var nextCalled = false;

        var middleware = new GlobalExceptionMiddleware(
            ctx =>
            {
                nextCalled = true;
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<GlobalExceptionMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(e => e.EnvironmentName).Returns(environmentName);
        return mock.Object;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
