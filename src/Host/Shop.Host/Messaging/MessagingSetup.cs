using JasperFx;
using JasperFx.CodeGeneration;
using Shop.DataHub.Infrastructure;
using Shop.Orders.Infrastructure;
using Shop.Settlements.Infrastructure;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Persistence;
using Wolverine.Postgresql;

namespace Shop.Host.Messaging;

/// <summary>Bazowa konfiguracja Wolverine wspólna dla wszystkich modułów (D-002, D-012, D-013).</summary>
public static class MessagingSetup
{
    public const string MessagingSchema = "wolverine";

    private const string ConnectionStringName = "shop";

    public static WebApplicationBuilder AddShopMessaging(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Brak connection stringu '{ConnectionStringName}'.");

        builder.UseWolverine(options =>
        {
            // Jawnie – bez tego Wolverine w testach przyjmuje assembly testów (D-012).
            options.ApplicationAssembly = typeof(Program).Assembly;

            // Kod handlerów generowany w czasie działania (D-013).
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
            options.UseRuntimeCompilation();

            // Wspólne inbox/outbox w schemacie "wolverine" bez głównego AddMarten(). Bez MessageStorageSchemaName
            // każdy store Martena dostałby własne tabele wolverine_* w schemacie modułu (D-012).
            options.PersistMessagesWithPostgresql(connectionString, MessagingSchema);
            options.Durability.MessageStorageSchemaName = MessagingSchema;

            // Schemat zmienia wyłącznie Flyway (D-004).
            options.AutoBuildMessageStorageOnStartup = AutoCreate.None;

            options.UseEntityFrameworkCoreTransactions();
            options.Policies.AutoApplyTransactions();
            options.Policies.UseDurableLocalQueues();
            options.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
            options.Durability.MessageIdentity = MessageIdentity.IdAndDestination;

            // Brak agregatu dla [WriteModel] → wyjątek (404), a nie ciche porzucenie komendy (.claude/rules/application.md).
            options.EntityDefaults.OnMissing = OnMissing.ThrowException;

            // Walidatory FluentValidation jako middleware – błąd kształtu danych → 400 (D-010).
            options.UseFluentValidation();

            options.Include<OrdersWolverineExtension>();
            options.Include<SettlementsWolverineExtension>();
            options.Include<DataHubWolverineExtension>();
        });

        return builder;
    }
}
