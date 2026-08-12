using RapidOcrNet;

namespace Lore.Core.TextExtractors;

public class VisionExtractor(RapidOcr rapidOcr) : ITextExtractor
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            var result = rapidOcr.Detect(filePath, RapidOcrOptions.Default);
            return result.StrRes;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}