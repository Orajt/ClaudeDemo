using Spike.Contracts;
using Spike.Orders.Domain;
using Wolverine;
using Wolverine.Marten;
using Wolverine.Persistence.EventSourcing;

namespace Spike.Orders;

public sealed record PlaceOrder(Guid OrderId, string CustomerName, decimal TotalAmount);

/// <summary><see cref="Version"/> = oczekiwana wersja strumienia (optymistyczna współbieżność).</summary>
public sealed record ConfirmPayment(Guid OrderId, string TransactionId, long Version);

/// <summary>Komenda bez wersji – na niej widać zachowanie OnMissing (H9).</summary>
public sealed record CancelOrder(Guid OrderId);

public static class PlaceOrderHandler
{
    public static (IStartStream, OutgoingMessages) Handle(PlaceOrder command)
    {
        var placed = new OrderPlaced(command.OrderId, command.CustomerName, command.TotalAmount, DateTimeOffset.UtcNow);

        var startStream = MartenOps.StartStream<Order>(command.OrderId, placed);
        var messages = new OutgoingMessages
        {
            new OrderStateChangedIntegrationEvent(
                command.OrderId, command.CustomerName, command.TotalAmount, nameof(OrderStatus.Placed), Version: 1)
        };

        return (startStream, messages);
    }
}

public static class ConfirmPaymentHandler
{
    // H2: aggregate handler workflow – Wolverine wczytuje Order przez FetchForWriting (z oczekiwaną wersją
    // z command.Version), dopisuje zwrócone zdarzenia i zapisuje OutgoingMessages w outboxie w tej samej transakcji.
    public static (Events, OutgoingMessages) Handle(ConfirmPayment command, [WriteModel] Order order)
    {
        var confirmed = order.ConfirmPayment(command.TransactionId, DateTimeOffset.UtcNow);
        var events = new Events { confirmed };

        // D-005: wersja po zapisie = wersja przed + liczba dopisanych zdarzeń (H7).
        var versionAfterAppend = order.Version + events.Count;
        var messages = new OutgoingMessages
        {
            new OrderPaidIntegrationEvent(order.Id, command.TransactionId, order.TotalAmount, confirmed.PaidAt),
            new OrderStateChangedIntegrationEvent(
                order.Id, order.CustomerName, order.TotalAmount, nameof(OrderStatus.Paid), versionAfterAppend)
        };

        return (events, messages);
    }
}

public static class CancelOrderHandler
{
    // [WriteModel] na parametrze generuje sprawdzenie, czy agregat istnieje, sterowane przez
    // EntityDefaults.OnMissing (H9). [DeciderFunction] na metodzie takiego sprawdzenia nie generuje.
    public static (Events, OutgoingMessages) Handle(CancelOrder command, [WriteModel] Order order)
    {
        var cancelled = order.Cancel(DateTimeOffset.UtcNow);
        var events = new Events { cancelled };

        var messages = new OutgoingMessages
        {
            new OrderStateChangedIntegrationEvent(
                order.Id, order.CustomerName, order.TotalAmount, nameof(OrderStatus.Cancelled), order.Version + events.Count)
        };

        return (events, messages);
    }
}
