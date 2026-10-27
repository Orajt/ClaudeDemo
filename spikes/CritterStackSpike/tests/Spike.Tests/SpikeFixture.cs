using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Spike.Host;
using Testcontainers.PostgreSql;
using Wolverine;

[assembly: AssemblyFixture(typeof(Spike.Tests.SpikeFixture))]

namespace Spike.Tests;

/// <summary>
/// Baza powstaje wyłącznie z plików SQL przez Flyway (H4), host startuje z AutoCreate.None.
/// </summary>
public sealed class SpikeFixture : IAsyncLifetime
{
    private const string PostgresImage = "postgres:18.6-alpine";
    private const string FlywayImage = "flyway/flyway:13.11.1-alpine";
    private const string PostgresAlias = "postgres";
    private const string Database = "spike";
    private const string User = "postgres";
    private const string Password = "postgres";

    private readonly INetwork _network = new NetworkBuilder().Build();
    private PostgreSqlContainer? _postgres;
    private IHost? _host;
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString => _postgres!.GetConnectionString();
    public IHost Host => _host!;
    public NpgsqlDataSource Db => _dataSource!;

    public async ValueTask InitializeAsync()
    {
        await _network.CreateAsync();

        _postgres = new PostgreSqlBuilder(PostgresImage)
            .WithNetwork(_network)
            .WithNetworkAliases(PostgresAlias)
            .WithDatabase(Database)
            .WithUsername(User)
            .WithPassword(Password)
            .Build();
        await _postgres.StartAsync();

        await ApplyFlywayMigrationsAsync();

        _dataSource = NpgsqlDataSource.Create(ConnectionString);
        _host = await StartHostAsync();
    }

    /// <summary>Host z tą samą kompozycją co Spike.Host.</summary>
    private async Task<IHost> StartHostAsync()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        SpikeComposition.Configure(builder, ConnectionString, options =>
        {
            // Tryb zalecany dla testów: bez wyboru lidera między węzłami.
            options.Durability.Mode = DurabilityMode.Solo;
        });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        await _network.DisposeAsync();
    }

    private async Task ApplyFlywayMigrationsAsync()
    {
        var spikeRoot = FindSpikeRoot();
        var config = FlywayModulesConfig.Load(Path.Combine(spikeRoot, "sql", "flyway-modules.json"));

        foreach (var module in config.Modules)
        {
            string[] command =
            [
                $"-url=jdbc:postgresql://{PostgresAlias}:5432/{Database}",
                $"-user={User}",
                $"-password={Password}",
                $"-schemas={module.Schema}",
                "-locations=filesystem:/flyway/sql",
                .. config.FlywayArguments,
                "migrate"
            ];

            await using var flyway = new ContainerBuilder(FlywayImage)
                .WithNetwork(_network)
                .WithResourceMapping(new DirectoryInfo(Path.Combine(spikeRoot, module.Location)), "/flyway/sql/")
                .WithCommand(command)
                .Build();

            await flyway.StartAsync();
            var exitCode = await flyway.GetExitCodeAsync();
            if (exitCode != 0)
            {
                var (stdout, stderr) = await flyway.GetLogsAsync();
                throw new InvalidOperationException(
                    $"Flyway failed for module '{module.Name}' (exit code {exitCode}).\n{stdout}\n{stderr}");
            }
        }
    }

    private static string FindSpikeRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CritterStackSpike.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("CritterStackSpike.slnx not found.");
    }

    private sealed record FlywayModule(string Name, string Schema, string Location);

    private sealed record FlywayModulesConfig(IReadOnlyList<FlywayModule> Modules, IReadOnlyList<string> FlywayArguments)
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public static FlywayModulesConfig Load(string path) =>
            JsonSerializer.Deserialize<FlywayModulesConfig>(File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Cannot read {path}.");
    }
}
