using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class AdminAccessFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? "";
        var required = AdminAccess.RequiredPage(path);
        if (required is null)
        {
            await next();
            return;
        }

        var (user, pages) = await AdminAccess.ResolveAsync(db, context.HttpContext.User, context.HttpContext.RequestAborted);
        if (user is null || user.Role != Domain.Enums.UserRole.Admin || !user.IsActive)
        {
            context.Result = new ObjectResult(new { message = "Account not found or inactive." }) { StatusCode = 401 };
            return;
        }

        var allowed = required == "search"
            ? pages.Contains("operators") || pages.Contains("customers")
            : pages.Contains(required);

        if (!allowed)
        {
            context.Result = new ObjectResult(new { message = "You do not have access to this page. Ask the main admin to update your user group." })
            {
                StatusCode = 403
            };
            return;
        }

        if (required == "settings" && path.StartsWith("/api/admin/access", StringComparison.OrdinalIgnoreCase) && !user.IsMainAdmin)
        {
            context.Result = new ObjectResult(new { message = "Only the main admin can manage users and access groups." })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
