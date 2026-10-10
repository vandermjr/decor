using System.Xml.Linq;

namespace Decor.Application.Tests;

public sealed class CrudActionLayoutTests
{
    private static readonly IReadOnlyDictionary<string, string> Colors = new Dictionary<string, string>
    {
        ["Delete"] = "DecorDangerBrush", ["Edit"] = "DecorEditIndicatorBrush",
        ["Create"] = "DecorAddIndicatorBrush", ["Add"] = "DecorAddIndicatorBrush",
        ["Save"] = "DecorAccentBrush", ["Cancel"] = "DecorDangerBrush"
    };

    [Fact]
    public void All_existing_crud_icons_use_semantic_dynamic_resources()
    {
        var count = 0;
        foreach (var file in Views())
        foreach (var icon in XDocument.Load(file).Descendants().Where(element => element.Name.LocalName is "Path" or "PathIcon"))
        {
            var id = icon.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "DecorIcon.Id")?.Value;
            foreach (var color in Colors)
            {
                if (id != $"{{x:Static icons:DecorIconId+Actions.{color.Key}}}") continue;
                Assert.Equal($"{{DynamicResource {color.Value}}}", (string?)icon.Attribute(icon.Name.LocalName == "Path" ? "Fill" : "Foreground"));
                count++;
            }
        }
        Assert.True(count >= 50);
    }

    [Fact]
    public void Commands_opt_in_without_blanket_button_or_form_field_policy()
    {
        foreach (var file in Views().Where(file => Path.GetFileName(file) is not "ProductsView.axaml" and not "ProductEditWindow.axaml"))
        {
            var document = XDocument.Load(file);
            Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Style"
                && (string?)element.Attribute("Selector") == "Button"
                && element.Elements().Any(setter => (string?)setter.Attribute("Property") == "IsVisible"));
            Assert.All(document.Descendants().Where(element => element.Name.LocalName == "Button" && element.Attribute("Command") is not null),
                button => Assert.Contains("decor-action", (string?)button.Attribute("Classes")));
            Assert.DoesNotContain(document.Descendants().Where(element => element.Name.LocalName != "Button"),
                element => ((string?)element.Attribute("Classes"))?.Split(' ').Contains("decor-action") == true);
        }
        var app = XDocument.Load(Path.Combine(UiRoot(), "App.axaml"));
        Assert.DoesNotContain(app.Descendants().Where(element => element.Name.LocalName == "Style" && (string?)element.Attribute("Selector") == "Button")
            .Descendants().Attributes(), attribute => attribute.Value.Contains("HideWhenDisabled"));
        var grid = XDocument.Load(Path.Combine(UiRoot(), "Controls/DecorDataGridControl.axaml"));
        Assert.DoesNotContain(grid.Descendants().Attributes(), attribute => attribute.Value.Contains("decor-action") || attribute.Value.Contains("HideWhenDisabled"));
    }

    [Fact]
    public void Toggle_forms_have_no_adjacent_state_label_but_quote_commercial_state_remains()
    {
        foreach (var file in Views())
            Assert.DoesNotContain(XDocument.Load(file).Descendants(), element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "Estado"
                && element.Parent!.Elements().Any(sibling => sibling.Name.LocalName == "ToggleSwitch"));
        Assert.Contains(XDocument.Load(Path.Combine(UiRoot(), "Views/QuotesView.axaml")).Descendants(),
            element => element.Name.LocalName == "TextBlock" && (string?)element.Attribute("Text") == "Estado");
    }

    [Fact]
    public void Common_delete_and_cancel_buttons_do_not_use_danger_backgrounds()
    {
        foreach (var file in Views())
        foreach (var button in XDocument.Load(file).Descendants().Where(element => element.Name.LocalName == "Button"))
        {
            var command = (string?)button.Attribute("Command") ?? "";
            if (!command.Contains("Delete") && !command.Contains("Cancel") && !command.Contains("Remove")) continue;
            var background = (string?)button.Attribute("Background") ?? "";
            Assert.DoesNotContain("DecorDangerBrush", background);
            Assert.NotEqual("#B3261E", background.ToUpperInvariant());
        }
    }

    private static IEnumerable<string> Views() => Directory.EnumerateFiles(Path.Combine(UiRoot(), "Views"), "*.axaml", SearchOption.AllDirectories);

    private static string UiRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Decor.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src/Decor.AvaloniaUI");
    }
}