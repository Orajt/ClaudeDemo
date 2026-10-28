using System.Text.Json;

namespace Shop.AppHost;

/// <summary>Moduł migracji z <c>db/flyway-modules.json</c>: schemat i katalog z plikami SQL (ścieżka względem korzenia repozytorium).</summary>
public sealed record FlywayModule(string Name, string Schema, string Location);

/// <summary>
/// Jedno źródło prawdy o migracjach i obrazach (D-015): AppHost, testy i skrypty czytają ten sam plik <c>db/flyway-modules.json</c>.
/// </summary>
public sealed record FlywayConfiguration(
    string PostgresImage,
    string FlywayImage,
    IReadOnlyList<FlywayModule> Modules,
    IReadOnlyList<string> FlywayArguments)
{
    private const string ConfigurationRelativePath = "db/flyway-modules.json";

    public static FlywayConfiguration Load(string repositoryRoot)
    {
        var json = File.ReadAllText(Path.Combine(repositoryRoot, ConfigurationRelativePath));
        var document = JsonDocument.Parse(json).RootElement;

        return new FlywayConfiguration(
            document.GetProperty("postgresImage").GetString()!,
            document.GetProperty("flywayImage").GetString()!,
            document.GetProperty("modules").EnumerateArray().Select(ReadModule).ToList(),
            document.GetProperty("flywayArguments").EnumerateArray().Select(argument => argument.GetString()!).ToList());
    }

    private static FlywayModule ReadModule(JsonElement module) => new(
        module.GetProperty("name").GetString()!,
        module.GetProperty("schema").GetString()!,
        module.GetProperty("location").GetString()!);
}
