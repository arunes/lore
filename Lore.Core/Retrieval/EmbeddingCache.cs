using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;
using Lore.Core.Telemetry;
using Lore.Data;
using Lore.Data.Models;
using SmartComponents.Inference;
using SmartComponents.LocalEmbeddings;

namespace Lore.Core.Retrieval;

public class EmbeddingCache(LoreDbContext dbContext, LocalEmbedder embedder, ILogger<EmbeddingCache> logger)
{
    public record CategoryVector<T>(int Id, string Name, ReadOnlyMemory<float> Vector);

    public List<(PrimaryCategory Item, EmbeddingF32 Embedding)> Categories { get; private set; } =
    [];
    public List<(DocumentType Item, EmbeddingF32 Embedding)> DocumentTypes { get; private set; } =
    [];

    public PrimaryCategory? FindBestCategory(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var results = embedder.FindClosestWithScore(GetSimilarityQuery(input), Categories);
        if (results.Length > 0)
        {
            return results.FirstOrDefault().Item;
        }

        return null;
    }

    public DocumentType? FindBestDocumentType(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var results = embedder.FindClosestWithScore(GetSimilarityQuery(input), DocumentTypes);
        if (results.Length > 0)
        {
            return results.FirstOrDefault().Item;
        }

        return null;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var activity = LoreActivitySource.Source.StartActivity("embedding/cache_init");

        var categories = await dbContext
            .PrimaryCategories.AsNoTracking()
            .ToListAsync(cancellationToken);
        foreach (var cat in categories)
        {
            var embedding = embedder.Embed(cat.Keywords);
            Categories.Add((cat, embedding));
        }

        var docTypes = await dbContext.DocumentTypes.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var dt in docTypes)
        {
            var embedding = embedder.Embed(dt.Keywords);
            DocumentTypes.Add((dt, embedding));
        }

        activity?.SetTag("cache.categories", categories.Count);
        activity?.SetTag("cache.document_types", docTypes.Count);

        logger.EmbeddingCacheLoaded(categories.Count, docTypes.Count);
    }

    private static SimilarityQuery GetSimilarityQuery(string input) =>
        new()
        {
            SearchText = input,
            MaxResults = 1,
            MinSimilarity = 0.5f,
        };
}