using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Spike.Contracts;
using Spike.Settlements;
using Wolverine.Tracking;

namespace Spike.Tests;

public sealed class MultiStreamTests(SpikeFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task H8_import_appends_to_many_streams_starts_report_stream_and_publishes_in_one_transaction()
    {
        Guid[] orderIds =
        [
            await fixture.Host.PlaceAndPayOrderAsync("Klient A", 100m),
            await fixture.Host.PlaceAndPayOrderAsync("Klient B", 200m),
            await fixture.Host.PlaceAndPayOrderAsync("Klient C", 300m)
        ];
        var reportId = Guid.CreateVersion7();
        var lines = orderIds
            .Select((orderId, index) => new ReportLine(orderId, $"TX-{orderId:N}", 95m + index))
            .Append(new ReportLine(Guid.CreateVersion7(), "TX-UNKNOWN", 1m))
            .ToList();

        var session = await fixture.Host.InvokeAndWaitAsync(new ImportSettlementReport(reportId, lines));

        // Każdy istniejący strumień OrderSettlement dostał jedno zdarzenie, nowy strumień raportu wystartował.
        foreach (var orderId in orderIds)
        {
            var version = await fixture.Db.ScalarAsync<long>(
                "select version from spike_settlements.mt_streams where id = $1", orderId);
            version.ShouldBe(2);
        }

        var reportVersion = await fixture.Db.ScalarAsync<long>(
            "select version from spike_settlements.mt_streams where id = $1", reportId);
        reportVersion.ShouldBe(1);

        // Jedna transakcja: wszystkie nowe wiersze mt_events mają ten sam identyfikator transakcji (xmin).
        var transactionIds = await fixture.Db.ListAsync<string>(
            """
            select distinct xmin::text from spike_settlements.mt_events
            where (stream_id = any($1) and version = 2) or stream_id = $2
            """,
            orderIds, reportId);
        transactionIds.Count.ShouldBe(1);

        session.Sent.MessagesOf<OrderSettlementStateChangedIntegrationEvent>().Count().ShouldBe(3);
        // Fakt o raporcie nie ma w spike'u odbiorcy – Wolverine notuje go jako NoRoutes (wiadomość jest porzucana).
        session.NoRoutes.SingleMessage<SettlementReportImportedIntegrationEvent>().SettledCount.ShouldBe(3);

        foreach (var orderId in orderIds)
        {
            (await fixture.Host.FindOrderPaymentRowAsync(orderId))!.SettlementStatus.ShouldBe("Settled");
        }
    }

    [Fact]
    public async Task H8_concurrent_write_to_one_of_the_streams_rolls_back_the_whole_unit_of_work()
    {
        var firstOrderId = await fixture.Host.PlaceAndPayOrderAsync("Klient D", 10m);
        var secondOrderId = await fixture.Host.PlaceAndPayOrderAsync("Klient E", 20m);
        var reportId = Guid.CreateVersion7();
        var store = fixture.Host.Services.GetRequiredService<ISettlementsStore>();

        // Ten sam wzorzec co ImportSettlementReportHandler: FetchForWriting w pętli + nowy strumień + jeden SaveChangesAsync.
        await using var import = store.LightweightSession();
        var first = await import.Events.FetchForWriting<OrderSettlement>(firstOrderId, CancellationToken);
        var second = await import.Events.FetchForWriting<OrderSettlement>(secondOrderId, CancellationToken);

        await using (var concurrentWriter = store.LightweightSession())
        {
            concurrentWriter.Events.Append(secondOrderId, new PaymentSettled(secondOrderId, Guid.CreateVersion7(), 19m));
            await concurrentWriter.SaveChangesAsync(CancellationToken);
        }

        first.AppendOne(new PaymentSettled(firstOrderId, reportId, 9m));
        second.AppendOne(new PaymentSettled(secondOrderId, reportId, 19m));
        import.Events.StartStream<SettlementReport>(reportId, new SettlementReportImported(reportId, 2, 2, DateTimeOffset.UtcNow));

        var exception = await Should.ThrowAsync<Exception>(() => import.SaveChangesAsync(CancellationToken));
        exception.ShouldBeOfType<JasperFx.Events.EventStreamUnexpectedMaxEventIdException>()
            .ShouldBeAssignableTo<JasperFx.ConcurrencyException>();

        (await fixture.Db.ScalarAsync<long>("select version from spike_settlements.mt_streams where id = $1", firstOrderId))
            .ShouldBe(1);
        (await fixture.Db.ScalarAsync<long>("select count(*) from spike_settlements.mt_streams where id = $1", reportId))
            .ShouldBe(0);
    }
}
