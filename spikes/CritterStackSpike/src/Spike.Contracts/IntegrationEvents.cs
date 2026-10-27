namespace Spike.Contracts;

// Fakt biznesowy: Orders → Settlements.
public sealed record OrderPaidIntegrationEvent(Guid OrderId, string TransactionId, decimal Amount, DateTimeOffset PaidAt);

// Zmiana stanu: Orders → DataHub (H2, H3, H6).
public sealed record OrderStateChangedIntegrationEvent(
    Guid OrderId,
    string CustomerName,
    decimal TotalAmount,
    string OrderStatus,
    long Version);

// Zmiana stanu: Settlements → DataHub (H8).
public sealed record OrderSettlementStateChangedIntegrationEvent(
    Guid OrderId,
    string SettlementStatus,
    decimal? SettledNetAmount,
    long Version);

// Fakt biznesowy: nowy raport (H8).
public sealed record SettlementReportImportedIntegrationEvent(Guid ReportId, int LineCount, int SettledCount);
