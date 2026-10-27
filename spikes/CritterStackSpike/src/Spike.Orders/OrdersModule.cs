using JasperFx;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Spike.Orders.Domain;
using Wolverine;
using Wolverine.Marten;

namespace Spike.Orders;

/// <summary>Znacznik store'a Martena modułu Orders (H1).</summary>
public interface IOrdersStore : IDocumentStore;

public static class OrdersModule
{
    public const string SchemaName = "spike_orders";

    public static IServiceCollection AddOrdersModule(this IServiceCollection services, string connectionString)
    {
        services.AddMartenStore<IOrdersStore>(options =>
            {
                options.Connection(connectionString);
                options.DatabaseSchemaName = SchemaName;
                options.AutoCreateSchemaObjects = AutoCreate.None;
                options.Projections.Snapshot<Order>(SnapshotLifecycle.Inline);
            })
            .IntegrateWithWolverine();

        return services;
    }

    /// <summary>Handlery z assembly modułu korzystają z store'a <see cref="IOrdersStore"/> (H2).</summary>
    public static WolverineOptions UseOrdersModule(this WolverineOptions options)
    {
        options.Policies.UseAncillaryStorageFromAssemblyContaining<IOrdersStore>(typeof(IOrdersStore));
        return options;
    }
}
