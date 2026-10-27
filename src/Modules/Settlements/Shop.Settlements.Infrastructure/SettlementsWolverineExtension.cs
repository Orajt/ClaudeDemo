using Shop.Settlements.Application;
using Wolverine;

namespace Shop.Settlements.Infrastructure;

/// <summary>Konfiguracja Wolverine specyficzna dla modułu Settlements. Dołącza ją Host (<c>options.Include&lt;…&gt;()</c>).</summary>
public sealed class SettlementsWolverineExtension : IWolverineExtension
{
    public void Configure(WolverineOptions options)
    {
        options.Discovery.IncludeAssembly(typeof(SettlementsApplicationMarker).Assembly);

        // Wszystkie handlery modułu używają store'a modułu: sesja Martena, outbox, FetchForWriting (D-003).
        options.Policies.UseAncillaryStorageFromAssemblyContaining<SettlementsApplicationMarker>(typeof(ISettlementsStore));
    }
}
