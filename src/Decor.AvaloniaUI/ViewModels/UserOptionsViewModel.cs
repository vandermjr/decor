using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class UserOptionsViewModel : INotifyPropertyChanged
{
    private readonly IThemeService _themeService;
    private string _selectedThemeOption = string.Empty;

    public ObservableCollection<string> ThemeOptions { get; } = ["Claro", "Escuro"];

    public string CurrentThemeName => _themeService.CurrentTheme == DecorThemeStyle.Light ? "Claro" : "Escuro";

    public string SelectedThemeOption
    {
        get => _selectedThemeOption;
        set => SetField(ref _selectedThemeOption, value);
    }

    public ICommand ApplyThemeCommand { get; }

    public UserOptionsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        SelectedThemeOption = CurrentThemeName;
        ApplyThemeCommand = new RelayCommand(async () => await ApplyThemeAsync());
    }

    private async Task ApplyThemeAsync()
    {
        var desiredTheme = SelectedThemeOption == "Claro" ? DecorThemeStyle.Light : DecorThemeStyle.Dark;
        if (_themeService.CurrentTheme == desiredTheme)
            return;

        await _themeService.SetThemeAsync(desiredTheme);
        if (global::Avalonia.Application.Current is not null)
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = desiredTheme == DecorThemeStyle.Dark
                ? global::Avalonia.Styling.ThemeVariant.Dark
                : global::Avalonia.Styling.ThemeVariant.Light;
        }

        OnPropertyChanged(nameof(CurrentThemeName));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
