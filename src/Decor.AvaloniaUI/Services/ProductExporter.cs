using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using Decor.Core.DTOs;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Decor.AvaloniaUI.Services;

public static class ProductExporter
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");

    public static Task WriteAsync(Stream output, IEnumerable<ProductDTO> products,
        IReadOnlyList<(string Header, PropertyInfo Property)> columns, ProductExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(columns);

        return format switch
        {
            ProductExportFormat.Csv => ProductCsvExporter.WriteAsync(output, products, columns, cancellationToken),
            ProductExportFormat.Xls => WriteExcelAsync(output, products, columns, new HSSFWorkbook(), cancellationToken),
            ProductExportFormat.Xlsx => WriteExcelAsync(output, products, columns, new XSSFWorkbook(), cancellationToken),
            ProductExportFormat.Odt => WriteOdtAsync(output, products, columns, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static async Task WriteExcelAsync(Stream output, IEnumerable<ProductDTO> products,
        IReadOnlyList<(string Header, PropertyInfo Property)> columns, IWorkbook workbook,
        CancellationToken cancellationToken)
    {
        try
        {
            var sheet = workbook.CreateSheet("Produtos");
            var header = sheet.CreateRow(0);
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                header.CreateCell(columnIndex).SetCellValue(columns[columnIndex].Header);

            var rowIndex = 1;
            foreach (var product in products)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = sheet.CreateRow(rowIndex++);
                for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    WriteExcelCell(row.CreateCell(columnIndex), columns[columnIndex].Property.GetValue(product));
            }

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                sheet.AutoSizeColumn(columnIndex);

            await Task.Run(() => workbook.Write(output, leaveOpen: true), cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
        finally
        {
            workbook.Close();
            workbook.Dispose();
        }
    }

    private static void WriteExcelCell(ICell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.SetCellType(CellType.Blank);
                break;
            case bool boolean:
                cell.SetCellValue(boolean);
                break;
            case byte or short or int or long or float or double or decimal:
                cell.SetCellValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case DateTime dateTime:
                cell.SetCellValue(dateTime);
                break;
            default:
                cell.SetCellValue(Convert.ToString(value, BrazilianCulture) ?? string.Empty);
                break;
        }
    }

    private static async Task WriteOdtAsync(Stream output, IEnumerable<ProductDTO> products,
        IReadOnlyList<(string Header, PropertyInfo Property)> columns, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var mimetype = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        await using (var stream = mimetype.Open())
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            await writer.WriteAsync("application/vnd.oasis.opendocument.spreadsheet".AsMemory(), cancellationToken);

        var manifest = archive.CreateEntry("META-INF/manifest.xml", CompressionLevel.Optimal);
        await using (var stream = manifest.Open())
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Async = true,
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = false
        }))
        {
            await writer.WriteStartDocumentAsync();
            await writer.WriteStartElementAsync("manifest", "manifest", "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0");
            await writer.WriteAttributeStringAsync("manifest", "version", "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0", "1.3");
            await WriteManifestEntryAsync(writer, "/", "application/vnd.oasis.opendocument.spreadsheet");
            await WriteManifestEntryAsync(writer, "content.xml", "text/xml");
            await writer.WriteEndElementAsync();
            await writer.WriteEndDocumentAsync();
        }

        var content = archive.CreateEntry("content.xml", CompressionLevel.Optimal);
        await using (var stream = content.Open())
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Async = true,
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = false
        }))
        {
            await writer.WriteStartDocumentAsync();
            await writer.WriteStartElementAsync("office", "document-content", "urn:oasis:names:tc:opendocument:xmlns:office:1.0");
            await writer.WriteAttributeStringAsync("xmlns", "table", null, "urn:oasis:names:tc:opendocument:xmlns:table:1.0");
            await writer.WriteAttributeStringAsync("xmlns", "text", null, "urn:oasis:names:tc:opendocument:xmlns:text:1.0");
            await writer.WriteAttributeStringAsync("office", "version", "urn:oasis:names:tc:opendocument:xmlns:office:1.0", "1.3");
            await writer.WriteStartElementAsync("office", "body", "urn:oasis:names:tc:opendocument:xmlns:office:1.0");
            await writer.WriteStartElementAsync("office", "spreadsheet", "urn:oasis:names:tc:opendocument:xmlns:office:1.0");
            await writer.WriteStartElementAsync("table", "table", "urn:oasis:names:tc:opendocument:xmlns:table:1.0");
            await writer.WriteAttributeStringAsync("table", "name", "urn:oasis:names:tc:opendocument:xmlns:table:1.0", "Produtos");

            await WriteOdtRowAsync(writer, columns.Select(column => (object?)column.Header), cancellationToken);
            foreach (var product in products)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteOdtRowAsync(writer, columns.Select(column => column.Property.GetValue(product)), cancellationToken);
            }

            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
            await writer.WriteEndDocumentAsync();
            await writer.FlushAsync();
        }

        await output.FlushAsync(cancellationToken);
    }

    private static async Task WriteOdtRowAsync(XmlWriter writer, IEnumerable<object?> values, CancellationToken cancellationToken)
    {
        await writer.WriteStartElementAsync("table", "table-row", "urn:oasis:names:tc:opendocument:xmlns:table:1.0");
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var valueType = value switch
            {
                bool => "boolean",
                byte or short or int or long or float or double or decimal => "float",
                DateTime => "date",
                _ => "string"
            };
            await writer.WriteStartElementAsync("table", "table-cell", "urn:oasis:names:tc:opendocument:xmlns:table:1.0");
            await writer.WriteAttributeStringAsync("office", "value-type", "urn:oasis:names:tc:opendocument:xmlns:office:1.0", valueType);
            if (value is not null && valueType != "string")
            {
                var attributeName = valueType == "float" ? "value" : valueType == "date" ? "date-value" : "boolean-value";
                var attributeValue = value switch
                {
                    DateTime dateTime => dateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                    bool boolean => boolean ? "true" : "false",
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0"
                };
                await writer.WriteAttributeStringAsync("office", attributeName, "urn:oasis:names:tc:opendocument:xmlns:office:1.0", attributeValue);
            }
            await writer.WriteStartElementAsync("text", "p", "urn:oasis:names:tc:opendocument:xmlns:text:1.0");
            await writer.WriteStringAsync(Convert.ToString(value, BrazilianCulture) ?? string.Empty);
            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
        }
        await writer.WriteEndElementAsync();
    }

    private static async Task WriteManifestEntryAsync(XmlWriter writer, string path, string mediaType)
    {
        await writer.WriteStartElementAsync("manifest", "file-entry", "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0");
        await writer.WriteAttributeStringAsync("manifest", "full-path", "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0", path);
        await writer.WriteAttributeStringAsync("manifest", "media-type", "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0", mediaType);
        await writer.WriteEndElementAsync();
    }
}
