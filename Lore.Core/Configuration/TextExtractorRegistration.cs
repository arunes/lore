using System.Reflection;
using System.Text;
using Lore.Core.TextExtractors;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.Configuration;

public static class TextExtractorRegistration
{
    public static IServiceCollection AddTextExtractors(this IServiceCollection services)
    {
        // register code pages for NPOI doc parser
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var targetNamespace = "Lore.Core.TextExtractors";

        var extractorTypes = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t =>
                t.IsClass
                && !t.IsAbstract
                && t.Namespace == targetNamespace
                && typeof(ITextExtractor).IsAssignableFrom(t)
            );

        foreach (var type in extractorTypes)
        {
            var attribute = type.GetCustomAttribute<SupportedExtensionsAttribute>();
            if (attribute != null && attribute.Extensions != null)
            {
                foreach (var extension in attribute.Extensions)
                {
                    services.AddKeyedSingleton(typeof(ITextExtractor), extension, type);
                }
            }
        }

        return services.AddSingleton<ITextExtractorFactory, TextExtractorFactory>();
    }
}