using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using ModMove.Domain.Enums;

namespace ModMove.WebApi.Controllers;

/// <summary>
/// One folder = one role (SDS 2.1). For Controllers/Customer/OrdersController this gives the route
/// api/customer/orders and requires the Customer role. Account needs any signed-in user; only Public and Webhooks are anonymous
/// (the webhook proves itself with the PayOS signature). Any other folder fails at startup, so a typo
/// can never publish an endpoint without a role check. A controller that declares its own [Route] keeps it.
/// </summary>
public sealed class RoleConvention : IControllerModelConvention
{
    public const string PlaceholderRoute = "api/[controller]";

    private const string Root = "ModMove.WebApi.Controllers.";

    private static readonly Dictionary<string, UserRole> RolesByFolder = new(StringComparer.Ordinal)
    {
        ["Customer"] = UserRole.Customer,
        ["Driver"] = UserRole.Driver,
        ["Dispatcher"] = UserRole.Dispatcher,
        ["Admin"] = UserRole.Administrator,
    };

    private static readonly HashSet<string> AnonymousFolders = new(StringComparer.Ordinal) { "Public", "Webhooks" };

    // Endpoints every signed-in user has (password, profile, logout, FCM token), whatever the role (ARCHITECTURE.md, API conventions).
    private const string AnyRoleFolder = "Account";

    public void Apply(ControllerModel controller)
    {
        var ns = controller.ControllerType.Namespace ?? "";
        var folder = ns.StartsWith(Root, StringComparison.Ordinal) ? ns[Root.Length..].Split('.')[0] : "";

        AuthorizationPolicy? policy = RolesByFolder.TryGetValue(folder, out var role)
            ? new AuthorizationPolicyBuilder().RequireRole(role.ToString()).Build()
            : folder == AnyRoleFolder ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
            : AnonymousFolders.Contains(folder) ? null
            : throw new InvalidOperationException(
                $"Controller {controller.ControllerType.FullName} must live in Controllers/<Customer|Driver|Dispatcher|Admin|Account|Public|Webhooks>.");

        foreach (var selector in controller.Selectors.Where(s => s.AttributeRouteModel?.Template is null or PlaceholderRoute))
            selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute($"api/{folder.ToLowerInvariant()}/[controller]"));

        if (policy is not null)
            controller.Filters.Add(new AuthorizeFilter(policy));
    }
}
