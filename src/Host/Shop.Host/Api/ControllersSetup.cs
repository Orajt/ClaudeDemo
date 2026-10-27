using Shop.DataHub.Api;
using Shop.Host.ErrorHandling;
using Shop.Orders.Api;
using Shop.Settlements.Api;

namespace Shop.Host.Api;

public static class ControllersSetup
{
    /// <summary>Kontrolery modułów (implementacje wygenerowanego kontraktu) i błędy jako ProblemDetails.</summary>
    public static WebApplicationBuilder AddShopControllers(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(OrdersController).Assembly)
            .AddApplicationPart(typeof(SettlementsController).Assembly)
            .AddApplicationPart(typeof(DataHubController).Assembly);

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ExceptionToProblemDetailsMapper>();

        return builder;
    }
}
