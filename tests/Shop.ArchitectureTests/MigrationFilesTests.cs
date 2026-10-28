using System.Text.Json;

namespace Shop.ArchitectureTests;

public sealed class MigrationFilesTests
{
    public static TheoryData<string> ModuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var module in ReadModules().Keys)
            data.Add(module);

        return data;
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    [Trait("AC", "AC-17")]
    [Trait("AC", "AC-16")]
    public void MigrationFiles_FollowNamingAndNumberingRules(string module)
    {
        var directory = Path.Combine(RepositoryLocator.Root, ReadModules()[module]);
        var fileNames = Directory.EnumerateFiles(directory, "*.sql").Select(Path.GetFileName).OfType<string>().ToList();

        var violations = MigrationFileRules.FindViolations(fileNames);

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData("V001__a.sql,V003__b.sql", "Brak migracji V002")]
    [InlineData("V1__a.sql", "Zła nazwa pliku migracji: V1__a.sql")]
    [InlineData("V001__a.sql,V002__b.sql,V002__c.sql", "Zduplikowany numer migracji: V002")]
    [InlineData("V001__CamelCase.sql", "Zła nazwa pliku migracji")]
    [Trait("AC", "AC-17")]
    public void BrokenMigrationSet_IsReported(string commaSeparatedFileNames, string expectedViolation)
    {
        var violations = MigrationFileRules.FindViolations(commaSeparatedFileNames.Split(','));

        violations.ShouldContain(violation => violation.Contains(expectedViolation));
    }

    [Fact]
    [Trait("AC", "AC-17")]
    public void ValidMigrationSet_HasNoViolations()
    {
        MigrationFileRules.FindViolations(["V001__wolverine_message_storage.sql", "V002__add_index.sql"]).ShouldBeEmpty();
    }

    // Moduły i katalogi z jedynego źródła prawdy: db/flyway-modules.json.
    private static Dictionary<string, string> ReadModules()
    {
        var json = File.ReadAllText(Path.Combine(RepositoryLocator.Root, "db", "flyway-modules.json"));
        return JsonDocument.Parse(json).RootElement.GetProperty("modules").EnumerateArray()
            .ToDictionary(
                module => module.GetProperty("name").GetString()!,
                module => module.GetProperty("location").GetString()!);
    }
}
