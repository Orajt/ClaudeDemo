using System.Reflection;
using JasperFx.Events.Aggregation;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Spike.Contracts;
using Spike.Orders;
using Spike.Orders.Domain;
using Wolverine;
using Wolverine.Tracking;

namespace Spike.Tests;

public sealed class CommandFlowTests(SpikeFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task H2_H3_H6_confirm_payment_appends_event_publishes_messages_and_datahub_handles_them()
    {
        var orderId = Guid.CreateVersion7();
        await fixture.Host.InvokeAndWaitAsync(new PlaceOrder(orderId, "Anna Nowak", 120.50m));

        var session = await fixture.Host.InvokeAndWaitAsync(new ConfirmPayment(orderId, "TX-1001", Version: 1));

        // H2: zdarzenie domenowe zapisane w store'ze Orders (schemat spike_orders) przez aggregate handler workflow.
        var eventTypes = await fixture.Db.ListAsync<string>(
            "select type from spike_orders.mt_events where stream_id = $1 order by version", orderId);
        eventTypes.ShouldBe(["order_placed", "payment_confirmed"]);

        // H2: zdarzenia integracyjne wyszły przez outbox.
        session.Sent.SingleMessage<OrderPaidIntegrationEvent>().OrderId.ShouldBe(orderId);
        session.Sent.SingleMessage<OrderStateChangedIntegrationEvent>().OrderStatus.ShouldBe("Paid");

        // H3 + H6: tracked session wróciła dopiero po obsłużeniu kaskady – wiersz DataHub jest już zapisany
        // (handler EF nie woła SaveChangesAsync), także połówka z Settlements (OrderPaid → OrderSettlement → DataHub).
        var row = await fixture.Host.FindOrderPaymentRowAsync(orderId);
        row.ShouldNotBeNull();
        row.CustomerName.ShouldBe("Anna Nowak");
        row.OrderStatus.ShouldBe("Paid");
        row.OrdersVersion.ShouldBe(2);
        row.SettlementStatus.ShouldBe("AwaitingSettlement");
        row.SettlementVersion.ShouldBe(1);
    }

    [Fact]
    public async Task H2_when_event_write_fails_on_commit_no_integration_event_leaves_the_module()
    {
        var orderId = Guid.CreateVersion7();
        await fixture.Host.InvokeAndWaitAsync(new PlaceOrder(orderId, "Original", 10m));

        // Drugi start tego samego strumienia: handler zwraca zdarzenie i wiadomość, ale zapis w SaveChangesAsync się nie udaje.
        Exception? failure = null;
        async Task PlaceDuplicateOrder(IMessageContext context)
        {
            try
            {
                await context.InvokeAsync(new PlaceOrder(orderId, "Duplicate", 99m), CancellationToken);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }

        var session = await fixture.Host.TrackWithTimeout()
            .DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)PlaceDuplicateOrder);

        failure.ShouldBeOfType<Marten.Exceptions.ExistingStreamIdCollisionException>();
        session.Sent.MessagesOf<OrderStateChangedIntegrationEvent>().ShouldBeEmpty();
        (await fixture.Host.FindOrderPaymentRowAsync(orderId))!.CustomerName.ShouldBe("Original");
    }

    [Fact]
    public async Task H2_stale_expected_version_is_rejected_and_nothing_is_published()
    {
        var orderId = Guid.CreateVersion7();
        await fixture.Host.InvokeAndWaitAsync(new PlaceOrder(orderId, "Jan Kowalski", 50m));

        var bus = fixture.Host.MessageBus();
        await Should.ThrowAsync<JasperFx.ConcurrencyException>(() =>
            bus.InvokeAsync(new ConfirmPayment(orderId, "TX-STALE", Version: 7), CancellationToken));

        var version = await fixture.Db.ScalarAsync<long>("select version from spike_orders.mt_streams where id = $1", orderId);
        version.ShouldBe(1);
    }

    [Fact]
    public async Task H5_aggregate_from_library_without_marten_is_evolved_by_source_generated_dispatcher()
    {
        var domainAssembly = typeof(Order).Assembly;

        var referencedAssemblies = domainAssembly.GetReferencedAssemblies().Select(x => x.Name!).ToList();
        referencedAssemblies.ShouldNotContain(name => name.StartsWith("Marten") || name.StartsWith("Wolverine") || name.StartsWith("Npgsql"));
        domainAssembly.GetCustomAttributes<GeneratedEvolverAttribute>().ShouldNotBeEmpty();

        var orderId = await fixture.Host.PlaceAndPayOrderAsync("Ewa Zielińska", 75m);

        // Create i Apply zostały wywołane – stan agregatu odbudowany ze zdarzeń.
        var store = fixture.Host.Services.GetRequiredService<IOrdersStore>();
        await using var session = store.LightweightSession();
        var order = await session.Events.FetchLatest<Order>(orderId, CancellationToken);
        order.ShouldNotBeNull();
        order.CustomerName.ShouldBe("Ewa Zielińska");
        order.Status.ShouldBe(OrderStatus.Paid);
        order.TransactionId.ShouldBe($"TX-{orderId:N}");
    }

    [Fact]
    public async Task H7_record_aggregate_version_plus_appended_events_equals_stream_version()
    {
        var orderId = Guid.CreateVersion7();
        await fixture.Host.InvokeAndWaitAsync(new PlaceOrder(orderId, "Piotr Wiśniewski", 30m));

        var store = fixture.Host.Services.GetRequiredService<IOrdersStore>();
        await using (var session = store.LightweightSession())
        {
            var stream = await session.Events.FetchForWriting<Order>(orderId, CancellationToken);
            stream.Aggregate!.Version.ShouldBe(1);
        }

        // Handler liczy wersję w zdarzeniu stanu jako order.Version (1) + liczba dopisanych zdarzeń (1).
        var tracked = await fixture.Host.InvokeAndWaitAsync(new ConfirmPayment(orderId, "TX-H7", Version: 1));

        var versionInMtStreams = await fixture.Db.ScalarAsync<long>(
            "select version from spike_orders.mt_streams where id = $1", orderId);
        versionInMtStreams.ShouldBe(2);
        tracked.Sent.SingleMessage<OrderStateChangedIntegrationEvent>().Version.ShouldBe(versionInMtStreams);
    }
}
