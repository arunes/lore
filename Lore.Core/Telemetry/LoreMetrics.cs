using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Lore.Core.Telemetry;

public static class LoreMetrics
{
    public static readonly Meter Meter = new("Lore", "1.0.0");

    public static readonly Counter<long> PipelineFilesProcessed = Meter.CreateCounter<long>(
        "pipeline.files.processed",
        "files",
        "Number of files processed per pipeline stage.");

    public static readonly Histogram<double> PipelineFileDuration = Meter.CreateHistogram<double>(
        "pipeline.files.duration_ms",
        "ms",
        "Processing duration per file per stage.");

    public static readonly Histogram<long> PipelineBatchSize = Meter.CreateHistogram<long>(
        "pipeline.batch.size",
        "items",
        "Number of items per batch processed.");

    public static readonly Histogram<double> PipelineBatchDuration = Meter.CreateHistogram<double>(
        "pipeline.batch.duration_ms",
        "ms",
        "Batch processing duration.");

    public static readonly Counter<long> RagChats = Meter.CreateCounter<long>(
        "rag.chats",
        "requests",
        "Number of chat requests.");

    public static readonly Histogram<double> RagRetrievalDuration = Meter.CreateHistogram<double>(
        "rag.retrieval.duration_ms",
        "ms",
        "Hybrid search retrieval duration.");

    public static readonly Histogram<double> RagLlmStreamDuration = Meter.CreateHistogram<double>(
        "rag.llm.stream_duration_ms",
        "ms",
        "LLM token streaming duration.");

    public static readonly Histogram<long> RagLlmStreamChars = Meter.CreateHistogram<long>(
        "rag.llm.stream_chars",
        "chars",
        "Number of characters streamed per chat response.");
}