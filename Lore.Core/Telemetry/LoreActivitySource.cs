using System.Diagnostics;

namespace Lore.Core.Telemetry;

public static class LoreActivitySource
{
    public static readonly ActivitySource Source = new("Lore", "1.0.0");
}