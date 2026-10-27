namespace Shop.Orders.Application;

/// <summary>
/// Wskazuje assembly Application modułu Orders: wyszukiwanie handlerów Wolverine i przypisanie ich do store'a modułu.
/// Klasa (nie statyczna), bo Wolverine przyjmuje ją jako parametr generyczny.
/// </summary>
public sealed class OrdersApplicationMarker;
