using System.Text.Json;

namespace WmsDistributionCenter.Tests;

/// <summary>
/// Locates the shared contract files in /contracts. They are consumed by this test project AND by the browser-demo
/// adapter tests in the frontend, so the two implementations of the rules cannot drift apart.
/// Run with UPDATE_CONTRACTS=1 to regenerate them from the real backend after an intentional change.
/// </summary>
internal static class Contracts
{
    public static bool UpdateMode => Environment.GetEnvironmentVariable("UPDATE_CONTRACTS") == "1";

    public static string PathOf(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(System.IO.Path.Combine(dir.FullName, "contracts")))
            dir = dir.Parent;
        if (dir == null) throw new DirectoryNotFoundException("The /contracts directory was not found above the test binaries.");
        return System.IO.Path.Combine(dir.FullName, "contracts", file);
    }

    /// <summary>Asserts the committed file equals the freshly generated text (or rewrites it in update mode).</summary>
    public static void AssertMatches(string file, string generated)
    {
        var path = PathOf(file);
        if (UpdateMode) File.WriteAllText(path, generated);
        var committed = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.True(committed == generated,
            $"contracts/{file} is out of date with the backend. Review the change, then regenerate with: " +
            "UPDATE_CONTRACTS=1 dotnet test --filter \"FullyQualifiedName~Contract\"");
    }

    public static JsonDocument Read(string file) => JsonDocument.Parse(File.ReadAllText(PathOf(file)));
}
