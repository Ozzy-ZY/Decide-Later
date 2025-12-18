using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Application.Exceptions;

namespace WebProj.Middleware;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception caught by global handler");
            await HandleExceptionAsync(context, ex, env);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex, IHostEnvironment env)
    {
        // Map common exception types to appropriate status codes
        var statusCode = ex switch
        {
            MessageValidationException => StatusCodes.Status400BadRequest,
            MessageUnauthorizedException => StatusCodes.Status403Forbidden,
            ChatNotFoundException => StatusCodes.Status404NotFound,
            ChatUnauthorizedException => StatusCodes.Status403Forbidden,
            ChatOperationException => StatusCodes.Status400BadRequest,
            ArgumentNullException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        var includeDetails = env.IsDevelopment();

        var errorPayload = new
        {
            error = new
            {
                message = ex.Message,
                type = ex.GetType().Name,
                details = includeDetails ? ex.StackTrace : null
            }
        };

        var json = JsonSerializer.Serialize(errorPayload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsync(json);
    }
}
