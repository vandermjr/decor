using System.Xml.Linq;

namespace Decor.Application.Tests;

public sealed class LoginBrandingLayoutTests
{
    [Fact]
    public void Login_logo_is_configurable_from_global_appearance_settings()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        var repositoryPath = root?.FullName ?? throw new InvalidOperationException("Decor.sln not found.");

        var options = XDocument.Load(Path.Combine(repositoryPath, "src", "Decor.AvaloniaUI", "Views", "IconCatalogView.axaml"));
        var logoOption = Assert.Single(options.Descendants(), element => element.Name.LocalName == "TreeViewItem"
            && (string?)element.Attribute("Tag") == "LoginBranding");
        Assert.Equal("Logo da empresa", (string?)logoOption.Attribute("Header"));
        Assert.Equal("Aparência", (string?)logoOption.Parent?.Attribute("Header"));
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "TextBlock"
            && ((string?)element.Attribute("Text"))?.Contains("1200 × 760 px", StringComparison.Ordinal) == true
            && ((string?)element.Attribute("Text"))?.Contains("PNG", StringComparison.Ordinal) == true
            && ((string?)element.Attribute("Text"))?.Contains("JPEG", StringComparison.Ordinal) == true
            && ((string?)element.Attribute("Text"))?.Contains("10 MB", StringComparison.Ordinal) == true);
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Click") == "ChooseLoginLogoButton_Click");
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "Slider"
            && ((string?)element.Attribute("Value"))?.Contains("LoginLogoSizePercent", StringComparison.Ordinal) == true);
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "CheckBox"
            && (string?)element.Attribute("Content") == "Inverter cores da logo no tema claro"
            && ((string?)element.Attribute("IsChecked"))?.Contains("InvertLoginLogoInLightTheme", StringComparison.Ordinal) == true);
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "Image"
            && ((string?)element.Attribute("Source"))?.Contains("LoginLogoPreview", StringComparison.Ordinal) == true);
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "Prévia");
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "TextBlock"
            && ((string?)element.Attribute("Text"))?.Contains("ajustar a posição da imagem", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(options.Descendants(), element => element.Name.LocalName == "ComboBox"
            && ((string?)element.Attribute("SelectedItem"))?.Contains("LoginLogo", StringComparison.Ordinal) == true);
        foreach (var direction in new[] { "Up", "Down", "Left", "Right" })
        {
            var arrow = Assert.Single(options.Descendants(), element => element.Name.LocalName == "Button"
                && ((string?)element.Attribute("Command"))?.Contains($"MoveLoginLogo{direction}Command", StringComparison.Ordinal) == true
                && ((string?)element.Attribute("IsEnabled"))?.Contains($"CanMoveLoginLogo{direction}", StringComparison.Ordinal) == true);
            Assert.Contains("logo-position-button", (string?)arrow.Attribute("Classes") ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("decor-action", (string?)arrow.Attribute("Classes") ?? string.Empty, StringComparison.Ordinal);
        }
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "Button.logo-position-button:disabled");
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "Button"
            && ((string?)element.Attribute("Command"))?.Contains("RemoveLoginLogoCommand", StringComparison.Ordinal) == true);
        var brandingSection = Assert.Single(options.Descendants(), element => element.Name.LocalName == "StackPanel"
            && ((string?)element.Attribute("IsVisible"))?.Contains("IsLoginBrandingSelected", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(brandingSection.Descendants(), element => element.Name.LocalName == "Button"
            && ((string?)element.Attribute("Content"))?.Contains("Restaurar", StringComparison.Ordinal) == true);

        var login = XDocument.Load(Path.Combine(repositoryPath, "src", "Decor.AvaloniaUI", "Views", "LoginWindow.axaml"));
        Assert.Contains(login.Descendants(), element => element.Name.LocalName == "Image"
            && (string?)element.Attribute("Width") == "{Binding LoginLogoWidth}"
            && (string?)element.Attribute("Grid.Column") == "1"
            && (string?)element.Attribute("HorizontalAlignment") == "{Binding LoginLogoHorizontalAlignment}"
            && (string?)element.Attribute("VerticalAlignment") == "{Binding LoginLogoVerticalAlignment}"
            && (string?)element.Attribute("Source") == "{Binding LoginLogo}");
        Assert.Contains(login.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Grid.Column") == "1"
            && (string?)element.Attribute("Background") == "{DynamicResource LoginImageColumnBrush}");
        var app = XDocument.Load(Path.Combine(repositoryPath, "src", "Decor.AvaloniaUI", "App.axaml"));
        Assert.Contains(app.Descendants(), element => element.Name.LocalName == "SolidColorBrush"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "LoginImageColumnBrush"
            && ((string?)element.Value ?? string.Empty).Contains("#FFFFFF", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(app.Descendants(), element => element.Name.LocalName == "SolidColorBrush"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "LoginImageColumnBrush"
            && ((string?)element.Value ?? string.Empty).Contains("#000000", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(app.Descendants(), element => element.Name.LocalName == "SolidColorBrush"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "LoginGlassFieldTextBrush"
            && ((string?)element.Value ?? string.Empty).Contains("#252A30", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(app.Descendants(), element => element.Name.LocalName == "SolidColorBrush"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "LoginGlassFieldTextBrush"
            && ((string?)element.Value ?? string.Empty).Contains("#FFFFFFFF", StringComparison.OrdinalIgnoreCase));
        var rootGrid = Assert.Single(login.Root!.Elements(), element => element.Name.LocalName == "Grid");
        Assert.Equal("360,*", (string?)rootGrid.Attribute("ColumnDefinitions"));
        Assert.Equal("LeftToRight", (string?)rootGrid.Attribute("FlowDirection"));
        var previewGrid = Assert.Single(options.Descendants(), element => element.Name.LocalName == "Grid"
            && ((string?)element.Attribute("ColumnDefinitions")) == "124,*");
        Assert.Equal("LeftToRight", (string?)previewGrid.Attribute("FlowDirection"));
        Assert.Equal("320", (string?)previewGrid.Parent?.Attribute("Width"));
        Assert.Equal("193", (string?)previewGrid.Parent?.Attribute("Height"));
        Assert.Contains(previewGrid.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Background") == "{DynamicResource LoginGlassPanelBrush}");
        Assert.Equal(2, previewGrid.Descendants().Count(element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Background") == "{DynamicResource LoginGlassFieldBrush}"));
        Assert.Contains(previewGrid.Descendants(), element => element.Name.LocalName == "Border"
            && ((string?)element.Attribute("BoxShadow"))?.Contains("#80000000", StringComparison.Ordinal) == true);
        Assert.Contains(previewGrid.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "Versão 1.0.0.0");
        var leftPanel = Assert.Single(rootGrid.Elements(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Grid.Column") == "0");
        Assert.Null(leftPanel.Attribute("BorderBrush"));
        Assert.Null(leftPanel.Attribute("BorderThickness"));
        var form = Assert.Single(leftPanel.Descendants(), element => element.Name.LocalName == "Grid"
            && ((string?)element.Attribute("MaxWidth")) == "220");
        Assert.Equal("Center", (string?)form.Attribute("HorizontalAlignment"));
        var textFields = form.Elements().Where(element => element.Name.LocalName == "TextBox").ToArray();
        Assert.Equal(2, textFields.Length);
        Assert.All(textFields, field => Assert.Same(form, field.Parent));
        var loginButtonFrame = Assert.Single(form.Elements(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Grid.Row") == "5");
        Assert.Contains("0 2 8 0 #80000000", (string?)loginButtonFrame.Attribute("BoxShadow") ?? string.Empty, StringComparison.Ordinal);
        var loginButton = Assert.Single(loginButtonFrame.Elements(), element => element.Name.LocalName == "Button");
        Assert.Contains("login-primary", ((string?)loginButton.Attribute("Classes")) ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(login.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "Button.login-primary");
        Assert.Contains(login.Descendants(), element => element.Name.LocalName == "Style"
            && ((string?)element.Attribute("Selector"))?.Contains("Button.login-primary:pointerover", StringComparison.Ordinal) == true
            && element.Elements().Any(setter => setter.Name.LocalName == "Setter"
                && (string?)setter.Attribute("Property") == "Background"
                && (string?)setter.Attribute("Value") == "#FF9B76"));
        Assert.Contains(login.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "Button.login-primary:focus"
            && element.Elements().Any(setter => setter.Name.LocalName == "Setter"
                && (string?)setter.Attribute("Property") == "Background"
                && (string?)setter.Attribute("Value") == "#FF9B76")
            && element.Elements().Any(setter => setter.Name.LocalName == "Setter"
                && (string?)setter.Attribute("Property") == "Foreground"
                && (string?)setter.Attribute("Value") == "#34231D"));
        Assert.Contains(login.Descendants(), element => element.Name.LocalName == "Style"
            && ((string?)element.Attribute("Selector"))?.Contains("Button.login-primary:pressed", StringComparison.Ordinal) == true);
        Assert.Contains(login.Descendants(), element => element.Name.LocalName == "Style"
            && (string?)element.Attribute("Selector") == "TextBox.login-field"
            && element.Elements().Any(setter => setter.Name.LocalName == "Setter"
                && (string?)setter.Attribute("Property") == "CornerRadius"
                && (string?)setter.Attribute("Value") == "14"));
        Assert.DoesNotContain(login.Descendants(), element => element.Name.LocalName == "TextBox"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "IsRequired"));
        var footer = Assert.Single(leftPanel.Descendants(), element => element.Name.LocalName == "Border"
            && ((string?)element.Attribute("Grid.Row")) == "1");
        Assert.Equal("{DynamicResource DecorBorderBrush}", (string?)footer.Attribute("BorderBrush"));
        Assert.Null(footer.Attribute("Margin"));
        Assert.Contains(leftPanel.Descendants(), element => element.Name.LocalName == "TextBlock"
            && ((string?)element.Attribute("Text"))?.Contains("SystemVersion", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(login.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "Logo do cliente");

        var loginCodeBehind = File.ReadAllText(Path.Combine(repositoryPath, "src", "Decor.AvaloniaUI", "Views", "LoginWindow.axaml.cs"));
        Assert.Contains("viewModel.SetPreviewTheme(GetCurrentTheme());", loginCodeBehind, StringComparison.Ordinal);
        Assert.Contains("application?.RequestedThemeVariant", loginCodeBehind, StringComparison.Ordinal);
        Assert.Contains("application?.ActualThemeVariant", loginCodeBehind, StringComparison.Ordinal);
    }
}