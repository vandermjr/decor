using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Decor.AvaloniaUI.Controls;

namespace Decor.Application.Tests;

public sealed class QuoteNumericInputTests
{
    [Theory]
    [InlineData("pt-BR", "")]
    [InlineData("pt-BR", "0")]
    [InlineData("pt-BR", "0123456789")]
    [InlineData("pt-BR", ",")]
    [InlineData("pt-BR", ".")]
    [InlineData("pt-BR", "12,")]
    [InlineData("pt-BR", ",5")]
    [InlineData("pt-BR", "1.234,56")]
    [InlineData("en-US", ".")]
    [InlineData("en-US", ",")]
    [InlineData("en-US", "12.")]
    [InlineData("en-US", ".5")]
    [InlineData("en-US", "1,234.56")]
    [InlineData("en-US", "1..2")]
    public void AllowedChunksAndIntermediateEdits_AreAccepted(string cultureName, string text)
    {
        Assert.True(QuoteNumericInput.IsValidInputText(text, CultureInfo.GetCultureInfo(cultureName)));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("e")]
    [InlineData("E")]
    [InlineData("1e3")]
    [InlineData("1E3")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("12abc34")]
    [InlineData("1/2")]
    [InlineData("$12")]
    [InlineData(" 12")]
    [InlineData("12 ")]
    [InlineData("1\t2")]
    [InlineData("1\n2")]
    [InlineData("\u0661\u0662")]
    [InlineData("\uff11\uff12")]
    public void InvalidChunks_AreRejectedEntirely(string text)
    {
        Assert.False(QuoteNumericInput.IsValidInputText(text, CultureInfo.GetCultureInfo("pt-BR")));
        Assert.False(QuoteNumericInput.IsValidInputText(text, CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void Whitespace_IsAllowedOnlyWhenItIsTheConfiguredSeparator()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = "\u202f";

        Assert.True(QuoteNumericInput.IsValidInputText("1\u202f234.5", culture));
        Assert.False(QuoteNumericInput.IsValidInputText("1 234.5", culture));
        Assert.False(QuoteNumericInput.IsValidInputText("1\u00a0234.5", culture));
    }

    [Fact]
    public void MultiCharacterSeparators_MustMatchTheWholeToken()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = "::";
        culture.NumberFormat.NumberGroupSeparator = "_";

        Assert.True(QuoteNumericInput.IsValidInputText("1_234::5", culture));
        Assert.False(QuoteNumericInput.IsValidInputText("1:5", culture));
    }

    [Theory]
    [InlineData("e")]
    [InlineData("E")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("\u0661")]
    public void CustomSeparators_CannotAllowLettersSignsOrNonAsciiDigits(string separator)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = separator;

        Assert.False(QuoteNumericInput.IsValidInputText("1" + separator + "2", culture));
    }

    [Fact]
    public void Enabled_IsOptInAndCanBeDisabled()
    {
        var control = new NumericUpDown();
        Assert.False(QuoteNumericInput.GetEnabled(control));
        Assert.False(RaiseTextInput(control, "hello").Handled);

        QuoteNumericInput.SetEnabled(control, true);
        Assert.True(RaiseTextInput(control, "hello").Handled);
        Assert.False(RaiseTextInput(control, "123").Handled);

        QuoteNumericInput.SetEnabled(control, false);
        Assert.False(RaiseTextInput(control, "hello").Handled);
    }

    [Fact]
    public void PasteFromInternalTextBox_IsCancelledSynchronouslyWithoutClipboard()
    {
        var textBox = new TextBox();
        var control = new NumericUpDown
        {
            Template = new FuncControlTemplate<NumericUpDown>((_, _) => textBox)
        };
        QuoteNumericInput.SetEnabled(control, true);
        control.ApplyTemplate();

        var args = new RoutedEventArgs(TextBox.PastingFromClipboardEvent);
        textBox.RaiseEvent(args);
        Assert.True(args.Handled);

        QuoteNumericInput.SetEnabled(control, false);
        args = new RoutedEventArgs(TextBox.PastingFromClipboardEvent);
        textBox.RaiseEvent(args);
        Assert.False(args.Handled);
    }

    private static TextInputEventArgs RaiseTextInput(NumericUpDown control, string text)
    {
        var args = new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text };
        control.RaiseEvent(args);
        return args;
    }
}