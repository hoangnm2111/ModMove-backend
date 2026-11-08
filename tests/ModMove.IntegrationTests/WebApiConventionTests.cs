using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application.Common.Exceptions;
using ModMove.WebApi.Controllers;
using ModMove.WebApi.Middlewares;

// Stand-in controllers: the convention only looks at the namespace, so they can live in the test assembly.
namespace ModMove.WebApi.Controllers.Customer { public sealed class FakeOrdersController : ApiControllerBase; }
namespace ModMove.WebApi.Controllers.Driver { public sealed class FakeJobsController : ApiControllerBase; }
namespace ModMove.WebApi.Controllers.Dispatcher { public sealed class FakeIncidentsController : ApiControllerBase; }
namespace ModMove.WebApi.Controllers.Admin { public sealed class FakeUsersController : ApiControllerBase; }
namespace ModMove.WebApi.Controllers.Public { public sealed class FakeVehicleTypesController : ApiControllerBase; }
namespace ModMove.WebApi.Controllers.Webhooks { public sealed class FakePayOsController : ApiControllerBase; }
namespace ModMove.WebApi.Controllers.Drivers { public sealed class FakeTypoController : ApiControllerBase; }
namespace ModMove.WebApi.Controllers.Account { public sealed class FakeProfileController : ApiControllerBase; }

namespace ModMove.IntegrationTests
{
    public class WebApiConventionTests
    {
        private static ControllerModel Apply(Type controller)
        {
            // Same shape MVC builds: the placeholder route inherited from ApiControllerBase.
            var model = new ControllerModel(controller.GetTypeInfo(), []);
            model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel(new Microsoft.AspNetCore.Mvc.RouteAttribute(RoleConvention.PlaceholderRoute)),
            });
            new RoleConvention().Apply(model);
            return model;
        }

        private static async Task<bool> IsAllowedAsync(ControllerModel model, params string[] roles)
        {
            var policy = model.Filters.OfType<AuthorizeFilter>().Single().Policy!;
            var principal = new ClaimsPrincipal(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test"));
            var service = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider()
                .GetRequiredService<IAuthorizationService>();

            return (await service.AuthorizeAsync(principal, null, policy.Requirements)).Succeeded;
        }

        [Fact]
        public async Task Account_folder_is_for_any_signed_in_role_but_not_anonymous_D2()
        {
            var model = Apply(typeof(WebApi.Controllers.Account.FakeProfileController));
            var policy = model.Filters.OfType<AuthorizeFilter>().Single().Policy!;
            var service = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider()
                .GetRequiredService<IAuthorizationService>();
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity()); // no authentication type = not signed in

            Assert.Equal("api/account/[controller]", model.Selectors[0].AttributeRouteModel!.Template);
            foreach (var role in new[] { "Customer", "Driver", "Dispatcher", "Administrator" })
                Assert.True(await IsAllowedAsync(model, role));
            Assert.False((await service.AuthorizeAsync(anonymous, null, policy.Requirements)).Succeeded);
        }

        [Fact]
        public async Task Conflict_is_mapped_to_409()
        {
            var context = new DefaultHttpContext();
            var middleware = new ExceptionHandlingMiddleware(_ => throw new ConflictException(), NullLogger<ExceptionHandlingMiddleware>.Instance);

            await middleware.InvokeAsync(context);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        }

        [Theory]
        [InlineData(typeof(WebApi.Controllers.Customer.FakeOrdersController), "api/customer/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Driver.FakeJobsController), "api/driver/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Driver.WalletController), "api/driver/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Dispatcher.FakeIncidentsController), "api/dispatcher/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Admin.FakeUsersController), "api/admin/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Admin.RefundsController), "api/admin/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Admin.WithdrawalsController), "api/admin/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Public.FakeVehicleTypesController), "api/public/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Webhooks.FakePayOsController), "api/webhooks/[controller]")]
        [InlineData(typeof(WebApi.Controllers.Webhooks.PayOsController), "api/webhooks/[controller]")]
        public void Route_comes_from_the_role_folder(Type controller, string expected)
        {
            Assert.Equal(expected, Apply(controller).Selectors[0].AttributeRouteModel!.Template);
        }

        [Theory]
        [InlineData(typeof(WebApi.Controllers.Customer.FakeOrdersController), "Customer")]
        [InlineData(typeof(WebApi.Controllers.Driver.FakeJobsController), "Driver")]
        [InlineData(typeof(WebApi.Controllers.Driver.WalletController), "Driver")]
        [InlineData(typeof(WebApi.Controllers.Driver.UploadsController), "Driver")]
        [InlineData(typeof(WebApi.Controllers.Dispatcher.FakeIncidentsController), "Dispatcher")]
        [InlineData(typeof(WebApi.Controllers.Admin.FakeUsersController), "Administrator")]
        [InlineData(typeof(WebApi.Controllers.Admin.RefundsController), "Administrator")]
        [InlineData(typeof(WebApi.Controllers.Admin.WithdrawalsController), "Administrator")]
        public async Task Role_folder_allows_only_its_own_role(Type controller, string role)
        {
            var model = Apply(controller);

            Assert.True(await IsAllowedAsync(model, role));
            Assert.False(await IsAllowedAsync(model, "Guest"));
            Assert.False(await IsAllowedAsync(model)); // signed in without any role
            foreach (var other in new[] { "Customer", "Driver", "Dispatcher", "Administrator" }.Where(r => r != role))
                Assert.False(await IsAllowedAsync(model, other));
        }

        [Theory]
        [InlineData(typeof(WebApi.Controllers.Public.FakeVehicleTypesController))]
        [InlineData(typeof(WebApi.Controllers.Webhooks.FakePayOsController))]
        [InlineData(typeof(WebApi.Controllers.Webhooks.PayOsController))]
        public void Public_and_webhook_controllers_stay_anonymous(Type controller)
        {
            Assert.Empty(Apply(controller).Filters.OfType<AuthorizeFilter>());
        }

        [Theory]
        [InlineData(typeof(WebApi.Controllers.Drivers.FakeTypoController))] // "Drivers" is not a role folder
        [InlineData(typeof(WebApiConventionTests))]                          // outside Controllers/ entirely
        public void Controller_in_an_unknown_folder_fails_at_startup_instead_of_going_anonymous(Type controller)
        {
            Assert.Throws<InvalidOperationException>(() => Apply(controller));
        }
    }
}
