namespace Lore.Core.TextExtractors;

public interface ITextExtractorFactory
{
    ITextExtractor GetExtractor(string filePath);
}

public class TextExtractorFactory : ITextExtractorFactory
{
    public ITextExtractor GetExtractor(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

        return extension switch
        {
            ".txt" or ".log" or ".csv" or ".md" or ".markdown" or ".sql" or ".css" =>
                new PlainTextExtractor(),
            ".json" or ".jsonl" or ".ndjson" or ".gdoc" or ".gsheet" => new JsonExtractor(),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => new VisionExtractor(),
            ".docx" or ".docm" or ".dotx" or ".dotm" => new DocxExtractor(),
            ".doc" => new DocExtractor(),
            ".rtf" => new RtfExtractor(),
            ".xlsx" or ".xls" or ".ods" => new SpreadsheetExtractor(),
            ".odt" => new OdtExtractor(),
            ".pptx" or ".ppt" => new PresentationExtractor(),
            ".html" or ".htm" => new HtmlExtractor(),
            ".xml"
            or ".xhtml"
            or ".xht"
            or ".gpx"
            or ".kml"
            or ".svg"
            or ".rss"
            or ".atom"
            or ".plist"
            or ".xlf"
            or ".xliff"
            or ".wsdl"
            or ".xslt"
            or ".xsl"
            or ".xsd"
            or ".config"
            or ".csproj"
            or ".vbproj" => new XmlExtractor(),
            ".pdf" => new PdfExtractor(),
            ".pem" or ".ppk" or ".zip" or ".vsd" or ".cdr" or ".ai" or ".eps" or ".mp4" =>
                new NoOpExtractor(),

            _ => throw new NotSupportedException(
                $"No extractor available for file extension '{extension}'."
            ),
        };
    }
}
