using System.Globalization;
using System.Reflection;
using System.Text;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Services;

public static class ProductCsvExporter
{
    public static async Task WriteAsync(Stream output, IEnumerable<ProductDTO> products,
        IReadOnlyList<(string Header, PropertyInfo Property)> columns, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(columns);

        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(';', columns.Select(column => Escape(column.Header))).AsMemory(), cancellationToken);
        foreach (var product in products)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = columns.Select(column => Escape(Convert.ToString(
                column.Property.GetValue(product), CultureInfo.GetCultureInfo("pt-BR")) ?? string.Empty));
            await writer.WriteLineAsync(string.Join(';', values).AsMemory(), cancellationToken);
        }
        await writer.FlushAsync(cancellationToken);
    }

    private static string Escape(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}