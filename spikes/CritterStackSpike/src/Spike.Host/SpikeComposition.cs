using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.Hosting;
using Spike.DataHub;
using Spike.Orders;
using Spike.Settlements;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence;
using Wolverine.Postgresql;

namespace Spike.Host;

/// <summary>
/// Kompozycja modułów – to samo używa Program (polecenia db-*) i testy.
/// </summary>
public static class SpikeComposition
{
    public const string MessagingSchema = "wolverine";

    public static HostApplicationBuilder Configure(
        HostApplicationBuilder builder,
        string connectionString,
        Action<WolverineOptions>? overrideWolverine = null)
    {
        builder.Services.AddOrdersModule(connectionString);
        builder.Services.AddSettlementsModule(connectionString);
        builder.Services.AddDataHubModule(connectionString);

        builder.UseWolverine(options =>
        {
            // Wolverine 6 nie ma kompilatora w rdzeniu: kod handlerów jest wygenerowany wcześniej
            // (dotnet run -- codegen write) do Internal/Generated tego assembly.
            // ApplicationAssembly ustawione jawnie – inaczej w testach Wolverine bierze assembly testów.
            options.ApplicationAssembly = typeof(SpikeComposition).Assembly;
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;

            options.Discovery.IncludeAssembly(typeof(IOrdersStore).Assembly);
            options.Discovery.IncludeAssembly(typeof(ISettlementsStore).Assembly);
            options.Discovery.IncludeAssembly(typeof(DataHubDbContext).Assembly);

            // H1: główny magazyn wiadomości bez głównego AddMarten() – wspólne inbox/outbox w schemacie "wolverine".
            // Bez MessageStorageSchemaName każdy store Martena dostaje własne tabele wolverine_* w schemacie modułu.
            options.PersistMessagesWithPostgresql(connectionString, MessagingSchema);
            options.Durability.MessageStorageSchemaName = MessagingSchema;

            // H4: aplikacja nie zmienia schematu.
            options.AutoBuildMessageStorageOnStartup = AutoCreate.None;

            // H3
            options.UseEntityFrameworkCoreTransactions();
            options.Policies.AutoApplyTransactions();
            options.Policies.UseDurableLocalQueues();
            options.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
            options.Durability.MessageIdentity = MessageIdentity.IdAndDestination;

            // H9
            options.EntityDefaults.OnMissing = OnMissing.ThrowException;

            // H2: handlery modułu → store modułu.
            options.UseOrdersModule();
            options.UseSettlementsModule();
            options.UseDataHubModule();

            overrideWolverine?.Invoke(options);
        });

        return builder;
    }
}
