using System.Xml.Linq;

namespace Decor.Application.Tests;

public class ActionSortLayoutTests
{
    [Fact]
    public void About_uses_information_in_menu_and_document()
    {
        var menu = Load("MainWindow.axaml").Descendants().Single(element =>
            (string?)element.Attribute("Command") == "{Binding ShowAboutCommand}");
        Assert.Contains(menu.Descendants().Attributes(), attribute => attribute.Name.LocalName == "DecorIcon.Id"
            && attribute.Value == "{x:Static icons:DecorIconId+Application.Info}");
        var document = new Decor.AvaloniaUI.ViewModels.WorkspaceDocumentViewModel("about", "About",
            new Avalonia.Controls.Border(), _ => { }, _ => { });
        Assert.Equal(Decor.AvaloniaUI.Icons.DecorIconId.Application.Info, document.IconId);
    }

    [Theory]
    [InlineData("Quotes")]
    [InlineData("Brands")]
    public void Disabled_actions_are_hidden_by_scoped_policy(string view)
    {
        var document = Load($"Views/{view}View.axaml");
        Assert.All(document.Descendants().Where(element => element.Name.LocalName == "Button"
            && element.Attribute("Command") is not null), button => Assert.Contains("decor-action", (string?)button.Attribute("Classes")));
        var style = Load("App.axaml").Descendants().Single(element => element.Name.LocalName == "Style"
            && ((string?)element.Attribute("Selector"))?.Contains("Button.decor-action") == true);
        Assert.Contains(style.Elements(), element => (string?)element.Attribute("Property") == "IsVisible"
            || element.Attributes().Any(attribute => attribute.Value == "controls:DecorActionAvailability.HideWhenDisabled")
                && (string?)element.Attribute("Value") == "True");
    }

    [Theory]
    [InlineData("New", "Create")]
    [InlineData("Edit", "Edit")]
    public void Quote_listing_actions_have_icons_and_hide_when_unavailable(string action, string icon)
    {
        var document = Load("Views/QuotesView.axaml");
        var button = document.Descendants().Single(element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == $"{{Binding {action}Command}}");
        Assert.Equal($"{{Binding Can{action}}}", (string?)button.Attribute("IsVisible"));
        Assert.Contains(button.Descendants().Attributes(), attribute => attribute.Name.LocalName == "DecorIcon.Id"
            && attribute.Value == $"{{x:Static icons:DecorIconId+Actions.{icon}}}");
    }

    [Fact]
    public void All_quote_command_buttons_have_icons()
    {
        var buttons = Load("Views/QuotesView.axaml").Descendants().Where(element =>
            element.Name.LocalName == "Button" && element.Attribute("Command") is not null);
        Assert.NotEmpty(buttons);
        foreach (var button in buttons)
            Assert.Contains(button.Descendants().Attributes(), attribute => attribute.Name.LocalName == "DecorIcon.Id");
    }

    private static XDocument Load(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Decor.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(directory.FullName, "src/Decor.AvaloniaUI", relativePath));
    }
}