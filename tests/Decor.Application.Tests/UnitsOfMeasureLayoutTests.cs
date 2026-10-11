using System.Xml.Linq;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.Application.Tests;

public sealed class UnitsOfMeasureLayoutTests
{
    [Fact]
    public void CatalogUsesSharedGridAndMaintainsCoreUnitFields()
    {
        var document = XDocument.Load(Path.Combine(SourceDirectory(), "src", "Decor.AvaloniaUI", "Views", "UnitsOfMeasureView.axaml"));
        var grid = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorDataGridControl");
        Assert.Equal("{Binding Units}", (string?)grid.Attribute("ItemsSource"));
        Assert.Equal("{Binding}", (string?)grid.Attribute("PaginationSource"));
        Assert.Equal("Code", (string?)grid.Attribute("DefaultSortMemberPath"));
        var search = Assert.Single(document.Descendants(), element => element.Name.LocalName == "DecorSearchField");
        Assert.Equal("{Binding SearchCommand}", (string?)search.Attribute("SearchCommand"));
        Assert.Equal("{Binding ClearSearchCommand}", (string?)search.Attribute("ClearCommand"));
        foreach (var field in new[] { "Code", "Description", "AllowsFraction" })
            Assert.Contains(document.Descendants().Attributes(), attribute => attribute.Value == $"{{Binding {field}}}");
        foreach (var command in new[] { "NewCommand", "EditCommand", "SaveCommand", "CancelCommand" })
            Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{Binding {command}}}");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "CheckBox" && (string?)element.Attribute("IsChecked") == "{Binding AllowsFraction}");
        var status = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ToggleSwitch");
        Assert.Equal("{Binding IsActive, Mode=TwoWay}", (string?)status.Attribute("IsChecked"));
        Assert.Equal("{Binding CanChangeActive}", (string?)status.Attribute("IsEnabled"));
        Assert.Equal("Ativo", (string?)status.Attribute("OnContent"));
        Assert.Equal("Inativo", (string?)status.Attribute("OffContent"));
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") is "{Binding ActivateCommand}" or "{Binding DeactivateCommand}");
        Assert.DoesNotContain(document.Descendants().Attributes(), attribute => attribute.Value == "{Binding ShowDeactivateConfirmation}");
    }

    [Fact]
    public void NavigationEntryUsesExistingUnitsViewPermission()
    {
        var document = XDocument.Load(Path.Combine(SourceDirectory(), "src", "Decor.AvaloniaUI", "MainWindow.axaml"));
        var item = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == "{Binding ShowUnitsOfMeasureCommand}");
        Assert.Equal("Unidades de medida", (string?)item.Attribute("Header"));
        Assert.Equal("{Binding CanViewUnitsOfMeasure}", (string?)item.Attribute("IsVisible"));
    }

    [Fact]
    public void DeniedViewPermissionDoesNotOpenUnitsWorkspace()
    {
        var navigation = new Mock<INavigationService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(Decor.Core.Common.DecorPermissions.UnitsOfMeasureView)).Returns(false);
        var viewModel = new MainViewModel(navigation.Object, new Mock<IThemeService>().Object,
            authorization.Object, new Mock<IAuthenticatedUserContext>().Object);

        viewModel.ShowUnitsOfMeasureCommand.Execute(null);

        Assert.False(viewModel.CanViewUnitsOfMeasure);
        Assert.Empty(viewModel.OpenDocuments);
        Assert.Empty(navigation.Invocations);
    }

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Decor.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}