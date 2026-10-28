using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace Shop.IntegrationTests;

/// <summary>Miejsce docelowe migracji: alias Postgresa w sieci kontenerów i dane logowania.</summary>
public sealed record FlywayTarget(string PostgresAlias, string DatabaseName, string UserName, string Password);

/// <summary>Uruchamia Flyway w kontenerach, po jednym na moduł z db/flyway-modules.json (w kolejności z pliku).</summary>
public sealed class FlywayRunner(INetwork network, FlywayModulesConfiguration configuration, FlywayTarget target)
{
    private const string MigrationsPathInContainer = "/flyway/sql/";

    public async Task MigrateAllModulesAsync()
    {
        foreach (var module in configuration.Modules)
            await MigrateModuleAsync(module);
    }

    private async Task MigrateModuleAsync(FlywayModule module)
    {
        // Pliki kopiowane do kontenera (a nie montowane) – działa też tam, gdzie Docker nie widzi katalogów hosta.
        await using var flyway = new ContainerBuilder(configuration.FlywayImage)
            .WithNetwork(network)
            .WithResourceMapping(new DirectoryInfo(Path.Combine(RepositoryLocator.Root, module.Location)), MigrationsPathInContainer)
            .WithCommand(FlywayCommand(module))
            .Build();

        await flyway.StartAsync();

        var exitCode = await flyway.GetExitCodeAsync();
        if (exitCode == 0)
            return;

        var (stdout, stderr) = await flyway.GetLogsAsync();
        throw new InvalidOperationException($"Flyway zakończył się błędem dla modułu '{module.Name}' (kod {exitCode}).\n{stdout}\n{stderr}");
    }

    private string[] FlywayCommand(FlywayModule module) =>
    [
        "migrate",
        $"-url=jdbc:postgresql://{target.PostgresAlias}:5432/{target.DatabaseName}",
        $"-user={target.UserName}",
        $"-password={target.Password}",
        $"-schemas={module.Schema}",
        $"-locations=filesystem:{MigrationsPathInContainer}",
        .. configuration.FlywayArguments
    ];
}
