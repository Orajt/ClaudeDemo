using JasperFx;
using JasperFx.CodeGeneration;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shop.Orders.Infrastructure;
using Shop.Settlements.Infrastructure;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.Persistence;
using Wolverine.Runtime;

namespace Shop.IntegrationTests;

/// <summary>Bazowa konfiguracja Wolverine i Martena z D-012/D-013 – zmiana któregokolwiek ustawienia ma przewrócić test.</summary>
public sealed class WolverineConfigurationTests(ShopFixture fixture)
{
    private WolverineOptions Options => fixture.Host.Services.GetRequiredService<IWolverineRuntime>().Options;

    [Fact]
    [Trait("AC", "AC-9")]
    public void MessageStorage_UsesWolverineSchemaAndIsNotAutoCreated()
    {
        Options.Durability.MessageStorageSchemaName.ShouldBe("wolverine");
        Options.AutoBuildMessageStorageOnStartup.ShouldBe(AutoCreate.None);
    }

    [Fact]
    [Trait("AC", "AC-9")]
    public void Messaging_UsesIdAndDestinationIdentityAndSeparatedHandlers()
    {
        Options.Durability.MessageIdentity.ShouldBe(MessageIdentity.IdAndDestination);
        Options.MultipleHandlerBehavior.ShouldBe(MultipleHandlerBehavior.Separated);
    }

    [Fact]
    [Trait("AC", "AC-9")]
    public void CodeGeneration_UsesDynamicTypeLoadMode()
    {
        Options.CodeGeneration.TypeLoadMode.ShouldBe(TypeLoadMode.Dynamic);
    }

    [Fact]
    [Trait("AC", "AC-9")]
    public void MissingEntity_ThrowsInsteadOfSilentlyDroppingCommand()
    {
        Options.EntityDefaults.OnMissing.ShouldBe(OnMissing.ThrowException);
    }

    [Fact]
    [Trait("AC", "AC-9")]
    public void Policies_IncludeAutoApplyTransactions()
    {
        Options.Policies.Select(policy => policy.GetType().Name).ShouldContain("AutoApplyTransactions");
    }

    [Fact]
    [Trait("AC", "AC-9")]
    public void LocalQueues_AreDurable()
    {
        var runtime = fixture.Host.Services.GetRequiredService<IWolverineRuntime>();

        var defaultQueue = runtime.Endpoints.EndpointFor(new Uri("local://default"));

        defaultQueue.ShouldNotBeNull().Mode.ShouldBe(EndpointMode.Durable);
    }

    [Fact]
    [Trait("AC", "AC-9")]
    public void MartenStores_DoNotAutoCreateSchemaObjects()
    {
        var services = fixture.Host.Services;

        AutoCreateOf(services.GetRequiredService<IOrdersStore>()).ShouldBe(AutoCreate.None);
        AutoCreateOf(services.GetRequiredService<ISettlementsStore>()).ShouldBe(AutoCreate.None);
    }

    // Interfejs IDocumentStore wystawia tylko widok do odczytu opcji – ustawienie AutoCreateSchemaObjects jest na StoreOptions.
    private static AutoCreate AutoCreateOf(IDocumentStore store) => store.Options.ShouldBeOfType<StoreOptions>().AutoCreateSchemaObjects;
}
