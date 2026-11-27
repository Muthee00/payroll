using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;

namespace PayMaster.Web.Infrastructure;

/// <summary>Maps application exceptions to HTTP responses (404 / 403 / 409) instead of a 500 page.</summary>
public sealed class AppExceptionFilter(ILogger<AppExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case NotFoundException:
                context.Result = new ViewResult { ViewName = "Error", StatusCode = 404, ViewData = WithStatus(context, 404) };
                context.ExceptionHandled = true;
                break;

            case ForbiddenAccessException:
                logger.LogWarning("Forbidden: {User} on {Path}", context.HttpContext.User.Identity?.Name, context.HttpContext.Request.Path);
                context.Result = new ViewResult { ViewName = "~/Views/Account/AccessDenied.cshtml", StatusCode = 403 };
                context.ExceptionHandled = true;
                break;

            case DbUpdateConcurrencyException:
                context.Result = new ViewResult { ViewName = "Error", StatusCode = 409, ViewData = WithStatus(context, 409) };
                context.ExceptionHandled = true;
                break;
        }
    }

    private static Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary WithStatus(ExceptionContext context, int status)
    {
        var viewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
            new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
        {
            ["StatusCode"] = status,
            ["RequestId"] = context.HttpContext.TraceIdentifier
        };
        return viewData;
    }
}
