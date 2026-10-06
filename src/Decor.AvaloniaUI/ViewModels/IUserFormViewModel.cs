using System.ComponentModel;
using System.Windows.Input;

namespace Decor.AvaloniaUI.ViewModels;

public interface IUserFormViewModel : INotifyPropertyChanged
{
    string FormTitle { get; }
    string Username { get; set; }
    bool CanEditFields { get; }
    bool IsBusy { get; }
    bool IsCompleted { get; }
    bool HasError { get; }
    string? ErrorMessage { get; }
    string DismissButtonText { get; }
    ICommand SaveCommand { get; }
    ICommand DismissCommand { get; }
}