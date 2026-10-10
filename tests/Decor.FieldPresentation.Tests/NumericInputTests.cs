using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Decor.AvaloniaUI.Controls;

namespace Decor.FieldPresentation.Tests;

public sealed class NumericInputTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Product_quantity_outline_surrounds_editor_and_spinner(bool dark)
    {
        var view = new Decor.AvaloniaUI.Views.ProductsView();
        var window = new Window
        {
            Content = view,
            Width = 900,
            Height = 850,
            RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light
        };
        window.Show();
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var outlines = view.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("quantity-field")).ToArray();
            Assert.Equal(2, outlines.Length);
            foreach (var outline in outlines)
            {
                var editor = outline.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.True(editor.Focus());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(dark ? Avalonia.Media.Colors.White : Avalonia.Media.Colors.Black,
                    Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(outline.BorderBrush).Color);
                Assert.Equal(new Avalonia.Thickness(1), outline.BorderThickness);
                Assert.Equal(160, outline.Width);
                Assert.Equal(new Avalonia.Thickness(0), Assert.IsType<NumericUpDown>(outline.Child).BorderThickness);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Barcode_filters_typing_and_pasted_text_without_losing_zeroes()
    {
        var input = new TextBox();
        DecorNumericInput.SetDigitsOnly(input, true);
        input.Text = "001a23-45";
        Assert.Equal("0012345", input.Text);
        input.Text = "abc";
        Assert.Equal(string.Empty, input.Text);
    }

    [AvaloniaFact]
    public void Quantity_filters_inner_input_and_preserves_decimal_separator()
    {
        var input = new NumericUpDown { Minimum = 0 };
        DecorNumericInput.SetQuantity(input, true);
        var window = new Window { Content = input };
        window.Show();
        try
        {
            var textBox = input.GetVisualDescendants().OfType<TextBox>().Single();
            var separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            textBox.Text = $"12{separator}5abc";
            Assert.Equal($"12{separator}5", textBox.Text);
            textBox.Text = "abc-4";
            Assert.Equal("4", textBox.Text);
        }
        finally
        {
            window.Close();
        }
    }
}