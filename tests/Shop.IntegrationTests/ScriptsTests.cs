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
        var (exitCode, output) = await RunAsync(relativePath, "--help");

        exitCode.ShouldBe(0);
        output.ShouldContain("Użycie:");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string relativePath, string argument)
    {
        var startInfo = new ProcessStartInfo("bash")
        {
            WorkingDirectory = RepositoryLocator.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { relativePath, argument }
        };

        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, output);
    }
}
