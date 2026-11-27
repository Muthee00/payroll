using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PayMaster.Web.Infrastructure;

/// <summary>
/// Runs any registered FluentValidation validator for each bound action argument and
/// copies failures into ModelState, so controllers only check <c>ModelState.IsValid</c>.
/// </summary>
public sealed class FluentValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var (name, argument) in context.ActionArguments)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            // Forms either post bare property names or prefix them (e.g. "Input.FirstName");
            // key errors the same way so tag helpers render them next to the right field.
            var prefix = context.ModelState.Keys
                .Select(k => k.Split('.')[0])
                .FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase)
                                     && context.ModelState.Keys.Any(x => x.StartsWith(k + ".", StringComparison.Ordinal)));

            foreach (var error in result.Errors)
            {
                var key = prefix is null || string.IsNullOrEmpty(error.PropertyName)
                    ? error.PropertyName ?? name
                    : $"{prefix}.{error.PropertyName}";
                context.ModelState.AddModelError(key, error.ErrorMessage);
            }
        }

        await next();
    }
}
