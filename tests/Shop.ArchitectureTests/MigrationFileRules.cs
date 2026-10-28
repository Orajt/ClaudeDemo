using System.Text.RegularExpressions;

namespace Shop.ArchitectureTests;

/// <summary>Reguły nazewnictwa plików migracji Flyway (.claude/rules/migrations.md): V&lt;NNN&gt;__&lt;opis_snake_case&gt;.sql, numeracja ciągła od V001.</summary>
public static partial class MigrationFileRules
{
    public static IReadOnlyList<string> FindViolations(IReadOnlyCollection<string> fileNames)
    {
        var violations = fileNames
            .Where(fileName => !FileNamePattern().IsMatch(fileName))
            .Select(fileName => $"Zła nazwa pliku migracji: {fileName} (oczekiwano V<NNN>__<opis_snake_case>.sql).")
            .ToList();

        var numbers = fileNames
            .Select(fileName => FileNamePattern().Match(fileName))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["number"].Value))
            .ToList();

        violations.AddRange(DuplicateNumbers(numbers).Select(number => $"Zduplikowany numer migracji: V{number:D3}."));
        violations.AddRange(MissingNumbers(numbers).Select(number => $"Brak migracji V{number:D3} (numeracja musi być ciągła od V001)."));
        return violations;
    }

    private static IEnumerable<int> DuplicateNumbers(IReadOnlyCollection<int> numbers) =>
        numbers.GroupBy(number => number).Where(group => group.Count() > 1).Select(group => group.Key);

    private static IEnumerable<int> MissingNumbers(IReadOnlyCollection<int> numbers) =>
        numbers.Count == 0 ? [] : Enumerable.Range(1, numbers.Max()).Except(numbers);

    [GeneratedRegex(@"^V(?<number>\d{3})__[a-z][a-z0-9]*(_[a-z0-9]+)*\.sql$")]
    private static partial Regex FileNamePattern();
}
