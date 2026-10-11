using System.Globalization;
using System.Text;
using Decor.AvaloniaUI.ViewModels;
using SkiaSharp;

namespace Decor.AvaloniaUI.Services;

public static class QuotePdfExporter
{
    public static void Export(Stream output, int quoteId, DateTime date, string customer,
        string seller, string state, IEnumerable<QuoteLineOption> lines, decimal subtotal,
        decimal discount, string notes)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
            throw new ArgumentException("O stream deve permitir escrita.", nameof(output));
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(seller);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(notes);
        var items = lines.ToArray();
        if (items.Any(item => item is null || item.DTO is null))
            throw new ArgumentException("Os itens e seus DTOs não podem ser nulos.", nameof(lines));

        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Não foi possível criar o PDF.");
        using var layout = new PdfLayout(document, quoteId);
        layout.StartPage();
        layout.WriteText("Lançamento do orçamento", layout.Title);
        layout.WriteText($"Número: {quoteId}    Data: {date:dd/MM/yyyy}");
        layout.WriteText($"Cliente: {customer}");
        layout.WriteText($"Vendedor: {seller}");
        layout.WriteText($"Estado: {state}");
        layout.WriteTableHeader();
        foreach (var item in items)
            layout.WriteItem(item);
        layout.WriteText($"Subtotal: {subtotal.ToString("C2", PdfLayout.Culture)}");
        layout.WriteText($"Desconto: {discount.ToString("C2", PdfLayout.Culture)}");
        layout.WriteText($"Total: {(subtotal - discount).ToString("C2", PdfLayout.Culture)}", layout.Heading);
        layout.WriteText("Observações", layout.Heading);
        layout.WriteText(notes);
        layout.EndPage();
        document.Close();
    }

    private sealed class PdfLayout : IDisposable
    {
        public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");
        private const float PageWidth = 595.28f;
        private const float PageHeight = 841.89f;
        private const float Margin = 36f;
        private const float ContentBottom = PageHeight - Margin - 24f;
        private const float LineHeight = 15f;
        private const float CellPadding = 4f;
        private static readonly float[] ColumnWidths = [30f, 169.28f, 70f, 82f, 70f, 102f];
        private readonly SKDocument _document;
        private readonly int _quoteId;
        private readonly SKTypeface _typeface = SKTypeface.FromFamilyName("DejaVu Sans");
        private readonly SKPaint _body = new() { IsAntialias = true, Color = SKColors.Black };
        private readonly SKFont _bodyFont;
        private readonly SKPaint _rule = new() { Color = new SKColor(190, 190, 190), StrokeWidth = 0.5f };
        public SKFont Title { get; }
        public SKFont Heading { get; }
        private SKCanvas _canvas = null!;
        private float _cursor;
        private int _pageNumber;

        public PdfLayout(SKDocument document, int quoteId)
        {
            _document = document;
            _quoteId = quoteId;
            _bodyFont = CreateFont(9f);
            Title = CreateFont(17f);
            Heading = CreateFont(11f);
        }

        private SKFont CreateFont(float size) => new(_typeface, size)
        {
            Edging = SKFontEdging.Antialias
        };

        public void StartPage()
        {
            _canvas = _document.BeginPage(PageWidth, PageHeight);
            _cursor = Margin;
            _pageNumber++;
            if (_pageNumber > 1)
                WriteText($"Orçamento nº {_quoteId} (continuação)", Heading);
        }

        public void EndPage()
        {
            _canvas.DrawLine(Margin, ContentBottom + 8f, PageWidth - Margin, ContentBottom + 8f, _rule);
            var footer = $"Página {_pageNumber}";
            _canvas.DrawText(footer, PageWidth - Margin - _bodyFont.MeasureText(footer, _body),
                PageHeight - Margin, SKTextAlign.Left, _bodyFont, _body);
            _document.EndPage();
        }

        private void NextPage(bool table)
        {
            EndPage();
            StartPage();
            if (table)
                WriteTableHeader();
        }

        public void WriteText(string text, SKFont? font = null)
        {
            font ??= _bodyFont;
            var height = Math.Max(LineHeight, font.Spacing + 3f);
            foreach (var line in Wrap(text, PageWidth - 2f * Margin, font, _body))
            {
                if (_cursor + height > ContentBottom)
                    NextPage(false);
                _canvas.DrawText(line, Margin, _cursor - font.Metrics.Ascent, SKTextAlign.Left, font, _body);
                _cursor += height;
            }
            _cursor += 6f;
        }

        public void WriteTableHeader()
        {
            var cells = new[] { "Item", "Descrição", "Categoria", "Preço Un.", "Quantidade", "Valor Total" };
            var wrapped = WrapCells(cells);
            var count = wrapped.Max(cell => cell.Count);
            var height = count * LineHeight + 2f * CellPadding;
            if (_cursor + height + LineHeight + 2f * CellPadding > ContentBottom)
                NextPage(false);
            _canvas.DrawLine(Margin, _cursor, PageWidth - Margin, _cursor, _rule);
            DrawCells(wrapped, 0, count);
        }

        public void WriteItem(QuoteLineOption item)
        {
            var cells = new[]
            {
                item.Item.ToString(Culture), item.ProductName, item.Category,
                item.UnitPrice.ToString("C2", Culture), item.Quantity.ToString("0.###", Culture),
                item.Total.ToString("C2", Culture)
            };
            var wrapped = WrapCells(cells);
            var count = wrapped.Max(cell => cell.Count);
            var fullHeight = count * LineHeight + 2f * CellPadding;
            if (_cursor + fullHeight > ContentBottom && fullHeight <= ContentBottom - Margin - 70f)
                NextPage(true);
            var offset = 0;
            while (offset < count)
            {
                var available = (int)((ContentBottom - _cursor - 2f * CellPadding) / LineHeight);
                if (available < 1)
                {
                    NextPage(true);
                    continue;
                }
                var length = Math.Min(available, count - offset);
                DrawCells(wrapped, offset, length);
                offset += length;
                if (offset < count)
                    NextPage(true);
            }
        }

        private List<string>[] WrapCells(string[] cells) => cells
            .Select((text, index) => Wrap(text, ColumnWidths[index] - 2f * CellPadding, _bodyFont, _body))
            .ToArray();

        private void DrawCells(List<string>[] cells, int offset, int count)
        {
            var left = Margin;
            for (var column = 0; column < cells.Length; column++)
            {
                for (var index = offset; index < Math.Min(offset + count, cells[column].Count); index++)
                {
                    var text = cells[column][index];
                    var horizontal = column >= 3
                        ? left + ColumnWidths[column] - CellPadding - _bodyFont.MeasureText(text, _body)
                        : left + CellPadding;
                    _canvas.DrawText(text, horizontal,
                        _cursor + CellPadding + (index - offset) * LineHeight - _bodyFont.Metrics.Ascent,
                        SKTextAlign.Left, _bodyFont, _body);
                }
                left += ColumnWidths[column];
            }
            _cursor += count * LineHeight + 2f * CellPadding;
            _canvas.DrawLine(Margin, _cursor, PageWidth - Margin, _cursor, _rule);
        }

        private static List<string> Wrap(string text, float width, SKFont font, SKPaint paint)
        {
            var result = new List<string>();
            foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = string.Empty;
                foreach (var word in paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = line.Length == 0 ? word : $"{line} {word}";
                    if (font.MeasureText(candidate, paint) <= width)
                    {
                        line = candidate;
                        continue;
                    }
                    if (line.Length > 0)
                    {
                        result.Add(line);
                        line = string.Empty;
                    }
                    foreach (var rune in word.EnumerateRunes())
                    {
                        candidate = line + rune;
                        if (line.Length > 0 && font.MeasureText(candidate, paint) > width)
                        {
                            result.Add(line);
                            line = string.Empty;
                        }
                        line += rune;
                    }
                }
                result.Add(line);
            }
            return result;
        }

        public void Dispose()
        {
            _body.Dispose();
            _bodyFont.Dispose();
            Title.Dispose();
            Heading.Dispose();
            _rule.Dispose();
            _typeface.Dispose();
        }
    }
}