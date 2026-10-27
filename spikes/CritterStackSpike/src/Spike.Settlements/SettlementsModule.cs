using JasperFx;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Marten;

namespace Spike.Settlements;

/// <summary>Znacznik store'a Martena modułu Settlements (H1).</summary>
public interface ISettlementsStore : IDocumentStore;

public static class SettlementsModule
{
    public const string SchemaName = "spike_settlements";

    public static IServiceCollection AddSettlementsModule(this IServiceCollection services, string connectionString)
    {
        services.AddMartenStore<ISettlementsStore>(options =>
            {
                options.Connection(connectionString);
                options.DatabaseSchemaName = SchemaName;
                options.AutoCreateSchemaObjects = AutoCreate.None;
                options.Projections.Snapshot<OrderSettlement>(SnapshotLifecycle.Inline);
                options.Projections.Snapshot<SettlementReport>(SnapshotLifecycle.Inline);
            })
            .IntegrateWithWolverine();

        return services;
    }

    public static WolverineOptions UseSettlementsModule(this WolverineOptions options)
    {
        options.Policies.UseAncillaryStorageFromAssemblyContaining<ISettlementsStore>(typeof(ISettlementsStore));
        return options;
    }
}
