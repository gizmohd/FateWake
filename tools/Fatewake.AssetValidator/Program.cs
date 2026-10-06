using System.Text.RegularExpressions;
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

var root=args.Length>0?Path.GetFullPath(args[0]):FindRoot();
var source=Path.Combine(root,"src");
var prompts=Path.Combine(root,"art","prompts");
var assets=Path.Combine(root,"src","Fatewake.Web","wwwroot","art");

var artworkKeys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach(var file in Directory.EnumerateFiles(source,"*.cs",SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(source,"*.razor",SearchOption.AllDirectories)))
{
    var text=File.ReadAllText(file);
    foreach(Match m in Regex.Matches(text,"\"(?<key>(?:bg|char|prop|fg|light|effect|wear|tool|injury|condition)-[a-z0-9-]+)\""))
        artworkKeys.Add(m.Groups["key"].Value);
    // Composition keys belong to scene beats and artwork switches, not scene IDs or rules versions.
    foreach(Match m in Regex.Matches(text,"(?:BeatKind\\.\\w+\\s*,\\s*\"(?<key>[a-z0-9-]+)\"|\"(?<key>day1-[a-z0-9-]+)\"\\s*=>)"))
        artworkKeys.Add(m.Groups["key"].Value);
}
var promptKeys=Directory.Exists(prompts)?Directory.EnumerateFiles(prompts,"*.md").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase):[];
var assetKeys=Directory.Exists(assets)?Directory.EnumerateFiles(assets,"*.*",SearchOption.AllDirectories).Where(x=>new[]{".webp",".png",".jpg",".jpeg"}.Contains(Path.GetExtension(x),StringComparer.OrdinalIgnoreCase)).Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase):[];

var missingPrompts=artworkKeys.Where(x=>!promptKeys.Contains(x)).Order().ToArray();
var placeholders=artworkKeys.Where(x=>!assetKeys.Contains(x)).Order().ToArray();
var orphanPrompts=promptKeys.Where(x=>!artworkKeys.Contains(x)&&!x.StartsWith("TEMPLATE-",StringComparison.OrdinalIgnoreCase)).Order().ToArray();
logger.LogInformation("Asset validation found {ReferencedCount} referenced keys and {MissingPromptCount} missing prompts", artworkKeys.Count, missingPrompts.Length);

Console.WriteLine($"Fatewake Asset Report");
Console.WriteLine($"Referenced artwork keys : {artworkKeys.Count}");
Console.WriteLine($"Approved/render assets : {artworkKeys.Count-placeholders.Length}");
Console.WriteLine($"Placeholders            : {placeholders.Length}");
Console.WriteLine($"Missing prompts          : {missingPrompts.Length}");
Console.WriteLine($"Orphan prompts           : {orphanPrompts.Length}");
if(missingPrompts.Length>0){Console.WriteLine("\nMissing prompt files:");foreach(var x in missingPrompts)Console.WriteLine($"  - {x}");}
if(placeholders.Length>0){Console.WriteLine("\nArtwork still placeholder:");foreach(var x in placeholders)Console.WriteLine($"  - {x}");}
if(orphanPrompts.Length>0){Console.WriteLine("\nPrompts not currently referenced:");foreach(var x in orphanPrompts)Console.WriteLine($"  - {x}");}
operation.Dispose();
await host.StopAsync();
return missingPrompts.Length==0?0:2;

static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null){if(File.Exists(Path.Combine(d.FullName,"Fatewake.slnx")))return d.FullName;d=d.Parent;}return Directory.GetCurrentDirectory();}
