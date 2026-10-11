using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Decor.AvaloniaUI.Controls;

public sealed class QuoteNumericInput : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<QuoteNumericInput, NumericUpDown, bool>("Enabled");

    static QuoteNumericInput()
    {
        EnabledProperty.Changed.AddClassHandler<NumericUpDown>((control, _) => UpdateHandlers(control));
    }

    private QuoteNumericInput()
    {
    }

    public static bool GetEnabled(NumericUpDown control) => control.GetValue(EnabledProperty);

    public static void SetEnabled(NumericUpDown control, bool value) => control.SetValue(EnabledProperty, value);

    public static bool IsValidInputText(string text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);

        var format = culture.NumberFormat;
        for (var index = 0; index < text.Length;)
        {
            var character = text[index];
            if (character is >= '0' and <= '9')
            {
                index++;
                continue;
            }

            var separatorLength = Math.Max(
                MatchSeparator(text, index, format.NumberDecimalSeparator),
                MatchSeparator(text, index, format.NumberGroupSeparator));
            if (separatorLength == 0)
                return false;

            index += separatorLength;
        }

        return true;
    }

    private static int MatchSeparator(string text, int index, string separator)
    {
        if (separator.Length == 0 || !text.AsSpan(index).StartsWith(separator.AsSpan(), StringComparison.Ordinal))
            return 0;

        foreach (var character in separator)
        {
            if (char.IsLetterOrDigit(character) || character is '+' or '-')
                return 0;
        }

        return separator.Length;
    }

    private static void UpdateHandlers(NumericUpDown control)
    {
        control.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
        control.RemoveHandler(TextBox.PastingFromClipboardEvent, OnPastingFromClipboard);
        if (!GetEnabled(control))
            return;

        control.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        control.AddHandler(TextBox.PastingFromClipboardEvent, OnPastingFromClipboard, RoutingStrategies.Bubble);
    }

    private static void OnTextInput(object? sender, TextInputEventArgs args)
    {
        if (args.Text is { } text && !IsValidInputText(text, CultureInfo.CurrentCulture))
            args.Handled = true;
    }

    private static async void OnPastingFromClipboard(object? sender, RoutedEventArgs args)
    {
        if (sender is not NumericUpDown control || args.Source is not TextBox textBox)
            return;

        args.Handled = true;
        var topLevel = TopLevel.GetTopLevel(textBox);
        var clipboard = topLevel?.Clipboard;
        if (clipboard is null || textBox.IsReadOnly || !textBox.IsEffectivelyEnabled)
            return;

        var originalText = textBox.Text;
        var selectionStart = textBox.SelectionStart;
        var selectionEnd = textBox.SelectionEnd;
        var caretIndex = textBox.CaretIndex;
        var culture = CultureInfo.CurrentCulture;
        string? pastedText;
        try
        {
            pastedText = await clipboard.TryGetTextAsync();
        }
        catch (Exception)
        {
            return;
        }

        if (string.IsNullOrEmpty(pastedText) || !IsValidInputText(pastedText, culture)
            || !GetEnabled(control) || textBox.IsReadOnly || !textBox.IsEffectivelyEnabled
            || !ReferenceEquals(TopLevel.GetTopLevel(textBox), topLevel)
            || textBox.Text != originalText || textBox.SelectionStart != selectionStart
            || textBox.SelectionEnd != selectionEnd || textBox.CaretIndex != caretIndex)
            return;

        var currentText = originalText ?? string.Empty;
        var start = Math.Min(selectionStart, selectionEnd);
        var end = Math.Max(selectionStart, selectionEnd);
        var candidate = currentText[..start] + pastedText + currentText[end..];
        if (IsValidInputText(candidate, culture))
            textBox.SelectedText = pastedText;
    }
}