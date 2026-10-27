namespace Spike.Orders.Domain;

public enum OrderStatus
{
    Placed,
    Paid,
    Cancelled
}

public sealed record OrderPlaced(Guid OrderId, string CustomerName, decimal TotalAmount, DateTimeOffset PlacedAt);

public sealed record PaymentConfirmed(Guid OrderId, string TransactionId, DateTimeOffset PaidAt);

public sealed record OrderCancelled(Guid OrderId, DateTimeOffset CancelledAt);

/// <summary>
/// Agregat jako niemutowalny record (H5, H7). Marten ustawia <see cref="Version"/> przy FetchForWriting.
/// </summary>
public sealed record Order(Guid Id, string CustomerName, decimal TotalAmount, OrderStatus Status, string? TransactionId)
{
    public long Version { get; init; }

    public static Order Create(OrderPlaced placed) =>
        new(placed.OrderId, placed.CustomerName, placed.TotalAmount, OrderStatus.Placed, TransactionId: null);

    public Order Apply(PaymentConfirmed confirmed) =>
        this with { Status = OrderStatus.Paid, TransactionId = confirmed.TransactionId };

    public Order Apply(OrderCancelled cancelled) =>
        this with { Status = OrderStatus.Cancelled };

    public OrderCancelled Cancel(DateTimeOffset cancelledAt)
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOperationException($"Order {Id} cannot be cancelled.");
        }

        return new OrderCancelled(Id, cancelledAt);
    }

    public PaymentConfirmed ConfirmPayment(string transactionId, DateTimeOffset paidAt)
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOperationException($"Order {Id} is not awaiting payment.");
        }

        return new PaymentConfirmed(Id, transactionId, paidAt);
    }
}
