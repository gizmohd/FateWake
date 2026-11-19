using System.Text.RegularExpressions;
using System.Text.Json;
using Fatewake.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder();
builder.AddObservability();
using var host = builder.Build();
await host.StartAsync();
using var operation = OperationTelemetry.Start("tool.validate_assets");
var logger = host.Services.GetRequiredService<ILogger<Program>>();

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : FindRoot();
var source = Path.Combine(root, "src");
var promptSeed = Path.Combine(root, "art", "prompt-seeds", "day1-v1.json");
var assets = Path.Combine(root, "src", "Fatewake.Web", "wwwroot", "art");

var artworkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(source, "*.razor", SearchOption.AllDirectories)))
{
    var text = File.ReadAllText(file);
    foreach (Match m in Regex.Matches(text, "\"(?<key>(?:bg|char|prop|fg|light|effect|wear|tool|injury|condition)-[a-z0-9-]+)\""))
        artworkKeys.Add(m.Groups["key"].Value);
    // Composition keys belong to scene beats and artwork switches, not scene IDs or rules versions.
    foreach (Match m in Regex.Matches(text, "(?:BeatKind\\.\\w+\\s*,\\s*\"(?<key>[a-z0-9-]+)\"|\"(?<key>day1-[a-z0-9-]+)\"\\s*=>)"))
        artworkKeys.Add(m.Groups["key"].Value);
}
var catalogPath = Path.Combine(root, "src", "Fatewake.Api", "SceneArtCatalog.cs");
var catalogText = File.ReadAllText(catalogPath);
var allowlist = Regex.Match(catalogText, @"private static readonly string\[\] Keys\s*=\s*\[(?<keys>.*?)\];", RegexOptions.Singleline);
HashSet<string> sceneKeys = allowlist.Success
    ? Regex.Matches(allowlist.Groups["keys"].Value, "\"(?<key>[a-z0-9-]+)\"")
        .Select(match => match.Groups["key"].Value).ToHashSet(StringComparer.OrdinalIgnoreCase)
    : new(StringComparer.OrdinalIgnoreCase);
var promptKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var invalidSeedEntries = new List<string>();
if (File.Exists(promptSeed))
{
    using var seed = JsonDocument.Parse(File.ReadAllText(promptSeed));
    if (seed.RootElement.TryGetProperty("prompts", out var entries) && entries.ValueKind == JsonValueKind.Array)
    {
        foreach (var entry in entries.EnumerateArray())
        {
            var key = entry.TryGetProperty("assetKey", out var keyValue) ? keyValue.GetString() : null;
            var positive = entry.TryGetProperty("positivePrompt", out var positiveValue) ? positiveValue.GetString() : null;
            var negative = entry.TryGetProperty("negativePrompt", out var negativeValue) ? negativeValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(positive) || positive.Length > 8000 ||
                negative?.Length > 2000 || !promptKeys.Add(key))
                invalidSeedEntries.Add(key ?? "(missing key)");
        }
    }
}
var assetKeys = Directory.Exists(assets) ? Directory.EnumerateFiles(assets, "*.*", SearchOption.AllDirectories).Where(x => new[] { ".webp", ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase)).Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : [];

var missingSeeds = sceneKeys.Where(x => !promptKeys.Contains(x)).Order().ToArray();
var orphanSeeds = promptKeys.Where(x => !sceneKeys.Contains(x)).Order().ToArray();
var placeholders = artworkKeys.Where(x => !assetKeys.Contains(x)).Order().ToArray();
var noRuntimePromptFileReads = !catalogText.Contains("art/prompts", StringComparison.OrdinalIgnoreCase) &&
    !catalogText.Contains("art\\prompts", StringComparison.OrdinalIgnoreCase) &&
    !File.ReadAllText(Path.Combine(root, "src", "Fatewake.Api", "Fatewake.Api.csproj"))
        .Contains("SceneArt/prompts", StringComparison.OrdinalIgnoreCase);
logger.LogInformation("Asset validation found {ReferencedCount} artwork references and {MissingSeedCount} missing SQL prompt seeds", artworkKeys.Count, missingSeeds.Length);

Console.WriteLine($"Fatewake Asset Report");
Console.WriteLine($"Referenced artwork keys : {artworkKeys.Count}");
Console.WriteLine($"Approved/render assets : {artworkKeys.Count - placeholders.Length}");
Console.WriteLine($"Placeholders            : {placeholders.Length}");
Console.WriteLine($"SQL scene metadata       : {sceneKeys.Count}");
Console.WriteLine($"SQL prompt seed entries  : {promptKeys.Count}");
Console.WriteLine($"Missing prompt seeds     : {missingSeeds.Length}");
Console.WriteLine($"Orphan prompt seeds      : {orphanSeeds.Length}");
Console.WriteLine($"Seed entries invalid     : {invalidSeedEntries.Count}");
Console.WriteLine($"Runtime prompt-file reads: {(noRuntimePromptFileReads ? "none" : "found")}");
if (missingSeeds.Length > 0) { Console.WriteLine("\nScene metadata missing SQL seeds:"); foreach (var x in missingSeeds) Console.WriteLine($"  - {x}"); }
if (placeholders.Length > 0) { Console.WriteLine("\nArtwork still placeholder:"); foreach (var x in placeholders) Console.WriteLine($"  - {x}"); }
if (orphanSeeds.Length > 0) { Console.WriteLine("\nSQL seeds not in scene metadata:"); foreach (var x in orphanSeeds) Console.WriteLine($"  - {x}"); }
if (invalidSeedEntries.Count > 0) { Console.WriteLine("\nInvalid SQL seed entries:"); foreach (var x in invalidSeedEntries) Console.WriteLine($"  - {x}"); }
operation.Dispose();
await host.StopAsync();
return allowlist.Success && missingSeeds.Length == 0 && orphanSeeds.Length == 0 &&
    invalidSeedEntries.Count == 0 && noRuntimePromptFileReads ? 0 : 2;

static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null) { if (File.Exists(Path.Combine(d.FullName, "Fatewake.slnx"))) return d.FullName; d = d.Parent; } return Directory.GetCurrentDirectory(); }
