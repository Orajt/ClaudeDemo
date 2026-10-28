using System.Text.Json;

namespace Shop.IntegrationTests;

public sealed record FlywayModule(string Name, string Schema, string Location);

/// <summary>Zawartość db/flyway-modules.json – jedno źródło prawdy o obrazach i migracjach (D-015), wspólne z AppHost i skryptami.</summary>
public sealed record FlywayModulesConfiguration(
    string PostgresImage,
    string FlywayImage,
    IReadOnlyList<FlywayModule> Modules,
    IReadOnlyList<string> FlywayArguments)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static FlywayModulesConfiguration Load(string repositoryRoot)
    {
        var json = File.ReadAllText(Path.Combine(repositoryRoot, "db", "flyway-modules.json"));
        return JsonSerializer.Deserialize<FlywayModulesConfiguration>(json, JsonOptions)
            ?? throw new InvalidOperationException("Nie udało się odczytać db/flyway-modules.json.");
    }
}
