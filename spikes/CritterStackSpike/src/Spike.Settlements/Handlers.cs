using Marten;
using Spike.Contracts;
using Wolverine;
using Wolverine.Marten;

namespace Spike.Settlements;

public sealed record ReportLine(Guid OrderId, string TransactionId, decimal NetAmount);

public sealed record ImportSettlementReport(Guid ReportId, IReadOnlyList<ReportLine> Lines);

public static class OrderPaidHandler
{
    public static (IStartStream, OutgoingMessages) Handle(OrderPaidIntegrationEvent paid)
    {
        var expected = new PaymentExpected(paid.OrderId, paid.TransactionId, paid.Amount);

        var startStream = MartenOps.StartStream<OrderSettlement>(paid.OrderId, expected);
        var messages = new OutgoingMessages
        {
            new OrderSettlementStateChangedIntegrationEvent(
                paid.OrderId, nameof(SettlementStatus.AwaitingSettlement), SettledNetAmount: null, Version: 1)
        };

        return (startStream, messages);
    }
}

public static class ImportSettlementReportHandler
{
    // H8: liczba strumieni jest znana dopiero w czasie działania, więc zamiast parametrów [WriteModel]
    // używamy IDocumentSession i FetchForWriting w pętli. Wolverine (AutoApplyTransactions) robi jeden
    // SaveChangesAsync: zdarzenia we wszystkich strumieniach + nowy strumień raportu + outbox.
    public static async Task<OutgoingMessages> Handle(
        ImportSettlementReport command,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var messages = new OutgoingMessages();
        var settledCount = 0;

        foreach (var line in command.Lines)
        {
            var stream = await session.Events.FetchForWriting<OrderSettlement>(line.OrderId, cancellationToken);
            if (stream.Aggregate is null)
            {
                continue;
            }

            stream.AppendOne(new PaymentSettled(line.OrderId, command.ReportId, line.NetAmount));
            settledCount++;

            messages.Add(new OrderSettlementStateChangedIntegrationEvent(
                line.OrderId, nameof(SettlementStatus.Settled), line.NetAmount, stream.Aggregate.Version + 1));
        }

        var imported = new SettlementReportImported(command.ReportId, command.Lines.Count, settledCount, DateTimeOffset.UtcNow);
        session.Events.StartStream<SettlementReport>(command.ReportId, imported);
        messages.Add(new SettlementReportImportedIntegrationEvent(command.ReportId, command.Lines.Count, settledCount));

        return messages;
    }
}
