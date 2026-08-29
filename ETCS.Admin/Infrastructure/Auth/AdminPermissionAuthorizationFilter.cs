using ETCS.Shared.Infrastructure.Admin.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ETCS.Admin.Infrastructure.Auth;

public sealed class AdminPermissionAuthorizationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            await next();
            return;
        }

        if (HasAllowWithoutPermission(descriptor))
        {
            await next();
            return;
        }

        if (!RequiresPermission(descriptor))
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var moduleKey = ResolveModuleKey(descriptor);
        var action = ResolvePermissionAction(descriptor, context);
        var permissionKey = $"{moduleKey}.{action}";

        if (!user.HasPermission(permissionKey))
        {
            await DenyAsync(context);
            return;
        }

        await next();
    }

    private static bool RequiresPermission(ControllerActionDescriptor descriptor)
    {
        if (descriptor.MethodInfo.IsDefined(typeof(AllowWithoutAdminPermissionAttribute), true))
        {
            return false;
        }

        if (descriptor.MethodInfo.IsDefined(typeof(AdminPermissionAttribute), true))
        {
            return true;
        }

        return descriptor.ControllerTypeInfo.IsDefined(typeof(AdminPermissionAttribute), true);
    }

    private static bool HasAllowWithoutPermission(ControllerActionDescriptor descriptor) =>
        descriptor.MethodInfo.IsDefined(typeof(AllowWithoutAdminPermissionAttribute), true)
        || descriptor.ControllerTypeInfo.IsDefined(typeof(AllowWithoutAdminPermissionAttribute), true);

    private static string ResolveModuleKey(ControllerActionDescriptor descriptor)
    {
        if (!string.Equals(descriptor.ControllerName, "Report", StringComparison.OrdinalIgnoreCase))
        {
            return descriptor.ControllerName;
        }

        var action = descriptor.ActionName;
        if (action.Contains("Canteen", StringComparison.OrdinalIgnoreCase))
        {
            return "Report.CanteenTransactions";
        }

        if (action.Contains("AdminTransaction", StringComparison.OrdinalIgnoreCase))
        {
            return "Report.AdminTransaction";
        }

        if (action.Contains("TerminalSales", StringComparison.OrdinalIgnoreCase))
        {
            return "Report.TerminalSalesSummary";
        }

        if (action.Contains("MealOrderPaymentsMealDb", StringComparison.OrdinalIgnoreCase))
        {
            return "Report.MealOrderPaymentsMealDb";
        }

        if (action.Contains("MealOrderPayments", StringComparison.OrdinalIgnoreCase))
        {
            return "Report.MealOrderPayments";
        }

        if (action.Contains("MealOrdersMealDb", StringComparison.OrdinalIgnoreCase))
        {
            return "Report.MealOrdersMealDb";
        }

        if (action.Contains("MealOrders", StringComparison.OrdinalIgnoreCase))
        {
            return "Report.MealOrders";
        }

        return $"Report.{descriptor.ActionName}";
    }

    private static AdminPermissionAction ResolvePermissionAction(
        ControllerActionDescriptor descriptor,
        ActionExecutingContext context)
    {
        var actionName = descriptor.ActionName;
        if (string.Equals(actionName, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Delete;
        }

        if (string.Equals(actionName, "Blacklist", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionName, "Transfer", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Edit;
        }

        if (string.Equals(actionName, "Save", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveSavePermission(context);
        }

        return AdminPermissionAction.View;
    }

    private static AdminPermissionAction ResolveSavePermission(ActionExecutingContext context)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var idProperty = argument.GetType().GetProperty("Id");
            if (idProperty?.PropertyType == typeof(int))
            {
                var id = (int)(idProperty.GetValue(argument) ?? 0);
                return id > 0 ? AdminPermissionAction.Edit : AdminPermissionAction.Add;
            }
        }

        return AdminPermissionAction.Add;
    }

    private static Task DenyAsync(ActionExecutingContext context)
    {
        var isAjax = string.Equals(
            context.HttpContext.Request.Headers.XRequestedWith,
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase)
            || context.HttpContext.Request.Headers.Accept.Any(h => h?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

        if (isAjax)
        {
            context.Result = new JsonResult(new
            {
                Success = false,
                Message = "You do not have permission to perform this action."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        else
        {
            context.Result = new RedirectToActionResult(
                "AccessDenied",
                "Home",
                null);
        }

        return Task.CompletedTask;
    }
}
