---
name: document-ingestion
description: Guides file discovery, filesystem event handling, extractor selection, text normalization, deduplication, and durable ingestion handoff in Lore.
---

# Skill: Document Ingestion and Extraction

## Skill Name & Purpose

**Document Ingestion and Extraction** owns discovery of files, filesystem event handling, deduplication, extractor selection, text extraction, normalization, and durable handoff into classification. It must turn a path into a safely persisted `FileEntry` without allowing unsupported, missing, empty, or failed files to masquerade as indexed documents.

## Key Classes & Interfaces

- `Lore.Core/Pipeline/StartupService.cs` — startup resume, initial full scan, and watcher startup.
- `Lore.Core/Pipeline/DirectoryWatcher.cs` — debounced `FileSystemWatcher` events and excluded extensions.
- `Lore.Core/Pipeline/FileArrivalService.cs` — existence/modification checks, SHA-256 hashing, and file upsert.
- `Lore.Core/Pipeline/TextExtractService.cs` — extraction, cleaning, status transitions, and handoff.
- `Lore.Core/Pipeline/IChannelService.cs` and `ChannelProcessor<TRequest>` — stage contract and batching.
- `Lore.Core/Pipeline/Requests.cs` — `FileArrivalRequest`, `TextExtractRequest`, and `FileClassifyRequest`.
- `Lore.Core/TextExtractors/ITextExtractor.cs` — extractor contract.
- `Lore.Core/TextExtractors/ITextExtractorFactory.cs` and `TextExtractorFactory.cs` — normalized extension lookup.
- `Lore.Core/TextExtractors/SupportedExtensionsAttribute.cs` — extractor registration metadata.
- `Lore.Core/Retrieval/RetrievalTextExtensions.cs` — `CleanTextForRAG` normalization.
- `Lore.Data/Models/FileEntry.cs` — persisted file metadata, content, and `FileProcessStatus`.

Supported extractors are registered by reflection and keyed by extension. Existing implementations cover plain text, PDF, DOC/DOCX, ODT, RTF, XML, JSON, HTML, spreadsheets, presentations, and images through OCR.

## Inputs & Outputs

**Inputs**

- `FileArrivalRequest(FilePath, TraceParent?)` from a full scan or watcher.
- `TextExtractRequest(FilePath, TraceParent?)` from the arrival stage.
- A readable path under an enabled `FileSource`.

**Outputs**

- A new or updated `FileEntry` containing normalized path metadata, modification timestamps, size, SHA-256 hash, and processing status.
- On successful nonblank extraction: cleaned `FileEntry.Content` with status `TextExtracted`, followed by `FileClassifyRequest(FileId, TraceParent?)`.
- On expected boundaries: `NotSupportedFile`, `EmptyContent`, or `TextExtractionFailed`, with a structured log and no classification handoff.

## Step-by-Step Execution Rules

1. Start by determining whether the change belongs to discovery, arrival persistence, extractor resolution, or extraction. Do not add extraction logic to a route or retrieval service.
2. For a new file type, implement `ITextExtractor` in `Lore.Core/TextExtractors`, add `SupportedExtensionsAttribute` with lowercase and/or normalized extensions, and ensure all file I/O accepts and passes `CancellationToken`.
3. Keep extractors focused on reading a file and returning text. Do not persist entities, enqueue channels, call an LLM, or perform classification from an extractor.
4. Use `ITextExtractorFactory.GetExtractor(filePath)` so extension lookup remains keyed and case-insensitive. Preserve `NotSupportedException` for an unavailable extension.
5. Clean every extracted result with `CleanTextForRAG`. Treat null, whitespace-only, and cleaned-empty results as `EmptyContent`.
6. Preserve `FileEntry` identity by full path and use the existing arrival fast path. If changing deduplication, define whether `LastWriteTimeUtc`, size, and SHA-256 are authoritative and test changed, unchanged, renamed, and deleted files.
7. Persist the current stage before writing the next channel message. Keep the trace parent from the current activity when handing off.
8. Use `IDbContextFactory<LoreDbContext>` in concurrent/background processing. Never use one context from parallel extraction tasks.
9. Catch only expected boundaries. Unsupported files and extraction failures get distinct statuses; cancellation should propagate; unexpected infrastructure failures must not be silently converted to successful extraction.
10. For watcher changes, retain debounce behavior and extension exclusion. Any path authorization or source matching must use normalized full paths and directory boundaries.
11. Add a structured logger event and a metric/tag when introducing a new outcome or expensive extractor. Do not log extracted document contents.

## Example Usage / Pattern

```csharp
[SupportedExtensions(".txt", ".mdx")]
public sealed class MarkdownExtractor : ITextExtractor
{
    public Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return File.ReadAllTextAsync(filePath, cancellationToken);
    }
}

// TextExtractService remains responsible for normalization and status handling.
string? cleanedText = (await extractor.ExtractTextAsync(path, cancellationToken))
    .CleanTextForRAG();

if (string.IsNullOrWhiteSpace(cleanedText))
{
    status = FileProcessStatus.EmptyContent;
}
else
{
    status = FileProcessStatus.TextExtracted;
    content = cleanedText;
}
```
