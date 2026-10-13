using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WmsDistributionCenter.Api.Infrastructure;

/// <summary>Requested resource does not exist (HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The request is valid but violates a business rule (HTTP 400).</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>The request conflicts with the current state, e.g. a concurrent stock change (HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Maps domain exceptions to consistent JSON problem responses.</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, message) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            KeyNotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            BusinessRuleException e => (StatusCodes.Status400BadRequest, e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "The record was changed by someone else. Reload and try again."),
            DbUpdateException => (StatusCodes.Status409Conflict,
                "The change conflicts with existing data (duplicate or still referenced)."),
            _ => (0, string.Empty)
        };

        if (status == 0)
        {
            logger.LogError(exception, "Unhandled exception");
            status = StatusCodes.Status500InternalServerError;
            message = "An unexpected error occurred.";
        }
        else
        {
            logger.LogInformation("Request rejected with {Status}: {Message}", status, message);
        }

        var problem = new ProblemDetails { Status = status, Title = message, Detail = message };
        problem.Extensions["message"] = message;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
