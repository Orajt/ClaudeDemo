using Alba;
using Alba.Security;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;

[assembly: AssemblyFixture(typeof(Shop.IntegrationTests.ShopFixture))]

namespace Shop.IntegrationTests;

/// <summary>
/// Jeden kontener Postgres na przebieg testów. Schemat powstaje wyłącznie z migracji Flyway (db/flyway-modules.json) –
/// tych samych plików co w produkcji; aplikacja startuje z AutoCreate.None, więc zapomniana migracja przewraca start.
/// </summary>
public sealed class ShopFixture : IAsyncLifetime
{
    private const string PostgresAlias = "postgres";
    private const string DatabaseName = "shop";
    private const string UserName = "postgres";
    private const string Password = "test-password";
    private const string ConnectionStringSetting = "ConnectionStrings:shop";

    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly FlywayModulesConfiguration _flyway = FlywayModulesConfiguration.Load(RepositoryLocator.Root);
    private PostgreSqlContainer? _postgres;
    private IAlbaHost? _host;

    /// <summary>Host aplikacji ze stubem uwierzytelniania JWT – role dodajemy w teście (<c>WithClaim</c>).</summary>
    public IAlbaHost Host => _host ?? throw new InvalidOperationException("Fixture nie został zainicjalizowany.");

    public string ConnectionString => _postgres?.GetConnectionString() ?? throw new InvalidOperationException("Fixture nie został zainicjalizowany.");

    public async ValueTask InitializeAsync()
    {
        await _network.CreateAsync();
        await StartPostgresAsync();
        await new FlywayRunner(_network, _flyway, new FlywayTarget(PostgresAlias, DatabaseName, UserName, Password)).MigrateAllModulesAsync();
        _host = await StartApplicationAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
            await _host.DisposeAsync();

        if (_postgres is not null)
            await _postgres.DisposeAsync();

        await _network.DisposeAsync();
    }

    private async Task StartPostgresAsync()
    {
        _postgres = new PostgreSqlBuilder(_flyway.PostgresImage)
            .WithNetwork(_network)
            .WithNetworkAliases(PostgresAlias)
            .WithDatabase(DatabaseName)
            .WithUsername(UserName)
            .WithPassword(Password)
            .Build();

        await _postgres.StartAsync();
    }

    private Task<IAlbaHost> StartApplicationAsync() =>
        AlbaHost.For<Program>(ConfigureApplication, new JwtSecurityStub());

    private void ConfigureApplication(IWebHostBuilder builder)
    {
        // Connection string podmieniamy przez ustawienie hosta – Program czyta go przy budowaniu WebApplicationBuilder.
        builder.UseSetting(ConnectionStringSetting, ConnectionString);

        // Zalecany tryb dla testów: bez wyboru lidera między węzłami.
        builder.ConfigureServices(services => services.RunWolverineInSoloMode());
    }
}
