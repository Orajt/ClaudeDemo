namespace Shop.AppHost;

public static class FlywayExtensions
{
    private const string MigrationsPathInContainer = "/flyway/sql";
    private const int ConnectRetries = 30;

    /// <summary>
    /// Dodaje kontener Flyway migrujący jeden moduł. Zwykły kontener zamiast pakietu CommunityToolkit.Aspire.Hosting.Flyway,
    /// bo pakiet nie pozwala przekazać <c>-schemas</c>, <c>-locations</c> ani przypiętego obrazu z db/flyway-modules.json.
    /// </summary>
    public static IResourceBuilder<ContainerResource> AddFlywayMigration(
        this IDistributedApplicationBuilder builder,
        FlywayMigrationRequest request)
    {
        var image = ImageReference.Parse(request.Configuration.FlywayImage);
        var module = request.Module;
        var database = request.Database;

        return builder.AddContainer($"flyway-{module.Name}", image.Repository, image.Tag)
            .WithBindMount(Path.Combine(request.RepositoryRoot, module.Location), MigrationsPathInContainer, isReadOnly: true)
            .WithEnvironment("FLYWAY_URL", JdbcUrl(database))
            .WithEnvironment("FLYWAY_USER", database.Resource.Parent.UserNameReference)
            .WithEnvironment("FLYWAY_PASSWORD", database.Resource.Parent.PasswordParameter)
            .WithArgs(FlywayArguments(module, request.Configuration))
            .WaitFor(database);
    }

    // Kontener łączy się z Postgresem po sieci kontenerów – Aspire podstawia alias i port wewnętrzny.
    private static ReferenceExpression JdbcUrl(IResourceBuilder<PostgresDatabaseResource> database)
    {
        var endpoint = database.Resource.Parent.PrimaryEndpoint;
        return ReferenceExpression.Create(
            $"jdbc:postgresql://{endpoint.Property(EndpointProperty.Host)}:{endpoint.Property(EndpointProperty.Port)}/{database.Resource.DatabaseName}");
    }

    private static string[] FlywayArguments(FlywayModule module, FlywayConfiguration configuration) =>
    [
        "migrate",
        $"-schemas={module.Schema}",
        $"-locations=filesystem:{MigrationsPathInContainer}",
        $"-connectRetries={ConnectRetries}",
        .. configuration.FlywayArguments
    ];
}

/// <summary>Parametry dodania migracji modułu (rekord, żeby nie przekraczać 3–4 parametrów metody).</summary>
public sealed record FlywayMigrationRequest(
    FlywayModule Module,
    FlywayConfiguration Configuration,
    IResourceBuilder<PostgresDatabaseResource> Database,
    string RepositoryRoot);
