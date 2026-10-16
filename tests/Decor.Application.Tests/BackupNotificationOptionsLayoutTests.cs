using System.Xml.Linq;

namespace Decor.Application.Tests;

public sealed class BackupNotificationOptionsLayoutTests
{
    [Fact]
    public void BackupNotificationOption_IsNestedAndUsesClockPicker()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);

        var view = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "IconCatalogView.axaml"));
        var backupNode = Assert.Single(view.Descendants(), element => element.Name.LocalName == "TreeViewItem"
            && (string?)element.Attribute("Tag") == "BackupNotifications");
        Assert.Equal("Backup", (string?)backupNode.Attribute("Header"));
        Assert.Equal("Notificações", (string?)backupNode.Parent?.Attribute("Header"));

        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "TimePickerPresenter"
            && (string?)element.Attribute("Time") == "{Binding BackupNotificationLeadTime, Mode=TwoWay}");
        Assert.DoesNotContain(view.Descendants(), element => element.Name.LocalName == "NumericUpDown"
            && ((string?)element.Attribute("Value"))?.Contains("BackupNotificationLeadTime", StringComparison.Ordinal) == true);

        var layout = Assert.Single(view.Descendants(), element => element.Name.LocalName == "Grid"
            && (string?)element.Attribute("ColumnDefinitions") == "230,Auto,*");
        Assert.Contains(layout.Elements(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Grid.Column") == "1"
            && (string?)element.Attribute("Grid.RowSpan") == "2");
        Assert.DoesNotContain(view.Descendants(), element => element.Name.LocalName == "Button"
            && ((string?)element.Attribute("Command"))?.Contains("ApplyCommand", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(view.Descendants(), element => element.Name.LocalName == "Button"
            && ((string?)element.Attribute("Command"))?.Contains("Save", StringComparison.Ordinal) == true);
    }
}