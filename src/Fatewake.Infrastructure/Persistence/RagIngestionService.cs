using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Extracts uploaded documents, creates reviewable chunks, and records reproducible ingestion runs.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagIngestionService.md">RagIngestionService documentation</see>.</remarks>
public sealed class RagIngestionService(FatewakeDbContext db) : IRagIngestionService
{
    private const int MaxChunkCharacters = 1200;
    private const int ChunkOverlapCharacters = 150;

    public async Task<RagImportResult> ImportAsync(RagImportRequest request, CancellationToken ct = default)
    {
        if (request.Bytes.Length is < 1 or > 20 * 1024 * 1024)
            throw new ArgumentException("Uploaded documents must contain 1–20 MB of data.", nameof(request));
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var text = ExtractText(extension, request.Bytes);
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("The uploaded document contains no readable text.");
        var contentHash = Convert.ToHexString(SHA256.HashData(request.Bytes)).ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var run = new RagIngestRunRecord
        {
            Id = Guid.NewGuid(),
            RunKey = $"{contentHash}:{request.SourceKey}",
            Phase = "Chunking",
            Status = "Running",
            IngestorVersion = "rag-ingestor-1",
            SourceCount = 0,
            DocumentCount = 0,
            ChunkCount = 0,
            StartedAt = now
        };
        db.RagIngestRuns.Add(run);
        await db.SaveChangesAsync(ct);
        try
        {
            var source = await db.RagSources.SingleOrDefaultAsync(x => x.SourceKey == request.SourceKey, ct);
            if (source is null)
            {
                source = new RagSourceRecord
                {
                    Id = Guid.NewGuid(),
                    SourceKey = request.SourceKey,
                    SourceType = request.SourceType,
                    Title = request.Title,
                    Origin = "admin-upload",
                    ContentHash = contentHash,
                    LicenseStatus = request.LicenseStatus,
                    ReviewStatus = request.ReviewStatus,
                    Visibility = request.Visibility,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.RagSources.Add(source);
            }
            run.SourceCount = 1;
            var documentKey = $"{request.FileName}:{contentHash}";
            var document = await db.RagDocuments.SingleOrDefaultAsync(x => x.SourceId == source.Id && x.DocumentKey == documentKey, ct);
            var reused = document is not null;
            if (document is null)
            {
                document = new RagDocumentRecord
                {
                    Id = Guid.NewGuid(),
                    SourceId = source.Id,
                    DocumentKey = documentKey,
                    DocumentType = extension.TrimStart('.'),
                    Title = request.Title,
                    ContentHash = contentHash,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        fileName = request.FileName,
                        contentType = request.ContentType,
                        byteSize = request.Bytes.Length,
                        timelineKey = request.TimelineKey,
                        spoilerSeries = request.SpoilerSeries
                    }),
                    Status = "Approved",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.RagDocuments.Add(document);
            }
            run.DocumentCount = 1;
            var chunks = ChunkText(text);
            if (!reused)
            {
                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    db.RagChunks.Add(new RagChunkRecord
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        ChunkKey = $"{document.Id:N}:{i}",
                        Ordinal = i,
                        HeadingPath = null,
                        Text = chunk.Text,
                        StartOffset = chunk.Start,
                        EndOffset = chunk.End,
                        TimelineKey = request.TimelineKey,
                        SpoilerSeriesMin = request.SpoilerSeries,
                        SpoilerSeriesMax = request.SpoilerSeries,
                        CanonStatus = "Candidate",
                        ReviewStatus = request.ReviewStatus,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }
            run.ChunkCount = reused ? await db.RagChunks.CountAsync(x => x.DocumentId == document.Id, ct) : chunks.Count;
            run.Status = "Completed";
            run.Phase = "IndexReady";
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return new RagImportResult(run.Id, source.Id, document.Id, run.ChunkCount, reused);
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.ErrorDetail = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<RagStatusResult> StatusAsync(CancellationToken ct = default)
    {
        var runs = await db.RagIngestRuns.AsNoTracking().OrderByDescending(x => x.StartedAt).Take(50)
            .Select(x => new RagRunStatus(x.Id, x.RunKey, x.Phase, x.Status, x.IngestorVersion, x.EmbeddingModel,
                x.SourceCount, x.DocumentCount, x.ChunkCount, x.ErrorDetail, x.StartedAt, x.CompletedAt))
            .ToArrayAsync(ct);
        return new RagStatusResult(
            await db.RagSources.CountAsync(ct),
            await db.RagDocuments.CountAsync(ct),
            await db.RagChunks.CountAsync(ct),
            await db.RagChunks.CountAsync(x => x.EmbeddingModel != null && x.EmbeddedAt != null, ct),
            await db.RagClaims.CountAsync(ct),
            await db.RagClaims.CountAsync(x => x.Status == "Approved", ct),
            runs);
    }

    private static string ExtractText(string extension, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return extension switch
        {
            ".pdf" => ExtractPdf(stream),
            ".docx" => ExtractDocx(stream),
            ".html" or ".htm" => Regex.Replace(Encoding.UTF8.GetString(bytes), "<[^>]+>", " ", RegexOptions.IgnoreCase),
            ".txt" or ".md" or ".json" or ".csv" or ".xml" or ".yaml" or ".yml" => Encoding.UTF8.GetString(bytes),
            _ => throw new NotSupportedException($"Unsupported RAG document type '{extension}'.")
        };
    }

    private static string ExtractPdf(Stream stream)
    {
        var text = new StringBuilder();
        using var document = PdfDocument.Open(stream);
        foreach (var page in document.GetPages())
        {
            text.AppendLine(page.Text);
        }
        return text.ToString();
    }

    private static string ExtractDocx(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body is null ? string.Empty : string.Join("\n", body.Descendants<Paragraph>().Select(x => x.InnerText));
    }

    private static string NormalizeText(string text)
    {
        return Regex.Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'), "[\\t ]+", " ")
            .Trim();
    }

    private static List<(string Text, int Start, int End)> ChunkText(string text)
    {
        var chunks = new List<(string Text, int Start, int End)>();
        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(text.Length, start + MaxChunkCharacters);
            if (end < text.Length)
            {
                var boundary = text.LastIndexOf('\n', end, end - start);
                if (boundary <= start)
                    boundary = text.LastIndexOf('.', end, end - start);
                if (boundary > start)
                    end = boundary + 1;
            }
            chunks.Add((text[start..end].Trim(), start, end));
            if (end >= text.Length)
                break;
            start = Math.Max(start + 1, end - ChunkOverlapCharacters);
        }
        return chunks;
    }
}
