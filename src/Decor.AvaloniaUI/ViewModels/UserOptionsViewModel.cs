using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Styling;
using Decor.Core.Common;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class UserOptionsViewModel : INotifyPropertyChanged
{
    private readonly IUserSettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IIconAppearanceService _iconAppearanceService;
    private DecorThemeStyle _selectedTheme = DecorDefaults.Theme;
    private int _iconWeight = IconAppearance.Default.MaterialSymbolWeight;
    private double _strokeThickness = IconAppearance.Default.StrokeThickness;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isLoaded;
    private string? _errorMessage;

    public UserOptionsViewModel(IUserSettingsService settingsService, IThemeService themeService, IIconAppearanceService iconAppearanceService)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _iconAppearanceService = iconAppearanceService;
        UseLightThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Light));
        UseDarkThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Dark));
        SaveIconAppearanceCommand = new RelayCommand(async () => await SaveIconAppearanceAsync());
        RetryLoadCommand = new RelayCommand(async () => await LoadAsync());
    }

    public bool IsLightTheme => _selectedTheme == DecorThemeStyle.Light;
    public bool IsDarkTheme => _selectedTheme == DecorThemeStyle.Dark;
    public int IconWeight
    {
        get => _iconWeight;
        set => SetField(ref _iconWeight, value);
    }

    public double StrokeThickness
    {
        get => _strokeThickness;
        set => SetField(ref _strokeThickness, Math.Round(value, 1, MidpointRounding.AwayFromZero));
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetField(ref _isLoading, value)) OnPropertyChanged(nameof(CanEdit)); }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set { if (SetField(ref _isSaving, value)) OnPropertyChanged(nameof(CanEdit)); }
    }

    public bool CanEdit => _isLoaded && !IsLoading && !IsSaving;
    public bool CanRetry => !_isLoaded && !IsLoading;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public ICommand UseLightThemeCommand { get; }
    public ICommand UseDarkThemeCommand { get; }
    public ICommand SaveIconAppearanceCommand { get; }
    public ICommand RetryLoadCommand { get; }

    public async Task LoadAsync()
    {
        if (_isLoaded) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var settings = await _settingsService.GetAsync();
            _selectedTheme = settings.Theme;
            IconWeight = settings.IconAppearance.MaterialSymbolWeight;
            StrokeThickness = settings.IconAppearance.StrokeThickness;
            _isLoaded = true;
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível carregar as opções do usuário.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanRetry));
        }
    }

    public async Task SetThemeAsync(DecorThemeStyle theme)
    {
        if (!CanEdit || _selectedTheme == theme) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await _themeService.SetThemeAsync(theme);
            _selectedTheme = theme;
            if (global::Avalonia.Application.Current is not null)
                global::Avalonia.Application.Current.RequestedThemeVariant = theme == DecorThemeStyle.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível salvar o tema.";
        }
        finally
        {
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
            IsSaving = false;
        }
    }

    public async Task SaveIconAppearanceAsync()
    {
        if (!CanEdit) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await _iconAppearanceService.SetAppearanceAsync(IconWeight, StrokeThickness);
            IconWeight = _iconAppearanceService.CurrentAppearance.MaterialSymbolWeight;
            StrokeThickness = _iconAppearanceService.CurrentAppearance.StrokeThickness;
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível salvar a aparência dos ícones. Confira os valores antes de tentar novamente.";
            try
            {
                await _iconAppearanceService.InitializeAsync();
                IconWeight = _iconAppearanceService.CurrentAppearance.MaterialSymbolWeight;
                StrokeThickness = _iconAppearanceService.CurrentAppearance.StrokeThickness;
            }
            catch (Exception)
            {
                ErrorMessage = "Não foi possível confirmar a aparência salva dos ícones. Tente carregar as opções novamente.";
                _isLoaded = false;
                OnPropertyChanged(nameof(CanRetry));
            }
        }
        finally
        {
            IsSaving = false;
            OnPropertyChanged(nameof(CanEdit));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}