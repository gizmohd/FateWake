namespace Fatewake.Infrastructure.Persistence;

/// <summary>Returns the import run and persisted document/chunk counts.</summary>
public sealed record RagImportResult(Guid RunId, Guid SourceId, Guid DocumentId, int ChunkCount, bool Reused);
