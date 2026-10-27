using Marten;

namespace Shop.Orders.Infrastructure;

/// <summary>Store Martena modułu Orders – osobny od store'ów innych modułów (D-003).</summary>
public interface IOrdersStore : IDocumentStore;
