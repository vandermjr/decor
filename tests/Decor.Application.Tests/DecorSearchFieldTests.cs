using System.Xml.Linq;

namespace Decor.Application.Tests;

public sealed class DecorSearchFieldTests
{
    [Fact]
    public void Single_border_and_local_template_styles_leave_focus_brush_overridable()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        var document = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Controls", "DecorSearchField.axaml"));
        var border = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Border");
        Assert.Equal("1", (string?)border.Attribute("BorderThickness"));
        Assert.Null(border.Attribute("BorderBrush"));
        Assert.Equal("36", (string?)document.Root!.Attribute("Height"));
        var styles = document.Descendants().Where(element => element.Name.LocalName == "Style").ToArray();
        var normal = Assert.Single(styles, element => (string?)element.Attribute("Selector") == "Border#FieldBorder");
        Assert.Equal("{DynamicResource DecorBorderBrush}", (string?)normal.Elements().Single().Attribute("Value"));
        var focus = Assert.Single(styles, element => (string?)element.Attribute("Selector") == "Border#FieldBorder:focus-within");
        Assert.Equal("{DynamicResource DecorFieldFocusBrush}", (string?)focus.Elements().Single().Attribute("Value"));
        var innerFocus = Assert.Single(styles, element => ((string?)element.Attribute("Selector"))?.Contains("TextBox.search-input:focus /template/ Border#PART_BorderElement") == true);
        Assert.Contains(innerFocus.Elements(), setter => (string?)setter.Attribute("Property") == "BorderThickness" && (string?)setter.Attribute("Value") == "0");
        var buttonStyle = Assert.Single(styles, element => (string?)element.Attribute("Selector") == "Button.search-action");
        Assert.Contains(buttonStyle.Elements(), setter => (string?)setter.Attribute("Property") == "Width" && (string?)setter.Attribute("Value") == "36");
        Assert.Contains(buttonStyle.Elements(), setter => (string?)setter.Attribute("Property") == "Height" && (string?)setter.Attribute("Value") == "32");
        Assert.Contains(buttonStyle.Elements(), setter => (string?)setter.Attribute("Property") == "FocusAdorner" && (string?)setter.Attribute("Value") == "{x:Null}");
    }

}