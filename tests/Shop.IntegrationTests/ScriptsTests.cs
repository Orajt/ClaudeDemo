using System.Diagnostics;

namespace Shop.IntegrationTests;

/// <summary>Skrypty pomocnicze: tryb ścisły basha, polskie komunikaty i --help kończące się kodem 0.</summary>
public sealed class ScriptsTests
{
    private const string StrictMode = "set -euo pipefail";

    public static TheoryData<string> ScriptPaths() => new()
    {
        "scripts/db/migrate-local.sh",
        "scripts/db/reset-local.sh",
        "scripts/db/new-migration.sh",
        "scripts/dev/get-token.sh"
    };

    [Theory]
    [MemberData(nameof(ScriptPaths))]
    [Trait("AC", "AC-13")]
    public void Script_UsesStrictBashMode(string relativePath)
    {
        var content = File.ReadAllText(Path.Combine(RepositoryLocator.Root, relativePath));

        content.ShouldContain(StrictMode);
    }

    [Theory]
    [MemberData(nameof(ScriptPaths))]
    [Trait("AC", "AC-13")]
    public async Task Script_HelpExitsZeroWithPolishUsage(string relativePath)
    {
        var result = await RunAsync(relativePath, "--help");

        result.ExitCode.ShouldBe(0, $"stderr: {result.Error}");
        result.Output.ShouldContain("Użycie:", customMessage: $"stderr: {result.Error}");
    }

    private sealed record ScriptResult(int ExitCode, string Output, string Error);

    private static async Task<ScriptResult> RunAsync(string relativePath, string argument)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var startInfo = new ProcessStartInfo("bash")
        {
            WorkingDirectory = RepositoryLocator.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { relativePath, argument }
        };

        using var process = Process.Start(startInfo)!;
        // Oba strumienie czytane równolegle – inaczej pełny bufor stderr zawiesiłby proces.
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ScriptResult(process.ExitCode, await outputTask, await errorTask);
    }
}
