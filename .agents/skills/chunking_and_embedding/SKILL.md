# Skill: Chunking and Embedding Pipeline

## Skill Name & Purpose

**Chunking and Embedding Pipeline** owns the transformation from classified extracted text to searchable chunk records and 384-dimensional local embeddings. It defines chunk boundaries, embedding input construction, vector replacement, transaction semantics, and the `ChunksCreated`/`Done` state transitions.

## Key Classes & Interfaces

- `Lore.Core/Pipeline/ChunkingService.cs` — chunk generation, `FileEntryChunk` persistence, and vectorization handoff.
- `Lore.Core/Pipeline/VectorizeService.cs` — local embedding generation and sqlite-vec writes.
- `Lore.Core/Pipeline/Requests.cs` — `ChunkingRequest` and `VectorizeRequest`.
- `Lore.Data/Models/FileEntry.cs` — source content, classification metadata, and status.
- `Lore.Data/Models/FileEntryChunk.cs` — chunk ID, file ID, chunk index, and text.
- `Lore.Data/DbContextExtensions.cs` — `EnsureVectorTablesCreatedAsync` and vector-aware context creation.
- `Lore.Core/Retrieval/EmbeddingCache.cs` — local embeddings for category/type classification (not document-vector storage).
- `SmartComponents.LocalEmbeddings/LocalEmbedder.cs` — ONNX embedding implementation and `Embed`/`EmbedRange` APIs.

Current chunking uses Semantic Kernel `TextChunker.SplitPlainTextLines(input, 100)` followed by `SplitPlainTextParagraphs(lines, 300, 30)`. The vector table is `vec_file_chunks` with `embedding float[384]`.

## Inputs & Outputs

**Inputs**

- `ChunkingRequest(FileId, TraceParent?)` for a file with cleaned `Content` and `Classified` status.
- `VectorizeRequest(FileId, TraceParent?)` for a file with persisted `FileEntryChunk` rows.
- File metadata: path, directory, primary category, and document type.

**Outputs**

- Ordered `FileEntryChunk` rows with `FileEntryId`, `ChunkIndex`, and nonblank `ChunkText`.
- One local float embedding per chunk, generated from metadata plus chunk text.
- Rows in `vec_file_chunks(chunk_id, embedding)` and status `Done` after a successful transaction.
- `VectorizationFailed` or an observable failure when vector persistence cannot complete; never falsely report a successfully searchable file.

## Step-by-Step Execution Rules

1. Keep chunking deterministic. If changing sizes, overlap, or the Semantic Kernel chunker, document the reason and evaluate re-indexing implications for all existing content.
2. Load content with `AsNoTracking` and a context factory. Do not retain tracked entities across batch or parallel work.
3. Do not create chunks for missing or whitespace-only content. Record the missing-file condition and do not enqueue vectorization for it.
4. Before persisting replacement chunks for a reprocessed file, remove old chunks and their vector rows in a deliberate, ordered operation. Ensure FTS triggers and vector cleanup remain consistent.
5. Preserve zero-based `ChunkIndex` ordering. Retrieval uses it to reconstruct context in document order.
6. Build embedding input consistently: file path/name context, directory, optional category and document type, then the chunk text. Do not embed only an unrelated filename or silently change the model for one stage.
7. Keep the embedding dimension synchronized with `vec_file_chunks` (`384` today). A model change requires a coordinated schema/rebuild plan and verification against the actual returned vector length.
8. Batch CPU-heavy embedding work with bounded concurrency. Do not call a synchronous local model from unbounded tasks; honor application cancellation around database writes.
9. Write vectors transactionally. Delete old vector rows for the affected chunk IDs, insert new rows in bounded batches, update `FileProcessStatus` to `Done`, and commit only after all writes succeed.
10. If a vector transaction fails, roll it back and preserve a failure signal. Do not log a normal completion or leave a file in `Done` when its vectors are absent.
11. Add pipeline spans, counts, durations, and safe logs. Do not log embeddings or document contents.
12. Verify both relational and virtual-table counts after changes. A file with chunks but no matching vector rows is not searchable through semantic retrieval.

## Example Usage / Pattern

```csharp
public static List<string> ChunkText(string input)
{
#pragma warning disable SKEXP0050
    var lines = TextChunker.SplitPlainTextLines(input, 100);
    return TextChunker.SplitPlainTextParagraphs(lines, 300, 30);
#pragma warning restore SKEXP0050
}

var inputs = chunks.Select(chunk =>
    $"File: {fileName}\n" +
    $"Directory: {directory}\n" +
    $"Primary Category: {category ?? "Undefined"}\n" +
    $"Document Type: {documentType ?? "Undefined"}\n\n" +
    chunk.ChunkText);

ReadOnlyMemory<float> vector = embedder.Embed(inputs.First()).Values;
if (vector.Length != 384)
{
    throw new InvalidOperationException(
        $"Embedding dimension {vector.Length} does not match vec_file_chunks dimension 384.");
}
```
