using Marten;

namespace Shop.Settlements.Infrastructure;

/// <summary>Store Martena modułu Settlements – osobny od store'ów innych modułów (D-003).</summary>
public interface ISettlementsStore : IDocumentStore;
