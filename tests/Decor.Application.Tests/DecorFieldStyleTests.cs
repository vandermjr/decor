using System.Xml.Linq;

namespace Decor.Application.Tests;

public sealed class DecorFieldStyleTests
{
    [Theory]
    [InlineData("Light", "#000000")]
    [InlineData("Dark", "#FFFFFF")]
    public void Field_focus_has_a_theme_specific_neutral_brush(string theme, string color)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var dictionary = Assert.Single(LoadApp().Descendants(),
            element => (string?)element.Attribute(xaml + "Key") == theme);
        var brush = Assert.Single(dictionary.Elements(),
            element => (string?)element.Attribute(xaml + "Key") == "DecorFieldFocusBrush");

        Assert.Equal(color, brush.Value);
        Assert.Contains(dictionary.Elements(),
            element => (string?)element.Attribute(xaml + "Key") == "DecorAccentBrush");
    }

    [Theory]
    [InlineData("TextBox")]
    [InlineData("ComboBox")]
    [InlineData("NumericUpDown")]
    [InlineData("DatePicker")]
    public void Fields_have_a_one_pixel_base_border(string control)
    {
        var style = Assert.Single(LoadApp().Descendants(),
            element => element.Name.LocalName == "Style" && (string?)element.Attribute("Selector") == control);

        Assert.Contains(style.Elements(), element =>
            (string?)element.Attribute("Property") == "BorderThickness"
            && (string?)element.Attribute("Value") == "1");
    }

    private static XDocument LoadApp()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Decor.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(directory.FullName, "src", "Decor.AvaloniaUI", "App.axaml"));
    }
}