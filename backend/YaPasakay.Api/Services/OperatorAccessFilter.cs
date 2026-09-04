using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class OperatorAccessFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? "";
        var required = OperatorAccess.RequiredPage(path);
        if (required is null)
        {
            await next();
            return;
        }

        var (user, pages) = await OperatorAccess.ResolveAsync(db, context.HttpContext.User, context.HttpContext.RequestAborted);
        if (user is null || user.Role != UserRole.Operator)
        {
            // Not an operator caller — [Authorize] / AdminAccessFilter handle other roles.
            await next();
            return;
        }

        if (!user.IsActive)
        {
            context.Result = new ObjectResult(new { message = "Account not found or inactive." }) { StatusCode = 401 };
            return;
        }

        if ((required is "employees" or "roles") && !user.IsMainOperator)
        {
            context.Result = new ObjectResult(new { message = "Only the main operator can manage employees and roles." })
            {
                StatusCode = 403
            };
            return;
        }

        if (!pages.Contains(required))
        {
            context.Result = new ObjectResult(new { message = "You do not have access to this module. Ask the main operator to update your role." })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
