using System.Collections.Concurrent;
using System.Threading.Channels;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lore.Common.Helpers;
using Lore.Core.TextExtractors;
using Lore.Data;
using Lore.Data.Models;

namespace Lore.Core.Pipeline;

public class TextExtractService(
    ILogger<TextExtractService> logger,
    ITextExtractorFactory textExtractorFactory,
    Channel<FileClassifyRequest> fileClassifyChannel,
    IDbContextFactory<LoreDbContext> dbContextFactory
) : IChannelService<TextExtractRequest>
{
    public int GetBatchSize() => 10;

    public async Task ProcessAsync(
        TextExtractRequest request,
        CancellationToken cancellationToken
    ) => await ProcessBatchAsync([request], cancellationToken);

    public async Task ProcessBatchAsync(
        IReadOnlyList<TextExtractRequest> requests,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Starting TextExtractor process for {TotalFiles} files",
            requests.Count
        );

        var fileEntries = new ConcurrentBag<FileEntry>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken,
        };

        Dictionary<string, int> fileEntryIds = [];
        var incomingPaths = requests.Select(req => req.FilePath).Distinct();
        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            fileEntryIds = await dbContext
                .Files.AsNoTracking()
                .Where(fl => incomingPaths.Contains(fl.Path))
                .Select(fl => new { fl.Path, fl.Id })
                .ToDictionaryAsync(fl => fl.Path, fl => fl.Id, cancellationToken);
        }

        await Parallel.ForEachAsync(
            requests,
            parallelOptions,
            async (request, ct) =>
            {
                if (!fileEntryIds.TryGetValue(request.FilePath, out var fileId))
                {
                    logger.LogError(
                        "File path {FilePath} not found in database. Skipping text extraction",
                        request.FilePath
                    );
                    return;
                }

                var fileEntry = new FileEntry
                {
                    Id = fileEntryIds[request.FilePath],
                    ProcessStatus = FileProcessStatus.Pending,

                    Path = default!,
                    Name = default!,
                    Hash = default!,
                    Extension = default!,
                    Directory = default!,
                };

                try
                {
                    var extractor = textExtractorFactory.GetExtractor(request.FilePath);
                    var extractedText = await extractor.ExtractTextAsync(request.FilePath);
                    var cleanedText = StringHelpers.CleanTextForRAG(extractedText);
                    if (string.IsNullOrWhiteSpace(cleanedText))
                    {
                        fileEntry.ProcessStatus = FileProcessStatus.EmptyContent;
                    }
                    else
                    {
                        fileEntry.ProcessStatus = FileProcessStatus.TextExtracted;
                        fileEntry.Content = cleanedText;
                    }

                    fileEntries.Add(fileEntry);
                }
                catch (NotSupportedException nex)
                {
                    fileEntry.ProcessStatus = FileProcessStatus.NotSupportedFile;
                    fileEntries.Add(fileEntry);

                    logger.LogWarning(nex.Message);
                    return;
                }
                catch (Exception ex)
                {
                    fileEntry.ProcessStatus = FileProcessStatus.TextExtractionFailed;
                    fileEntry.Content = ex.Message;
                    fileEntries.Add(fileEntry);
                    return;
                }
            }
        );

        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await dbContext.BulkUpdateAsync(
                    fileEntries.ToList(),
                    new BulkConfig
                    {
                        PropertiesToInclude =
                        [
                            nameof(FileEntry.Content),
                            nameof(FileEntry.ProcessStatus),
                        ],
                    },
                    cancellationToken: cancellationToken
                );
            });
        }

        logger.LogInformation("TextExtractor process finished");
        var extractedFiles = fileEntries.Where(fl =>
            fl.ProcessStatus == FileProcessStatus.TextExtracted
        );
        
        foreach (var entry in extractedFiles)
        {
            await fileClassifyChannel.Writer.WriteAsync(
                new FileClassifyRequest(entry.Id),
                cancellationToken
            );
        }
    }
}
