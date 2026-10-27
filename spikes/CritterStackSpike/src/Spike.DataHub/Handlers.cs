using Spike.Contracts;

namespace Spike.DataHub;

// H3: handler przyjmuje konkretny DataHubDbContext. SaveChangesAsync woła transakcyjny middleware Wolverine
// (AutoApplyTransactions + UseEntityFrameworkCoreTransactions) – w handlerze go nie ma.

public static class OrderStateChangedHandler
{
    public static async Task Handle(
        OrderStateChangedIntegrationEvent changed,
        DataHubDbContext db,
        CancellationToken cancellationToken)
    {
        var row = await OrderPaymentRows.FindOrAddAsync(db, changed.OrderId, cancellationToken);
        if (changed.Version <= row.OrdersVersion)
        {
            return;
        }

        row.CustomerName = changed.CustomerName;
        row.TotalAmount = changed.TotalAmount;
        row.OrderStatus = changed.OrderStatus;
        row.OrdersVersion = changed.Version;
    }
}

public static class OrderSettlementStateChangedHandler
{
    public static async Task Handle(
        OrderSettlementStateChangedIntegrationEvent changed,
        DataHubDbContext db,
        CancellationToken cancellationToken)
    {
        var row = await OrderPaymentRows.FindOrAddAsync(db, changed.OrderId, cancellationToken);
        if (changed.Version <= row.SettlementVersion)
        {
            return;
        }

        row.SettlementStatus = changed.SettlementStatus;
        row.SettledNetAmount = changed.SettledNetAmount;
        row.SettlementVersion = changed.Version;
    }
}

internal static class OrderPaymentRows
{
    public static async Task<OrderPaymentRow> FindOrAddAsync(DataHubDbContext db, Guid orderId, CancellationToken cancellationToken)
    {
        var row = await db.OrderPayments.FindAsync([orderId], cancellationToken);
        if (row is not null)
        {
            return row;
        }

        row = new OrderPaymentRow { OrderId = orderId };
        db.OrderPayments.Add(row);
        return row;
    }
}
