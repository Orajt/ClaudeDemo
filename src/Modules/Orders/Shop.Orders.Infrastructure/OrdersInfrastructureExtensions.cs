using JasperFx;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Wolverine.Marten;

namespace Shop.Orders.Infrastructure;

public static class OrdersInfrastructureExtensions
{
    public const string SchemaName = "orders";

    private const string ConnectionStringName = "shop";

    public static IHostApplicationBuilder AddOrdersModule(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Brak connection stringu '{ConnectionStringName}'.");

        builder.Services.AddMartenStore<IOrdersStore>(options =>
            {
                options.Connection(connectionString);
                options.DatabaseSchemaName = SchemaName;
                // Schemat zmienia wyłącznie Flyway (D-004).
                options.AutoCreateSchemaObjects = AutoCreate.None;
            })
            .IntegrateWithWolverine();

        return builder;
    }
}
