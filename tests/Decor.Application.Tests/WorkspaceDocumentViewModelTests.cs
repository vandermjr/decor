using Avalonia.Controls;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.ViewModels;
using System.ComponentModel;

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
    [InlineData("permissions", "Forms.Permissions")]
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

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public void ModificationIndicator_DistinguishesAddFromEdit(bool isAdding, bool showAddition, bool showEdit)
    {
        var state = new TestDocumentState();
        var document = new WorkspaceDocumentViewModel(
            "test", "Documento", new ContentControl { DataContext = state }, _ => { }, _ => { });

        state.SetMode(isAdding);
        Assert.Equal(showAddition, document.ShowAdditionIndicator);
        Assert.Equal(showEdit, document.ShowModificationIndicator);
        Assert.False(document.ShowCloseButton);
    }

    private sealed class TestDocumentState : IWorkspaceDocumentState
    {
        public bool IsEditing => true;
        public bool IsAdding { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetMode(bool isAdding)
        {
            IsAdding = isAdding;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAdding)));
        }
    }
}