namespace Fatewake.Web.Presentation;

/// <summary>Captures an immutable, fingerprinted survivor visual projection.</summary>
/// <param name="SurvivorId">Survivor represented by the snapshot.</param>
/// <param name="StateVersion">Canonical visual state version used to produce the snapshot.</param>
/// <param name="Fingerprint">Deterministic fingerprint of the identity and rendered visual inputs.</param>
/// <param name="CapturedAt">Time at which the snapshot was created.</param>
/// <param name="Projection">Resolved projection stored in the snapshot.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/SurvivorVisualSnapshot.md">SurvivorVisualSnapshot documentation</see>
public sealed record SurvivorVisualSnapshot(
    Guid SurvivorId,
    long StateVersion,
    string Fingerprint,
    DateTimeOffset CapturedAt,
    SurvivorVisualProjection Projection);
