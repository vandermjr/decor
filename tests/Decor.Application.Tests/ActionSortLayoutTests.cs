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

    [Fact]
    public void Main_menu_categories_have_no_icons_but_submenu_items_keep_them()
    {
        var document = Load("MainWindow.axaml");
        var mainMenu = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Menu"
            && ((string?)element.Attribute("Classes"))?.Split(' ').Contains("top-menu") == true);
        var mainMenuItems = mainMenu.Elements().Where(element => element.Name.LocalName == "MenuItem").ToArray();

        Assert.Equal(8, mainMenuItems.Length);
        Assert.DoesNotContain(mainMenuItems, item => item.Elements().Any(element => element.Name.LocalName == "PathIcon"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "DecorIcon.Id")));

        var submenuItems = mainMenu.Descendants().Where(element => element.Name.LocalName == "MenuItem");
        Assert.Contains(submenuItems, item => item.Descendants().Any(element => element.Name.LocalName == "PathIcon"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "DecorIcon.Id")));
    }

    [Fact]
    public void Main_menu_uses_native_submenus_instead_of_independent_flyouts()
    {
        var document = Load("MainWindow.axaml");
        var mainMenu = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Menu"
            && ((string?)element.Attribute("Classes"))?.Split(' ').Contains("top-menu") == true);

        Assert.DoesNotContain(mainMenu.Descendants(), element => element.Name.LocalName == "MenuFlyout");
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("Click") == "MainMenuButton_Click"
            || (string?)element.Attribute("PointerEntered") == "MainMenuButton_PointerEntered");
    }

    [Fact]
    public void Main_and_submenu_highlights_are_inset_and_rounded()
    {
        var document = Load("App.axaml");
        var menuItemStyle = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "MenuItem");
        var mainMenuItemStyle = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "MenuItem.main-menu-category");
        var mainMenuLayoutRootStyle = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "MenuItem.main-menu-category /template/ Border#PART_LayoutRoot");
        var submenuLayoutRootStyle = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "MenuItem.submenu-item /template/ Border#PART_LayoutRoot");

        Assert.Contains(menuItemStyle.Elements(), element => (string?)element.Attribute("Property") == "CornerRadius"
            && (string?)element.Attribute("Value") == "5");
        Assert.Contains(mainMenuLayoutRootStyle.Elements(), element => (string?)element.Attribute("Property") == "Margin"
            && (string?)element.Attribute("Value") == "4,5");
        Assert.Contains(mainMenuItemStyle.Elements(), element => (string?)element.Attribute("Property") == "Padding"
            && (string?)element.Attribute("Value") == "6,3");
        Assert.Contains(submenuLayoutRootStyle.Elements(), element => (string?)element.Attribute("Property") == "Margin"
            && (string?)element.Attribute("Value") == "4,4");
        var submenuItemStyle = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "MenuItem.submenu-item");
        Assert.Contains(submenuItemStyle.Elements(), element => (string?)element.Attribute("Property") == "Padding"
            && (string?)element.Attribute("Value") == "12,3");
        Assert.Contains(submenuLayoutRootStyle.Elements(), element => (string?)element.Attribute("Property") == "BorderThickness"
            && (string?)element.Attribute("Value") == "1");
        Assert.Contains(submenuLayoutRootStyle.Elements(), element => (string?)element.Attribute("Property") == "BorderBrush"
            && (string?)element.Attribute("Value") == "Transparent");
        Assert.DoesNotContain(document.Descendants().Attributes(), attribute => attribute.Name.LocalName == "Key"
            && attribute.Value == "DecorMenuSelectionBorderBrush");

        var mainMenu = Assert.Single(Load("MainWindow.axaml").Descendants(), element => element.Name.LocalName == "Menu"
            && ((string?)element.Attribute("Classes"))?.Split(' ').Contains("top-menu") == true);
        var categories = mainMenu.Elements().Where(element => element.Name.LocalName == "MenuItem").ToArray();
        Assert.All(categories, item => Assert.Contains("main-menu-category", ((string?)item.Attribute("Classes"))?.Split(' ') ?? []));
        var submenuItems = categories.SelectMany(category => category.Descendants().Where(element => element.Name.LocalName == "MenuItem"));
        Assert.All(submenuItems, item => Assert.Contains("submenu-item", ((string?)item.Attribute("Classes"))?.Split(' ') ?? []));
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