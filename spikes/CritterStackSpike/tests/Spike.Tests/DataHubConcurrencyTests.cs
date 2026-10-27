using Shouldly;
using Spike.Contracts;
using Wolverine;

namespace Spike.Tests;

public sealed class DataHubConcurrencyTests(SpikeFixture fixture)
{
    [Fact]
    public async Task State_events_from_both_modules_racing_for_the_same_row_are_retried_until_both_halves_are_saved()
    {
        var orderIds = Enumerable.Range(0, 20).Select(_ => Guid.CreateVersion7()).ToList();

        // Obie połówki wiersza naraz – osobne kolejki (Separated), więc handlery ścigają się o ten sam wiersz.
        async Task PublishBothHalves(IMessageContext context)
        {
            foreach (var orderId in orderIds)
            {
                await context.PublishAsync(new OrderStateChangedIntegrationEvent(orderId, "Klient", 10m, "Paid", Version: 2));
                await context.PublishAsync(new OrderSettlementStateChangedIntegrationEvent(orderId, "Settled", 9m, Version: 2));
            }
        }

        await fixture.Host.TrackWithTimeout().ExecuteAndWaitAsync((Func<IMessageContext, Task>)PublishBothHalves);

        foreach (var orderId in orderIds)
        {
            var row = await fixture.Host.FindOrderPaymentRowAsync(orderId);
            row.ShouldNotBeNull();
            row.OrdersVersion.ShouldBe(2);
            row.SettlementVersion.ShouldBe(2);
        }
    }
}
