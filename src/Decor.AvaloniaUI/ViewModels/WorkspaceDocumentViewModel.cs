using Avalonia.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class WorkspaceDocumentViewModel : INotifyPropertyChanged
{
    private readonly Action<WorkspaceDocumentViewModel> _close;
    private readonly Action<WorkspaceDocumentViewModel> _activate;
    private bool _isActive;
    private bool _isPointerOver;

    public WorkspaceDocumentViewModel(
        string key,
        string title,
        Control content,
        Action<WorkspaceDocumentViewModel> close,
        Action<WorkspaceDocumentViewModel> activate)
    {
        Key = key;
        Title = title;
        Content = content;
        _close = close;
        _activate = activate;
        CloseCommand = new RelayCommand(() => _close(this));
        ActivateCommand = new RelayCommand(() => _activate(this));
        if (DocumentState is not null)
            DocumentState.PropertyChanged += OnDocumentStatePropertyChanged;
    }

    public string Key { get; }
    public string Title { get; }
    public Control Content { get; }
    public ICommand CloseCommand { get; }
    public ICommand ActivateCommand { get; }
    public IStatusBarSource? StatusSource => Content.DataContext as IStatusBarSource;
    public IWorkspaceDocumentState? DocumentState => Content.DataContext as IWorkspaceDocumentState;
    public bool IsModified => DocumentState?.IsEditing ?? false;
    public bool IsHighlighted => IsActive || IsPointerOver;
    public bool ShowModificationIndicator => IsModified && !IsPointerOver;
    public bool ShowCloseButton => !IsModified || IsPointerOver;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHighlighted));
        }
    }

    public bool IsPointerOver
    {
        get => _isPointerOver;
        set
        {
            if (_isPointerOver == value) return;
            _isPointerOver = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHighlighted));
            OnPropertyChanged(nameof(ShowModificationIndicator));
            OnPropertyChanged(nameof(ShowCloseButton));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnDocumentStatePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(IWorkspaceDocumentState.IsEditing))
        {
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(ShowModificationIndicator));
            OnPropertyChanged(nameof(ShowCloseButton));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
