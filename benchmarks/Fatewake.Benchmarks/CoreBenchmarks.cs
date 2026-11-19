using BenchmarkDotNet.Attributes;
using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using System.Text.Json;

namespace Fatewake.Benchmarks;

/// <summary>Measures deterministic resolution and visual cache fingerprint latency/allocations without logging in measured paths.</summary>
/// <see href="../../docs/code/benchmarks/Fatewake.Benchmarks/CoreBenchmarks.md">Documentation</see>
[MemoryDiagnoser]
public class CoreBenchmarks
{
    private readonly DayOneGameEngine engine = new();
    private readonly GameSnapshot state = new(Guid.Empty, Guid.Empty, 1, "day-001-injured-stranger", new Dictionary<string, string>());
    private readonly CandidateAction action = new("help_injured_stranger", new Dictionary<string, string>());
    private readonly CharacterAppearance appearance = new();
    private readonly string checkpoint = JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        RulesVersion = "day1-v1",
        ContentVersion = "day1-scenes-v1",
        Facts = new Dictionary<string, string> { ["michelle.trust"] = "increased" },
        Panels = Enumerable.Range(0, 9).Select(index => new
        {
            Sequence = index + 1,
            Narration = "An ordinary morning becomes a silence full of consequential decisions.",
            Layers = new[] { "bg-day1-neighborhood-player-threshold", "char-michelle-day1-supporting" },
            CauseRevealed = false
        }).ToArray()
    });

    /// <summary>Measures accepted day-one consequence calculation.</summary>
    /// <returns>Resolved authoritative consequences.</returns>
    [Benchmark]
    public ActionResolution ResolveDayOne() => engine.Resolve(state, action);

    /// <summary>Measures stable artwork cache-key normalization, serialization, and hashing.</summary>
    /// <returns>The normalized fingerprint.</returns>
    [Benchmark]
    public string AppearanceFingerprint() => CharacterAppearanceFingerprint.Create("default-survivor", 1, appearance);

    /// <summary>Measures JSONB-stable checkpoint integrity hashing for a representative reached chapter.</summary>
    [Benchmark]
    public string CheckpointFingerprint() => TimelineSnapshotCodec.Hash(checkpoint);
}
