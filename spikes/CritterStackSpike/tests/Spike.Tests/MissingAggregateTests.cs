using Shouldly;
using Spike.Orders;
using Wolverine;
using Wolverine.Persistence;

namespace Spike.Tests;

public sealed class MissingAggregateTests(SpikeFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task H9_command_for_missing_aggregate_throws_when_OnMissing_is_ThrowException()
    {
        var bus = fixture.Host.MessageBus();

        await Should.ThrowAsync<RequiredDataMissingException>(() =>
            bus.InvokeAsync(new CancelOrder(Guid.CreateVersion7()), CancellationToken));
    }

    [Fact]
    public void H9_built_in_default_is_Simple404_which_for_message_handlers_silently_stops()
    {
        // Wariant "bez ustawienia" sprawdzony na wygenerowanym kodzie (codegen preview): EntityIsNotNullGuard
        // loguje ostrzeżenie i kończy handler bez wyjątku. Kod jest generowany statycznie, więc drugi host
        // z innym OnMissing użyłby tego samego kodu – dlatego nie ma tu testu na żywym hoście.
        new WolverineOptions().EntityDefaults.OnMissing.ShouldBe(OnMissing.Simple404);
    }

    [Fact]
    public async Task H9_versioned_command_for_missing_aggregate_fails_with_concurrency_exception_before_OnMissing()
    {
        var bus = fixture.Host.MessageBus();

        var exception = await Should.ThrowAsync<Exception>(() =>
            bus.InvokeAsync(new ConfirmPayment(Guid.CreateVersion7(), "TX-MISSING", Version: 1), CancellationToken));

        exception.ShouldBeOfType<JasperFx.ConcurrencyException>();
    }
}
