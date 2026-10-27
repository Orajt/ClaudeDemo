using Shop.Orders.Application;
using Wolverine;

namespace Shop.Orders.Infrastructure;

/// <summary>Konfiguracja Wolverine specyficzna dla modułu Orders. Dołącza ją Host (<c>options.Include&lt;…&gt;()</c>).</summary>
public sealed class OrdersWolverineExtension : IWolverineExtension
{
    public void Configure(WolverineOptions options)
    {
        options.Discovery.IncludeAssembly(typeof(OrdersApplicationMarker).Assembly);

        // Wszystkie handlery modułu używają store'a modułu: sesja Martena, outbox, FetchForWriting (D-003).
        options.Policies.UseAncillaryStorageFromAssemblyContaining<OrdersApplicationMarker>(typeof(IOrdersStore));
    }
}
