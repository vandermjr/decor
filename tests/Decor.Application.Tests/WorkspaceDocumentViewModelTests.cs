using Avalonia.Controls;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.Application.Tests;

public sealed class WorkspaceDocumentViewModelTests
{
    [Theory]
    [InlineData("products", "Forms.Products")]
    [InlineData("brands", "Forms.Brands")]
    [InlineData("employees", "User.Profile")]
    [InlineData("term-delivery", "Forms.TermDelivery")]
    [InlineData("classifications", "Forms.Classifications")]
    [InlineData("users", "Forms.Users")]
    [InlineData("roles", "Forms.PermissionGroups")]
    [InlineData("database-maintenance", "Forms.DatabaseMaintenance")]
    [InlineData("system-icons", "Application.Settings")]
    [InlineData("user-options", "User.Preferences")]
    [InlineData("unknown", "Common.Folder")]
    public void IconId_UsesRegisteredIconForDocument(string key, string expectedIcon)
    {
        var document = new WorkspaceDocumentViewModel(key, "Documento", new Control(), _ => { }, _ => { });

        Assert.Equal(new DecorIconId(expectedIcon), document.IconId);
        Assert.Contains(document.IconId, DecorIconCatalog.Ids);
    }
}