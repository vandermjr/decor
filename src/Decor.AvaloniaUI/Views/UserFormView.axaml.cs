using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class UserFormView : UserControl
{
    private readonly ScrollViewer _formScrollViewer;
    private readonly TextBox _usernameTextBox;
    private UserFormViewModel? _form;

    public UserFormView()
    {
        AvaloniaXamlLoader.Load(this);
        _formScrollViewer = this.FindControl<ScrollViewer>("FormScrollViewer")!;
        _usernameTextBox = this.FindControl<TextBox>("UsernameTextBox")!;
        DataContextChanged += (_, _) =>
        {
            if (_form is not null) _form.PropertyChanged -= FormPropertyChanged;
            if (_form is not null) _form.EmployeeLookupRequested -= FormEmployeeLookupRequested;
            _form = DataContext as UserFormViewModel;
            if (_form is not null)
            {
                _form.PropertyChanged += FormPropertyChanged;
                _form.EmployeeLookupRequested += FormEmployeeLookupRequested;
            }
            Dispatcher.UIThread.Post(() =>
            {
                _formScrollViewer.ScrollToHome();
                FocusUsername();
            });
        };
    }

    public event EventHandler? EmployeeLookupRequested;

    private void SearchEmployee_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
        => _form?.RequestEmployeeLookup();

    private void FormEmployeeLookupRequested(object? sender, EventArgs args)
        => EmployeeLookupRequested?.Invoke(sender, args);

    private void FormPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UserFormViewModel.IsBusy) && _form is { IsBusy: false })
            Dispatcher.UIThread.Post(FocusUsername);
    }

    private void FocusUsername()
    {
        if (_form is { CanEditFields: true }) _usernameTextBox.Focus();
    }
}