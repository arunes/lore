# lore
lore: local object retrieval engine


Experimental local rag system using;
- SemanticKernel
- Vector Search


Tech stack
- .NET 10, ASP.NET Core
- Entity Framework Core, SQLite
- sqlite-vec (vector search), FTS5 (full-text search)
- Semantic Kernel, Microsoft.Extensions.AI
- SmartComponents.LocalEmbeddings (ONNX)
- OpenAI-compatible LLM backends (LM Studio, etc.)
- Hybrid search with Reciprocal Rank Fusion
- OpenTelemetry (traces, metrics, structured logs)
- Aspire Dashboard (observability)
- React, TypeScript, Vite

Features
- Retrieval-Augmented Generation (RAG) — traditional & agentic
- 5-stage async file processing pipeline (Channel<T>, BackgroundService)
- 14 text extractors (PDF, DOCX, XLSX, PPTX, HTML, images via OCR, etc.)
- Semantic document classification
- Text chunking with overlap
- SHA-256 file deduplication
- Real-time file system watching (FileSystemWatcher)

Development
- EditorConfig — code style, naming, formatting
- dotnet format — CI-enforced style + analyzers
- GitHub Actions — build, format check, analyzer check

Text extractors
PdfPig, DocumentFormat.OpenXml, NPOI.HWPF, ExcelDataReader, HtmlAgilityPack, RapidOcrNet



started  6:26:58.432 PM
finished 6:41:29.782 PM

15 mins