using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shop.DataHub.Application;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Shop.DataHub.Infrastructure;

/// <summary>Konfiguracja Wolverine specyficzna dla modułu DataHub. Dołącza ją Host (<c>options.Include&lt;…&gt;()</c>).</summary>
public sealed class DataHubWolverineExtension : IWolverineExtension
{
    private static readonly TimeSpan[] RetryCooldowns =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250)
    ];

    public void Configure(WolverineOptions options)
    {
        options.Discovery.IncludeAssembly(typeof(DataHubApplicationMarker).Assembly);

        // Zdarzenia stanu z Orders i Settlements mogą jednocześnie zmieniać ten sam wiersz widoku.
        // Konflikt xmin albo równoległe wstawienie wiersza → ponów handler, który wczyta wiersz od nowa (D-006, D-012).
        options.OnException<DbUpdateConcurrencyException>().RetryWithCooldown(RetryCooldowns);
        options.OnException<DbUpdateException>(IsUniqueViolation).RetryWithCooldown(RetryCooldowns);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
