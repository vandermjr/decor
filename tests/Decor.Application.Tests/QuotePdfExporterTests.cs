using System.Text;
using System.Text.RegularExpressions;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.Application.Tests;

public sealed class QuotePdfExporterTests
{
    [Fact]
    public void Export_CreatesSinglePageA4PdfAndLeavesStreamOpen()
    {
        using var output = new MemoryStream();

        Export(output, [CreateLine(1)]);

        var pdf = AssertPdf(output);
        Assert.Equal(1, PageCount(pdf));
        var mediaBox = Regex.Match(pdf, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        Assert.True(mediaBox.Success);
        Assert.InRange(double.Parse(mediaBox.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 595, 596);
        Assert.InRange(double.Parse(mediaBox.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 841, 843);
        Assert.True(output.CanWrite);
        output.WriteByte(0);
    }

    [Fact]
    public void Export_ManyItemsCreatesMultiplePages()
    {
        using var output = new MemoryStream();

        Export(output, Enumerable.Range(1, 150).Select(index => CreateLine(index)));

        Assert.True(PageCount(AssertPdf(output)) >= 4);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Export_ItemLargerThanPageWrapsAcrossPages(bool includeSpaces)
    {
        using var output = new MemoryStream();
        var description = includeSpaces
            ? string.Join(" ", Enumerable.Repeat("Descrição muito longa do produto", 400))
            : new string('W', 8_000);
        var item = CreateLine(1) with { ProductName = description, Category = description };

        Export(output, [item]);

        Assert.True(PageCount(AssertPdf(output)) > 2);
    }

    [Fact]
    public void Export_LongMetadataAndNotesCreatesMultiplePagesWithoutItems()
    {
        using var output = new MemoryStream();
        var longText = string.Join("\r\n", Enumerable.Repeat("Observação com acentuação e informações complementares.", 180));

        QuotePdfExporter.Export(output, 42, new DateTime(2026, 10, 6), longText,
            "Vendedor", "Aberto", [], 0m, 0m, longText);

        Assert.True(PageCount(AssertPdf(output)) >= 6);
    }

    [Fact]
    public void Export_EmptyQuoteCreatesPdf()
    {
        using var output = new MemoryStream();

        Export(output, []);

        Assert.Equal(1, PageCount(AssertPdf(output)));
    }

    [Fact]
    public void Export_NullOutputThrows()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => Export(null!, []));
        Assert.Equal("output", exception.ParamName);
    }

    [Fact]
    public void Export_ReadOnlyOutputThrows()
    {
        using var output = new MemoryStream([], writable: false);

        var exception = Assert.Throws<ArgumentException>(() => Export(output, []));

        Assert.Equal("output", exception.ParamName);
    }

    [Fact]
    public void Export_NullLinesThrowsBeforeWriting()
    {
        using var output = new MemoryStream();

        var exception = Assert.Throws<ArgumentNullException>(() => Export(output, null!));

        Assert.Equal("lines", exception.ParamName);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void Export_NullItemThrowsBeforeWriting()
    {
        using var output = new MemoryStream();

        var exception = Assert.Throws<ArgumentException>(() => Export(output, [null!]));

        Assert.Equal("lines", exception.ParamName);
        Assert.Equal(0, output.Length);
    }

    private static QuoteLineOption CreateLine(int index) => new(
        new QuoteItemDTO(index, 1, index, 2.5m, 19.90m, false),
        "Cortina de tecido", 49.75m, index, "Decoração");

    private static void Export(Stream output, IEnumerable<QuoteLineOption> lines) =>
        QuotePdfExporter.Export(output, 42, new DateTime(2026, 10, 6), "Cliente José",
            "Vendedora Ana", "Aberto", lines, 49.75m, 5m, "Entrega após aprovação.\nConferir medidas.");

    private static string AssertPdf(MemoryStream output)
    {
        var bytes = output.ToArray();
        Assert.True(bytes.Length > 5);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        var pdf = Encoding.Latin1.GetString(bytes);
        Assert.EndsWith("%%EOF", pdf.TrimEnd());
        return pdf;
    }

    private static int PageCount(string pdf) => Regex.Matches(pdf, @"/Type\s*/Page\b").Count;
}