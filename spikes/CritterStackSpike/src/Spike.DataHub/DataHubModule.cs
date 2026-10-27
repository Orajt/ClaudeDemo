using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;

namespace Spike.DataHub;

public static class DataHubModule
{
    private static readonly TimeSpan[] RetryCooldowns =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250)
    ];

    public static IServiceCollection AddDataHubModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextWithWolverineIntegration<DataHubDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DataHubDbContext.SchemaName)));

        return services;
    }

    /// <summary>
    /// Zdarzenia stanu z Orders i Settlements mogą jednocześnie zmieniać ten sam wiersz widoku.
    /// Konflikt xmin albo równoległe wstawienie wiersza → Wolverine ponawia handler, który wczytuje wiersz od nowa (D-006).
    /// </summary>
    public static WolverineOptions UseDataHubModule(this WolverineOptions options)
    {
        options.OnException<DbUpdateConcurrencyException>()
            .RetryWithCooldown(RetryCooldowns);

        options.OnException<DbUpdateException>(IsUniqueViolation)
            .RetryWithCooldown(RetryCooldowns);

        return options;
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
