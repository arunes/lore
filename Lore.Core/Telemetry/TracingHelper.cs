using System.Diagnostics;

namespace Lore.Core.Telemetry;

public static class TracingHelper
{
    public static Activity? StartStageSpan(string stage, string filePathOrId, string? traceParent)
    {
        ActivityContext parentContext = default;
        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            ActivityContext.TryParse(traceParent, null, out parentContext);
        }

        var activity = LoreActivitySource.Source.StartActivity(
            $"pipeline.{stage}/process_file",
            ActivityKind.Internal,
            parentContext);

        activity?.SetTag("pipeline.stage", stage);
        activity?.SetTag("file.identifier", filePathOrId);

        return activity;
    }
}