using System.Globalization;
using System.Security.Claims;
using System.Transactions;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IotGateway.WebAPI.Filters;

// Register globally with Filters.Add<SafeAuditActionFilter>(int.MinValue).
// Action filters cannot observe authorization/resource short circuits or result-execution failures.
public sealed class SafeAuditActionFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SafeAuditActionFilter> _logger;

    public SafeAuditActionFilter(
        IServiceScopeFactory scopeFactory, ILogger<SafeAuditActionFilter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public int Order => int.MinValue;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var entry = CreateEntry(context);
        ActionExecutedContext? executed = null;
        Exception? thrown = null;
        try
        {
            executed = await next();
        }
        catch (Exception exception)
        {
            thrown = exception;
            throw;
        }
        finally
        {
            if (entry is not null)
                await PersistAsync(entry, context, executed, thrown);
        }
    }

    private static AuditLog? CreateEntry(ActionExecutingContext context)
    {
        try
        {
            var method = context.HttpContext.Request.Method switch
            {
                "POST" => "POST",
                "PUT" => "PUT",
                "PATCH" => "PATCH",
                "DELETE" => "DELETE",
                _ => null
            };
            if (method is null || context.ActionDescriptor is not ControllerActionDescriptor action)
                return null;

            long? subject = null;
            foreach (var claim in context.HttpContext.User.Identities
                         .Where(x => x.IsAuthenticated).SelectMany(x => x.Claims)
                         .Where(x => x.Type == ClaimTypes.NameIdentifier || x.Type == "sub"))
            {
                var id = NumericId(claim.Value);
                if (!id.HasValue || (subject.HasValue && subject != id)) return null;
                subject = id;
            }
            if (!subject.HasValue) return null;

            var template = action.AttributeRouteInfo?.Template
                ?? action.ControllerName + "/" + action.ActionName;
            return new AuditLog
            {
                UserId = subject.Value,
                Username = subject.Value.ToString(CultureInfo.InvariantCulture),
                ActionType = method,
                TargetType = Bound(action.ControllerName, 50),
                TargetId = RouteTargetId(context)?.ToString(CultureInfo.InvariantCulture) ?? "",
                Description = Bound(method + " " + template, 500),
                RequestIp = "",
                RequestParams = null,
                ErrorMessage = null,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistAsync(AuditLog entry, ActionExecutingContext context,
        ActionExecutedContext? executed, Exception? thrown)
    {
        try
        {
            var exception = thrown ?? (executed?.ExceptionHandled == false ? executed.Exception : null);
            var httpCode = exception is null
                ? ResultStatus(executed?.Result, context.HttpContext.Response.StatusCode)
                : ExceptionStatus(exception);
            var applicationCode = exception is null ? EnvelopeCode(executed?.Result) : null;
            var failed = exception is not null || httpCode >= 400
                || (applicationCode.HasValue && applicationCode != 200);
            entry.Result = (failed ? "Failed:" : "Success:") + httpCode.ToString(CultureInfo.InvariantCulture)
                + (applicationCode.HasValue ? ":" + applicationCode.Value.ToString(CultureInfo.InvariantCulture) : "");

            // Never reuse the action's context, connection or ambient transaction:
            // SaveChanges here must not commit entities left tracked by a failed action.
            using var suppression = new TransactionScope(
                TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var scope = _scopeFactory.CreateAsyncScope();
            var options = new DbContextOptionsBuilder<AppDbContext>(
                    scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>())
                .UseLoggerFactory(NullLoggerFactory.Instance)
                .LogTo(_ => { })
                .EnableSensitiveDataLogging(false)
                .EnableDetailedErrors(false)
                .Options;
            await using var db = new AppDbContext(options);
            db.Database.SetCommandTimeout(5);
            db.AuditLogs.Add(entry);
            await db.SaveChangesAsync(timeout.Token);
            suppression.Complete();
        }
        catch
        {
            // Best effort: even scope/context disposal failures must preserve the action outcome.
            try
            {
                _logger.LogWarning(new EventId(4101, "AuditWriteUnavailable"),
                    "Audit metadata could not be persisted.");
            }
            catch
            {
                // A logging sink failure must not change the action's response either.
            }
        }
    }

    private static long? RouteTargetId(ActionExecutingContext context)
    {
        if (context.RouteData.Values.TryGetValue("id", out var id))
            return NumericRouteValue(id);
        var ids = context.RouteData.Values
            .Where(x => x.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase)).ToArray();
        return ids.Length == 1 ? NumericRouteValue(ids[0].Value) : null;
    }

    private static long? NumericRouteValue(object? value) => value switch
    {
        string text => NumericId(text),
        long id when id > 0 => id,
        int id when id > 0 => id,
        _ => null
    };

    private static long? NumericId(string? value) =>
        value is { Length: > 0 and <= 100 }
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0 ? id : null;

    private static string Bound(string value, int length) =>
        value.Length <= length ? value : value[..length];

    // Match the existing GlobalExceptionFilter; a bare AppException is currently its 500 fallback.
    private static int ExceptionStatus(Exception exception) => exception switch
    {
        UnauthorizedException => 401,
        ForbiddenException => 403,
        ValidationException => 422,
        NotFoundException => 404,
        BusinessException business => StatusCode(business.StatusCode),
        _ => 500
    };

    private static int ResultStatus(IActionResult? result, int responseStatus)
    {
        var status = result switch
        {
            ChallengeResult => 401,
            ForbidResult => 403,
            RedirectResult redirect => RedirectStatus(redirect.Permanent, redirect.PreserveMethod),
            LocalRedirectResult redirect => RedirectStatus(redirect.Permanent, redirect.PreserveMethod),
            RedirectToActionResult redirect => RedirectStatus(redirect.Permanent, redirect.PreserveMethod),
            RedirectToRouteResult redirect => RedirectStatus(redirect.Permanent, redirect.PreserveMethod),
            RedirectToPageResult redirect => RedirectStatus(redirect.Permanent, redirect.PreserveMethod),
            ObjectResult obj => obj.StatusCode ?? (obj.Value as ProblemDetails)?.Status ?? responseStatus,
            IStatusCodeActionResult codeResult => codeResult.StatusCode ?? responseStatus,
            _ => responseStatus
        };
        // The default MVC output formatter turns a null ObjectResult with HTTP 200 into 204.
        if (status == 200 && result is ObjectResult { Value: null }) status = 204;
        return StatusCode(status);
    }

    private static int RedirectStatus(bool permanent, bool preserveMethod) =>
        preserveMethod ? (permanent ? 308 : 307) : (permanent ? 301 : 302);

    private static int? EnvelopeCode(IActionResult? result)
    {
        var value = result switch
        {
            ObjectResult obj => obj.Value,
            JsonResult json => json.Value,
            _ => null
        };
        if (value is ApiResponse response) return StatusCode(response.Code);
        // Read only the known envelope's integer Code, never Data, Message or arbitrary getters.
        for (var type = value?.GetType(); type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>))
                return type.GetProperty(nameof(ApiResponse.Code))?.GetValue(value) is int code
                    ? StatusCode(code) : null;
        }
        return null;
    }

    private static int StatusCode(int value) => value is >= 100 and <= 599 ? value : 500;
}
