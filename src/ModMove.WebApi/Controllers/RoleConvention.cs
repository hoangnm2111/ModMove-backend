using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using ModMove.Domain.Enums;

namespace ModMove.WebApi.Controllers;

/// <summary>
/// One folder = one role (SDS 2.1). For Controllers/Customer/OrdersController this gives the route
/// api/customer/orders and requires the Customer role. Public and Webhooks stay anonymous
/// (the webhook proves itself with the PayOS signature). A controller that declares its own [Route] keeps it.
/// </summary>
public sealed class RoleConvention : IControllerModelConvention
{
    private const string Root = "ModMove.WebApi.Controllers.";

    private static readonly Dictionary<string, UserRole> RolesByFolder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Customer"] = UserRole.Customer,
        ["Driver"] = UserRole.Driver,
        ["Dispatcher"] = UserRole.Dispatcher,
        ["Admin"] = UserRole.Administrator,
    };

    public void Apply(ControllerModel controller)
    {
        var ns = controller.ControllerType.Namespace;
        if (ns is null || !ns.StartsWith(Root, StringComparison.Ordinal))
            return;

        var folder = ns[Root.Length..].Split('.')[0];
        foreach (var selector in controller.Selectors.Where(s => s.AttributeRouteModel is null))
            selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute($"api/{folder.ToLowerInvariant()}/[controller]"));

        if (RolesByFolder.TryGetValue(folder, out var role))
            controller.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireRole(role.ToString()).Build()));
    }
}
