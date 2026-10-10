using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Decor.AvaloniaUI.Controls;

namespace Decor.Application.Tests;

public sealed class DecorRequiredFieldTests
{
    [Fact]
    public void Required_is_explicit_and_never_a_validation_error()
    {
        var field = new TextBox();
        Assert.False(DecorRequiredField.GetIsRequired(field));
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetHelpText(field)));

        DecorRequiredField.SetIsRequired(field, true);

        Assert.True(DecorRequiredField.GetIsRequired(field));
        Assert.Equal("Campo obrigatorio", AutomationProperties.GetHelpText(field));
        Assert.False(DataValidationErrors.GetHasErrors(field));
        Assert.DoesNotContain(":error", field.Classes);

        DecorRequiredField.SetIsRequired(field, false);

        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetHelpText(field)));
        Assert.False(DataValidationErrors.GetHasErrors(field));
    }

    [Fact]
    public void Existing_help_text_is_not_overwritten()
    {
        var field = new ComboBox();
        AutomationProperties.SetHelpText(field, "Escolha a unidade");

        DecorRequiredField.SetIsRequired(field, true);
        Assert.Equal("Escolha a unidade", AutomationProperties.GetHelpText(field));
        DecorRequiredField.SetIsRequired(field, false);
        Assert.Equal("Escolha a unidade", AutomationProperties.GetHelpText(field));
    }

    [Fact]
    public void Later_help_text_remains_after_required_is_removed()
    {
        var field = new NumericUpDown();
        DecorRequiredField.SetIsRequired(field, true);
        AutomationProperties.SetHelpText(field, "Informe a quantidade");

        Assert.Equal("Informe a quantidade", AutomationProperties.GetHelpText(field));
        DecorRequiredField.SetIsRequired(field, false);
        Assert.Equal("Informe a quantidade", AutomationProperties.GetHelpText(field));
    }

    [Fact]
    public void Existing_styled_help_text_is_not_overwritten()
    {
        var field = new DatePicker();
        using var help = field.SetValue(AutomationProperties.HelpTextProperty,
            "Data de entrega", BindingPriority.Style);
        DecorRequiredField.SetIsRequired(field, true);

        Assert.Equal("Data de entrega", AutomationProperties.GetHelpText(field));
        DecorRequiredField.SetIsRequired(field, false);
        Assert.Equal("Data de entrega", AutomationProperties.GetHelpText(field));
    }

    [Fact]
    public void Lookup_border_can_be_flagged_without_flagging_its_children()
    {
        var input = new TextBox { BorderThickness = new Thickness(0) };
        var border = new Border { Child = input };

        for (var cycle = 0; cycle < 3; cycle++)
        {
            DecorRequiredField.SetIsRequired(border, true);
            Assert.Equal("Campo obrigatorio", AutomationProperties.GetHelpText(border));
            Assert.False(DecorRequiredField.GetIsRequired(input));
            Assert.Equal(new Thickness(0), input.BorderThickness);
            DecorRequiredField.SetIsRequired(border, false);
            Assert.True(string.IsNullOrEmpty(AutomationProperties.GetHelpText(border)));
        }
    }

    [Fact]
    public void No_layer_host_does_not_add_space_to_the_field()
    {
        var field = new Border { Width = 120, Height = 32 };
        var host = new DecorRequiredFieldHost { Children = { field } };
        DecorRequiredField.SetIsRequired(field, true);

        host.Measure(new Size(200, 100));
        host.Arrange(new Rect(host.DesiredSize));

        Assert.Equal(new Size(120, 32), host.DesiredSize);
        Assert.Equal(new Rect(0, 0, 120, 32), field.Bounds);
        Assert.Single(host.Children);
        DecorRequiredField.SetIsRequired(field, false);
    }
}