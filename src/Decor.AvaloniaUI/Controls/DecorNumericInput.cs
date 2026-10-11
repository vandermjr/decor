using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Decor.AvaloniaUI.Controls;

public sealed class DecorNumericInput : AvaloniaObject
{
    public static readonly AttachedProperty<bool> DigitsOnlyProperty =
        AvaloniaProperty.RegisterAttached<DecorNumericInput, Control, bool>("DigitsOnly");
    public static readonly AttachedProperty<bool> QuantityProperty =
        AvaloniaProperty.RegisterAttached<DecorNumericInput, Control, bool>("Quantity");

    static DecorNumericInput()
    {
        DigitsOnlyProperty.Changed.AddClassHandler<Control>((control, _) => Configure(control));
        QuantityProperty.Changed.AddClassHandler<Control>((control, _) => Configure(control));
    }

    public static bool GetDigitsOnly(Control control) => control.GetValue(DigitsOnlyProperty);
    public static void SetDigitsOnly(Control control, bool value) => control.SetValue(DigitsOnlyProperty, value);
    public static bool GetQuantity(Control control) => control.GetValue(QuantityProperty);
    public static void SetQuantity(Control control, bool value) => control.SetValue(QuantityProperty, value);

    private static void Configure(Control control)
    {
        if (control is TextBox textBox)
        {
            textBox.TextChanging -= ValidateText;
            if (GetDigitsOnly(control) || GetQuantity(control))
                textBox.TextChanging += ValidateText;
        }
        else if (control is NumericUpDown numeric)
        {
            numeric.TemplateApplied -= ConfigureTemplate;
            numeric.TemplateApplied += ConfigureTemplate;
            ConfigureInnerInput(numeric);
        }
    }

    private static void ConfigureTemplate(object? sender, TemplateAppliedEventArgs args)
    {
        if (sender is NumericUpDown numeric && args.NameScope.Find<TextBox>("PART_TextBox") is { } input)
            SetQuantity(input, GetQuantity(numeric));
    }

    private static void ConfigureInnerInput(NumericUpDown numeric)
    {
        var input = numeric.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        if (input is not null)
            SetQuantity(input, GetQuantity(numeric));
    }

    private static void ValidateText(object? sender, TextChangingEventArgs args)
    {
        if (sender is not TextBox input || input.Text is not { } text)
            return;

        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var hasSeparator = false;
        var filtered = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is >= '0' and <= '9')
                filtered.Append(character);
            else if (GetQuantity(input) && !hasSeparator && separator.Contains(character))
            {
                filtered.Append(character);
                hasSeparator = true;
            }
        }

        var result = filtered.ToString();
        if (result != text)
            input.SetCurrentValue(TextBox.TextProperty, result);
    }
}