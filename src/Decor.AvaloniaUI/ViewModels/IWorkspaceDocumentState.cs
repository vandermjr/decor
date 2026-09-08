using System.ComponentModel;

namespace Decor.AvaloniaUI.ViewModels;

public interface IWorkspaceDocumentState : INotifyPropertyChanged
{
    bool IsEditing { get; }
}
