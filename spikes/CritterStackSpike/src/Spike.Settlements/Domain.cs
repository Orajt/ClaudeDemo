namespace Spike.Settlements;

// W spike'u agregaty Settlements są w assembly z referencją do Martena (przypadek domyślny dla generatora).
// Przypadek "Domain bez Martena" sprawdza Spike.Orders.Domain (H5).

public enum SettlementStatus
{
    AwaitingSettlement,
    Settled
}

public sealed record PaymentExpected(Guid OrderId, string TransactionId, decimal Amount);

public sealed record PaymentSettled(Guid OrderId, Guid ReportId, decimal NetAmount);

public sealed record OrderSettlement(
    Guid Id,
    string TransactionId,
    decimal ExpectedAmount,
    SettlementStatus Status,
    decimal? SettledNetAmount)
{
    public long Version { get; init; }

    public static OrderSettlement Create(PaymentExpected expected) =>
        new(expected.OrderId, expected.TransactionId, expected.Amount, SettlementStatus.AwaitingSettlement, SettledNetAmount: null);

    public OrderSettlement Apply(PaymentSettled settled) =>
        this with { Status = SettlementStatus.Settled, SettledNetAmount = settled.NetAmount };
}

public sealed record SettlementReportImported(Guid ReportId, int LineCount, int SettledCount, DateTimeOffset ImportedAt);

public sealed record SettlementReport(Guid Id, int LineCount, int SettledCount)
{
    public long Version { get; init; }

    public static SettlementReport Create(SettlementReportImported imported) =>
        new(imported.ReportId, imported.LineCount, imported.SettledCount);
}
