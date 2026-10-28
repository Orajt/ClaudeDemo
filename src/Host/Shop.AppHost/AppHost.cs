using Shop.AppHost;

const int PostgresHostPort = 55432;
const int KeycloakHostPort = 8080;
const string DatabaseName = "shop";

// Hasło tylko do lokalnego developmentu; musi zgadzać się z Shop.Host/appsettings.Development.json (komendy db-* spoza Aspire).
const string DevelopmentPostgresPassword = "shop-dev-password";

var builder = DistributedApplication.CreateBuilder(args);

var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", ".."));
var flyway = FlywayConfiguration.Load(repositoryRoot);

var postgres = AddPostgres(builder, flyway);
var database = postgres.AddDatabase(DatabaseName);
var migrations = AddMigrations(builder, flyway, database, repositoryRoot);
var keycloak = AddKeycloak(builder);

var host = builder.AddProject<Projects.Shop_Host>("host")
    .WithReference(database)
    .WithReference(keycloak)
    .WaitFor(keycloak);

foreach (var migration in migrations)
    host.WaitForCompletion(migration);

builder.Build().Run();

static IResourceBuilder<PostgresServerResource> AddPostgres(IDistributedApplicationBuilder builder, FlywayConfiguration flyway)
{
    var image = ImageReference.Parse(flyway.PostgresImage);
    var password = builder.AddParameter("postgres-password", DevelopmentPostgresPassword, secret: true);

    return builder.AddPostgres("postgres", password: password, port: PostgresHostPort)
        .WithImage(image.Repository)
        .WithImageTag(image.Tag)
        .WithDataVolume();
}

// Moduły migrują po kolei, w kolejności z pliku – każdy czeka na zakończenie poprzedniego.
static IReadOnlyList<IResourceBuilder<ContainerResource>> AddMigrations(
    IDistributedApplicationBuilder builder,
    FlywayConfiguration flyway,
    IResourceBuilder<PostgresDatabaseResource> database,
    string repositoryRoot)
{
    var migrations = new List<IResourceBuilder<ContainerResource>>();

    foreach (var module in flyway.Modules)
    {
        var migration = builder.AddFlywayMigration(new FlywayMigrationRequest(module, flyway, database, repositoryRoot));
        if (migrations.Count > 0)
            migration.WaitForCompletion(migrations[^1]);

        migrations.Add(migration);
    }

    return migrations;
}

// WithoutHttpsCertificate jest API eksperymentalnym (ASPIRECERTIFICATES001) – wyciszone tylko dla tego wywołania.
#pragma warning disable ASPIRECERTIFICATES001
static IResourceBuilder<KeycloakResource> AddKeycloak(IDistributedApplicationBuilder builder) =>
    builder.AddKeycloak("keycloak", KeycloakHostPort)
        // Keycloak lokalnie po zwykłym HTTP: certyfikat deweloperski Aspire nie jest zaufany na Linuksie, a skrypty i Host
        // (RequireHttpsMetadata=false w Development) oczekują http://localhost:8080.
        .WithoutHttpsCertificate()
        .WithRealmImport("./Realms");
#pragma warning restore ASPIRECERTIFICATES001
