using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.Controls.GridStyling;
using Decor.Core.Common;
using Decor.Core.Interfaces.Services;
using System.Windows.Input;
using Decor.Core.Configuration;
using Avalonia.Styling;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class IconCatalogViewModel : INotifyPropertyChanged
{
    private const int MaximumTimePickerDurationMinutes = 23 * 60 + 59;
    private static readonly string[] HorizontalLogoPositions = ["Esquerda", "Centro", "Direita"];
    private static readonly string[] VerticalLogoPositions = ["Topo", "Centro", "Base"];
    private readonly IIconAppearanceService _iconAppearanceService;
    private readonly IThemeService _themeService;
    private readonly IStockSettingsService? _stockSettingsService;
    private readonly IBackupNotificationSettingsStore? _backupNotificationSettingsStore;
    private readonly IDatabaseBackupScheduleStore? _backupScheduleStore;
    private readonly ILoginBrandingSettingsStore? _loginBrandingSettingsStore;
        private readonly GridAppearanceEditorViewModel _gridAppearanceEditor;
    private int _iconWeight;
    private bool _allowNegativeStock;
    private string _selectedSettingsSection = "Theme";
    private string _selectedThemeOption = string.Empty;
    private string _stockSettingsStatus = string.Empty;
    private string _themeSettingsStatus = string.Empty;
    private string _iconAppearanceStatus = string.Empty;
    private string _iconColorStatus = string.Empty;
    private bool _notifyBeforeScheduledBackup = true;
    private int _backupNotificationLeadTimeMinutes = 15;
    private string _backupNotificationSettingsStatus = string.Empty;
    private string _loginBrandingStatus = string.Empty;
    private string _loginLogoFileName = "Logo padrão do Decor";
    private Bitmap? _loginLogoPreview;
    private double _loginLogoSizePercent = 90;
    private string _loginLogoHorizontalPosition = "Centro";
    private string _loginLogoVerticalPosition = "Centro";
    private bool _invertLoginLogoInLightTheme;

    public ObservableCollection<IconCatalogFamilyViewModel> FamilyGroups { get; } = [];
    public ObservableCollection<string> ThemeOptions { get; } = ["Claro", "Escuro"];
    public GridAppearanceEditorViewModel GridAppearance => _gridAppearanceEditor;

    public int IconWeight
    {
        get => _iconWeight;
        set
        {
            if (!DecorIconCatalog.SupportedWeights.Contains(value) || !SetField(ref _iconWeight, value))
                return;

            foreach (var item in FamilyGroups.SelectMany(group => group.Items))
                item.UpdateGeometry(value);
            _ = SaveIconAppearanceAsync(value);
        }
    }

    public string IconWeightDisplay => IconWeight.ToString(CultureInfo.InvariantCulture);
    public ICommand ResetIconColorsCommand { get; }
    public ICommand RemoveLoginLogoCommand { get; }
    public ICommand MoveLoginLogoUpCommand { get; }
    public ICommand MoveLoginLogoDownCommand { get; }
    public ICommand MoveLoginLogoLeftCommand { get; }
    public ICommand MoveLoginLogoRightCommand { get; }
    public ICommand ResetSelectedSettingsCommand { get; }
    public ICommand UseLightThemeCommand { get; }
    public ICommand UseDarkThemeCommand { get; }
    public bool IsThemeSettingsSelected => _selectedSettingsSection == "Theme";
    public bool IsIconSettingsSelected => _selectedSettingsSection == "Icons";
    public bool IsStockMovementsSelected => _selectedSettingsSection == "StockMovements";
    public bool IsBackupNotificationsSelected => _selectedSettingsSection == "BackupNotifications";
    public bool IsLoginBrandingSelected => _selectedSettingsSection == "LoginBranding";
    public bool IsGridAppearanceSelected => _selectedSettingsSection == "GridAppearance";
    public string ResetSelectedSettingsText => IsLoginBrandingSelected || IsGridAppearanceSelected ? "Restaurar padrão" : "Restaurar cores";
    public bool CanMoveLoginLogoUp => _loginLogoVerticalPosition != "Topo";
    public bool CanMoveLoginLogoDown => _loginLogoVerticalPosition != "Base";
    public bool CanMoveLoginLogoLeft => _loginLogoHorizontalPosition != "Esquerda";
    public bool CanMoveLoginLogoRight => _loginLogoHorizontalPosition != "Direita";
    public string CurrentThemeName => _themeService.CurrentTheme == DecorThemeStyle.Light ? "Claro" : "Escuro";
    public string SelectedThemeOption
    {
        get => _selectedThemeOption;
        set
        {
            if (SetField(ref _selectedThemeOption, value))
                _ = ApplyThemeSelectionAsync();
        }
    }
    public string ThemeSettingsStatus
    {
        get => _themeSettingsStatus;
        private set => SetField(ref _themeSettingsStatus, value);
    }
    public string IconAppearanceStatus
    {
        get => _iconAppearanceStatus;
        private set => SetField(ref _iconAppearanceStatus, value);
    }
    public string StockSettingsStatus
    {
        get => _stockSettingsStatus;
        private set => SetField(ref _stockSettingsStatus, value);
    }
    public string IconColorStatus
    {
        get => _iconColorStatus;
        private set => SetField(ref _iconColorStatus, value);
    }

    public void ReportIconColorSaveFailure() => IconColorStatus = "Não foi possível salvar a cor do ícone.";

    public void ClearIconColorStatus() => IconColorStatus = string.Empty;

    public string SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        set
        {
            if (!SetField(ref _selectedSettingsSection, value)) return;
            OnPropertyChanged(nameof(IsThemeSettingsSelected));
            OnPropertyChanged(nameof(IsIconSettingsSelected));
            OnPropertyChanged(nameof(IsStockMovementsSelected));
            OnPropertyChanged(nameof(IsBackupNotificationsSelected));
            OnPropertyChanged(nameof(IsLoginBrandingSelected));
            OnPropertyChanged(nameof(IsGridAppearanceSelected));
            OnPropertyChanged(nameof(ResetSelectedSettingsText));
        }
    }

    public bool AllowNegativeStock
    {
        get => _allowNegativeStock;
        set
        {
            if (SetField(ref _allowNegativeStock, value))
                _ = SaveStockSettingsAsync(value);
        }
    }

    public bool NotifyBeforeScheduledBackup
    {
        get => _notifyBeforeScheduledBackup;
        set
        {
            if (!SetField(ref _notifyBeforeScheduledBackup, value))
                return;
            SaveBackupNotificationSettings();
            OnPropertyChanged(nameof(NextBackupNotificationPreview));
        }
    }

    public int BackupNotificationLeadTimeMinutes
    {
        get => _backupNotificationLeadTimeMinutes;
        set
        {
            var minutes = Math.Clamp(value, 1, Math.Min(MaximumBackupNotificationLeadTimeMinutes, MaximumTimePickerDurationMinutes));
            if (!SetField(ref _backupNotificationLeadTimeMinutes, minutes))
                return;
            OnPropertyChanged(nameof(BackupNotificationLeadTime));
            OnPropertyChanged(nameof(BackupNotificationLeadTimeDisplay));
            OnPropertyChanged(nameof(NextBackupNotificationPreview));
        }
    }

    public TimeSpan? BackupNotificationLeadTime
    {
        get => TimeSpan.FromMinutes(_backupNotificationLeadTimeMinutes);
        set
        {
            if (value is null)
                return;
            BackupNotificationLeadTimeMinutes = (int)Math.Round(value.Value.TotalMinutes, MidpointRounding.AwayFromZero);
        }
    }

    public string BackupNotificationLeadTimeDisplay => TimeSpan.FromMinutes(_backupNotificationLeadTimeMinutes).ToString(@"hh\:mm");
    public int MaximumBackupNotificationLeadTimeMinutes => GetMaximumBackupNotificationLeadTimeMinutes();
    public string BackupNotificationLeadTimeLabel => $"Avisar antes do backup (máximo atual: {MaximumBackupNotificationLeadTimeMinutes} min)";
    public string NextBackupNotificationPreview => CreateNextBackupNotificationPreview();

    public string BackupNotificationSettingsStatus
    {
        get => _backupNotificationSettingsStatus;
        private set => SetField(ref _backupNotificationSettingsStatus, value);
    }

    public string LoginBrandingStatus
    {
        get => _loginBrandingStatus;
        private set => SetField(ref _loginBrandingStatus, value);
    }

    public string LoginLogoFileName
    {
        get => _loginLogoFileName;
        private set => SetField(ref _loginLogoFileName, value);
    }

    public Bitmap? LoginLogoPreview
    {
        get => _loginLogoPreview;
        private set
        {
            if (!SetField(ref _loginLogoPreview, value))
                return;
            OnPropertyChanged(nameof(HasLoginLogoPreview));
        }
    }

    public bool HasLoginLogoPreview => LoginLogoPreview is not null;

    public bool InvertLoginLogoInLightTheme
    {
        get => _invertLoginLogoInLightTheme;
        set
        {
            if (!SetField(ref _invertLoginLogoInLightTheme, value))
                return;
            try
            {
                _loginBrandingSettingsStore?.SaveInvertLogoInLightTheme(value);
                LoadLoginLogoPreview();
                LoginBrandingStatus = "Inversão de cores salva.";
            }
            catch (Exception)
            {
                LoginBrandingStatus = "Não foi possível salvar a inversão de cores.";
            }
        }
    }

    public double LoginLogoSizePercent
    {
        get => _loginLogoSizePercent;
        set
        {
            var size = Math.Clamp(Math.Round(value / 5) * 5, 25, 100);
            if (!SetField(ref _loginLogoSizePercent, size))
                return;
            OnPropertyChanged(nameof(LoginLogoSizeDisplay));
            OnPropertyChanged(nameof(LoginLogoWidth));
            OnPropertyChanged(nameof(LoginLogoPreviewWidth));
            SaveLoginLogoLayout();
        }
    }

    public string LoginLogoSizeDisplay => $"{LoginLogoSizePercent:0}%";
    public double LoginLogoWidth => LoginLogoSizePercent * 5.1;
    public double LoginLogoPreviewWidth => LoginLogoSizePercent * 5.1 * 320 / 930;

    public string LoginLogoHorizontalPosition
    {
        get => _loginLogoHorizontalPosition;
        set
        {
            if (!HorizontalLogoPositions.Contains(value) || !SetField(ref _loginLogoHorizontalPosition, value))
                return;
            OnPropertyChanged(nameof(LoginLogoHorizontalAlignment));
            OnPropertyChanged(nameof(CanMoveLoginLogoLeft));
            OnPropertyChanged(nameof(CanMoveLoginLogoRight));
            OnPropertyChanged(nameof(LoginLogoPositionDisplay));
            RaiseLogoPositionCommands();
            SaveLoginLogoLayout();
        }
    }

    public string LoginLogoVerticalPosition
    {
        get => _loginLogoVerticalPosition;
        set
        {
            if (!VerticalLogoPositions.Contains(value) || !SetField(ref _loginLogoVerticalPosition, value))
                return;
            OnPropertyChanged(nameof(LoginLogoVerticalAlignment));
            OnPropertyChanged(nameof(CanMoveLoginLogoUp));
            OnPropertyChanged(nameof(CanMoveLoginLogoDown));
            OnPropertyChanged(nameof(LoginLogoPositionDisplay));
            RaiseLogoPositionCommands();
            SaveLoginLogoLayout();
        }
    }

    public HorizontalAlignment LoginLogoHorizontalAlignment => LoginLogoHorizontalPosition switch
    {
        "Esquerda" => HorizontalAlignment.Left,
        "Direita" => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Center
    };

    public VerticalAlignment LoginLogoVerticalAlignment => LoginLogoVerticalPosition switch
    {
        "Topo" => VerticalAlignment.Top,
        "Base" => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Center
    };

    public string LoginLogoPositionDisplay => $"{LoginLogoVerticalPosition} · {LoginLogoHorizontalPosition}";

    public IconCatalogViewModel(IIconAppearanceService iconAppearanceService, IThemeService themeService,
        IStockSettingsService? stockSettingsService = null,
        IBackupNotificationSettingsStore? backupNotificationSettingsStore = null,
        IDatabaseBackupScheduleStore? backupScheduleStore = null,
        ILoginBrandingSettingsStore? loginBrandingSettingsStore = null,
        GridAppearanceSettingsService? gridAppearanceSettingsService = null)
    {
        _iconAppearanceService = iconAppearanceService;
        _themeService = themeService;
        _stockSettingsService = stockSettingsService;
        _backupNotificationSettingsStore = backupNotificationSettingsStore;
        _backupScheduleStore = backupScheduleStore;
        _loginBrandingSettingsStore = loginBrandingSettingsStore;
        _gridAppearanceEditor = new GridAppearanceEditorViewModel(gridAppearanceSettingsService
            ?? new GridAppearanceSettingsService(Path.Combine(Path.GetTempPath(), $"decor-grid-options-{Guid.NewGuid():N}.json")), _themeService);
        LoadLoginBrandingSettings();
        if (_backupScheduleStore is not null)
            _backupScheduleStore.SchedulesChanged += OnBackupSchedulesChanged;
        _iconWeight = iconAppearanceService.CurrentAppearance.MaterialSymbolWeight;
        _themeService.ThemeChanged += OnThemeChanged;
        ResetIconColorsCommand = new RelayCommand(async () => await ResetIconColorsAsync());
        RemoveLoginLogoCommand = new RelayCommand(RemoveLoginLogo);
        MoveLoginLogoUpCommand = new RelayCommand(() => StepLoginLogoVertical(-1), () => CanMoveLoginLogoUp);
        MoveLoginLogoDownCommand = new RelayCommand(() => StepLoginLogoVertical(1), () => CanMoveLoginLogoDown);
        MoveLoginLogoLeftCommand = new RelayCommand(() => StepLoginLogoHorizontal(-1), () => CanMoveLoginLogoLeft);
        MoveLoginLogoRightCommand = new RelayCommand(() => StepLoginLogoHorizontal(1), () => CanMoveLoginLogoRight);
        ResetSelectedSettingsCommand = new RelayCommand(async () =>
        {
            if (IsLoginBrandingSelected)
                ResetLoginLogo();
            else if (IsGridAppearanceSelected)
                GridAppearance.ResetToDefaults();
            else
                await ResetIconColorsAsync();
        });
        UseLightThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Light));
        UseDarkThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Dark));
        _selectedThemeOption = CurrentThemeName;
        foreach (var family in DecorIconCatalog.GetFamilyGroups())
        {
            var group = new IconCatalogFamilyViewModel(family.Name);
            foreach (var iconId in family.Ids)
                group.Items.Add(new IconCatalogItemViewModel(iconId, IconWeight, _iconAppearanceService, ThemeVariantFor(_themeService.CurrentTheme)));

            FamilyGroups.Add(group);
        }
    }

    public async Task InitializeAsync()
    {
        if (_stockSettingsService is not null)
        {
            _allowNegativeStock = await _stockSettingsService.GetAllowNegativeStockAsync();
            OnPropertyChanged(nameof(AllowNegativeStock));
        }
        if (_backupNotificationSettingsStore is not null)
        {
            var settings = _backupNotificationSettingsStore.Load();
            _notifyBeforeScheduledBackup = settings.IsEnabled;
            OnPropertyChanged(nameof(NotifyBeforeScheduledBackup));
            _backupNotificationLeadTimeMinutes = Math.Clamp(settings.LeadTimeMinutes, 1, 1440);
            OnPropertyChanged(nameof(BackupNotificationLeadTimeMinutes));
            OnPropertyChanged(nameof(BackupNotificationLeadTime));
            OnPropertyChanged(nameof(BackupNotificationLeadTimeDisplay));
            RefreshBackupNotificationPreview();
        }
        if (_loginBrandingSettingsStore is not null)
            LoadLoginBrandingSettings();
    }

    public void SaveLoginLogo(string sourcePath)
    {
        if (_loginBrandingSettingsStore is null)
        {
            LoginBrandingStatus = "As configurações do logo não estão disponíveis.";
            return;
        }

        try
        {
            _loginBrandingSettingsStore.SaveLogo(sourcePath);
            LoginLogoFileName = _loginBrandingSettingsStore.LogoFileName;
            LoadLoginLogoPreview();
            LoginBrandingStatus = "Logo atualizado e prévia sincronizada.";
        }
        catch (Exception)
        {
            LoginBrandingStatus = "Não foi possível salvar essa imagem. Use um PNG ou JPEG de até 10 MB.";
        }
    }

    private void ResetLoginLogo()
    {
        if (_loginBrandingSettingsStore is null)
        {
            LoginBrandingStatus = "As configurações do logo não estão disponíveis.";
            return;
        }

        try
        {
            _loginBrandingSettingsStore.ResetLogo();
            LoadLoginBrandingSettings();
            LoginBrandingStatus = "Logo e apresentação padrão restaurados.";
        }
        catch (Exception)
        {
            LoginBrandingStatus = "Não foi possível restaurar o logo padrão.";
        }
    }

    private void RemoveLoginLogo()
    {
        if (_loginBrandingSettingsStore is null)
        {
            LoginBrandingStatus = "As configurações da logo não estão disponíveis.";
            return;
        }

        try
        {
            _loginBrandingSettingsStore.RemoveLogo();
            LoginLogoFileName = _loginBrandingSettingsStore.LogoFileName;
            LoadLoginLogoPreview();
            LoginBrandingStatus = "Imagem removida; logo padrão do Decor aplicada.";
        }
        catch (Exception)
        {
            LoginBrandingStatus = "Não foi possível remover essa imagem.";
        }
    }

    private void StepLoginLogoHorizontal(int step)
    {
        LoginLogoHorizontalPosition = LoginLogoHorizontalPosition switch
        {
            "Esquerda" when step > 0 => "Centro",
            "Centro" when step < 0 => "Esquerda",
            "Centro" when step > 0 => "Direita",
            "Direita" when step < 0 => "Centro",
            _ => LoginLogoHorizontalPosition
        };
    }

    private void StepLoginLogoVertical(int step)
    {
        LoginLogoVerticalPosition = LoginLogoVerticalPosition switch
        {
            "Topo" when step > 0 => "Centro",
            "Centro" when step < 0 => "Topo",
            "Centro" when step > 0 => "Base",
            "Base" when step < 0 => "Centro",
            _ => LoginLogoVerticalPosition
        };
    }

    private void RaiseLogoPositionCommands()
    {
        ((RelayCommand)MoveLoginLogoUpCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MoveLoginLogoDownCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MoveLoginLogoLeftCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MoveLoginLogoRightCommand).RaiseCanExecuteChanged();
    }

    private void LoadLoginBrandingSettings()
    {
        if (_loginBrandingSettingsStore is null)
            return;

        _loginLogoSizePercent = _loginBrandingSettingsStore.LogoSizePercent;
        _loginLogoHorizontalPosition = _loginBrandingSettingsStore.LogoHorizontalPosition;
        _loginLogoVerticalPosition = _loginBrandingSettingsStore.LogoVerticalPosition;
        _invertLoginLogoInLightTheme = _loginBrandingSettingsStore.InvertLogoInLightTheme;
        OnPropertyChanged(nameof(LoginLogoSizePercent));
        OnPropertyChanged(nameof(LoginLogoSizeDisplay));
        OnPropertyChanged(nameof(LoginLogoWidth));
        OnPropertyChanged(nameof(LoginLogoPreviewWidth));
        OnPropertyChanged(nameof(LoginLogoHorizontalPosition));
        OnPropertyChanged(nameof(LoginLogoHorizontalAlignment));
        OnPropertyChanged(nameof(CanMoveLoginLogoLeft));
        OnPropertyChanged(nameof(CanMoveLoginLogoRight));
        OnPropertyChanged(nameof(LoginLogoVerticalPosition));
        OnPropertyChanged(nameof(InvertLoginLogoInLightTheme));
        OnPropertyChanged(nameof(LoginLogoVerticalAlignment));
        OnPropertyChanged(nameof(CanMoveLoginLogoUp));
        OnPropertyChanged(nameof(CanMoveLoginLogoDown));
        OnPropertyChanged(nameof(LoginLogoPositionDisplay));
        if (MoveLoginLogoUpCommand is RelayCommand moveUp)
            moveUp.RaiseCanExecuteChanged();
        if (MoveLoginLogoDownCommand is RelayCommand moveDown)
            moveDown.RaiseCanExecuteChanged();
        if (MoveLoginLogoLeftCommand is RelayCommand moveLeft)
            moveLeft.RaiseCanExecuteChanged();
        if (MoveLoginLogoRightCommand is RelayCommand moveRight)
            moveRight.RaiseCanExecuteChanged();
        LoginLogoFileName = _loginBrandingSettingsStore.LogoFileName;
        LoadLoginLogoPreview();
    }

    private void LoadLoginLogoPreview()
    {
        var path = _loginBrandingSettingsStore?.LogoPath;
        var bitmap = path is null
            ? null
            : LoginLogoImageLoader.Load(path,
                InvertLoginLogoInLightTheme && _themeService.CurrentTheme == DecorThemeStyle.Light);

        var previousBitmap = _loginLogoPreview;
        LoginLogoPreview = bitmap;
        previousBitmap?.Dispose();
    }

    private void SaveLoginLogoLayout()
    {
        if (_loginBrandingSettingsStore is null)
            return;

        try
        {
            _loginBrandingSettingsStore.SaveLayout(
                (int)LoginLogoSizePercent,
                LoginLogoHorizontalPosition,
                LoginLogoVerticalPosition);
            LoginBrandingStatus = "Prévia atualizada; ajustes salvos.";
        }
        catch (Exception)
        {
            LoginBrandingStatus = "Não foi possível salvar os ajustes do logo.";
        }
    }

    private async Task SaveIconAppearanceAsync(int weight)
    {
        try
        {
            await _iconAppearanceService.SetAppearanceAsync(weight, _iconAppearanceService.CurrentAppearance.StrokeThickness);
            IconAppearanceStatus = "Peso dos ícones salvo.";
        }
        catch (Exception)
        {
            IconAppearanceStatus = "Não foi possível salvar o peso dos ícones.";
        }
    }

    private async Task ResetIconColorsAsync()
    {
        await _iconAppearanceService.ResetIconColorsAsync();
        foreach (var item in FamilyGroups.SelectMany(group => group.Items))
            item.RefreshColor();
    }

    private void OnThemeChanged(DecorThemeStyle theme)
    {
        var themeVariant = ThemeVariantFor(theme);
        foreach (var item in FamilyGroups.SelectMany(group => group.Items))
            item.RefreshColor(themeVariant);
        LoadLoginLogoPreview();
    }

    private static ThemeVariant ThemeVariantFor(DecorThemeStyle theme) =>
        theme == DecorThemeStyle.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

    private async Task SaveStockSettingsAsync(bool allowNegativeStock)
    {
        if (_stockSettingsService is null)
        {
            StockSettingsStatus = "As configurações de estoque não estão disponíveis.";
            return;
        }

        try
        {
            await _stockSettingsService.SetAllowNegativeStockAsync(allowNegativeStock);
            StockSettingsStatus = "Configuração de estoque salva.";
        }
        catch
        {
            StockSettingsStatus = "Não foi possível salvar a configuração de estoque.";
        }
    }

    public void SaveBackupNotificationSettings()
    {
        if (_backupNotificationSettingsStore is null)
        {
            BackupNotificationSettingsStatus = "As opções de notificação de backup não estão disponíveis.";
            return;
        }

        try
        {
            _backupNotificationSettingsStore.Save(new BackupNotificationSettings(
                NotifyBeforeScheduledBackup, BackupNotificationLeadTimeMinutes));
            BackupNotificationSettingsStatus = "Opções de notificação salvas.";
        }
        catch (Exception)
        {
            BackupNotificationSettingsStatus = "Não foi possível salvar as opções de notificação.";
        }
    }

    private int GetMaximumBackupNotificationLeadTimeMinutes()
    {
        var nextSchedule = GetNextPendingBackupSchedule();
        if (nextSchedule is null)
            return MaximumTimePickerDurationMinutes;

        var minutesAvailable = (int)Math.Floor((nextSchedule.NextExecutionAt - DateTime.Now).TotalMinutes);
        return Math.Clamp(minutesAvailable, 1, MaximumTimePickerDurationMinutes);
    }

    private string CreateNextBackupNotificationPreview()
    {
        if (!NotifyBeforeScheduledBackup)
            return "O aviso de antecedência está desativado.";

        var nextSchedule = GetNextPendingBackupSchedule();
        if (nextSchedule is null)
        {
            var alreadyNotified = _backupScheduleStore?.Load()
                .Where(schedule => schedule.IsEnabled && schedule.NotifiedForExecutionAt == schedule.NextExecutionAt)
                .OrderBy(schedule => schedule.NextExecutionAt)
                .FirstOrDefault();
            if (alreadyNotified is not null)
                return $"O aviso já foi enviado. Próximo backup: {alreadyNotified.NextExecutionAt:dd/MM/yyyy HH:mm}.";
            return "Sem backups ativos. Esta antecedência será aplicada aos próximos agendamentos.";
        }

        var notificationAt = nextSchedule.NextExecutionAt.AddMinutes(-BackupNotificationLeadTimeMinutes);
        return notificationAt <= DateTime.Now
            ? $"Próximo backup: {nextSchedule.NextExecutionAt:dd/MM/yyyy HH:mm}. O horário do aviso já chegou; ele será exibido imediatamente."
            : $"Próximo backup: {nextSchedule.NextExecutionAt:dd/MM/yyyy HH:mm}. Aviso previsto para {notificationAt:dd/MM/yyyy HH:mm}.";
    }

    private DatabaseBackupSchedule? GetNextPendingBackupSchedule() => _backupScheduleStore?.Load()
        .Where(schedule => schedule.IsEnabled && schedule.NotifiedForExecutionAt != schedule.NextExecutionAt)
        .OrderBy(schedule => schedule.NextExecutionAt)
        .FirstOrDefault();

    private void OnBackupSchedulesChanged(object? sender, EventArgs eventArgs) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(RefreshBackupNotificationPreview);

    private void RefreshBackupNotificationPreview()
    {
        var maximum = MaximumBackupNotificationLeadTimeMinutes;
        if (_backupNotificationLeadTimeMinutes > maximum)
        {
            BackupNotificationLeadTimeMinutes = maximum;
            if (_backupNotificationSettingsStore is not null)
            {
                try
                {
                    _backupNotificationSettingsStore.Save(new BackupNotificationSettings(
                        NotifyBeforeScheduledBackup, maximum));
                    BackupNotificationSettingsStatus = $"Antecedência ajustada para {maximum} min por causa do próximo backup.";
                }
                catch (Exception)
                {
                    BackupNotificationSettingsStatus = "Não foi possível ajustar a antecedência ao próximo backup.";
                }
            }
        }
        OnPropertyChanged(nameof(MaximumBackupNotificationLeadTimeMinutes));
        OnPropertyChanged(nameof(BackupNotificationLeadTimeLabel));
        OnPropertyChanged(nameof(NextBackupNotificationPreview));
    }

    private async Task SetThemeAsync(DecorThemeStyle theme)
    {
        if (_themeService.CurrentTheme == theme)
            return;

        try
        {
            await _themeService.SetThemeAsync(theme);
            _selectedThemeOption = theme == DecorThemeStyle.Light ? "Claro" : "Escuro";
            if (global::Avalonia.Application.Current is not null)
            {
                global::Avalonia.Application.Current.RequestedThemeVariant = theme == DecorThemeStyle.Dark
                    ? global::Avalonia.Styling.ThemeVariant.Dark
                    : global::Avalonia.Styling.ThemeVariant.Light;
            }
            OnPropertyChanged(nameof(CurrentThemeName));
            OnPropertyChanged(nameof(SelectedThemeOption));
            ThemeSettingsStatus = "Tema atualizado e salvo.";
        }
        catch (Exception)
        {
            _selectedThemeOption = CurrentThemeName;
            OnPropertyChanged(nameof(SelectedThemeOption));
            ThemeSettingsStatus = "Não foi possível salvar o tema.";
        }
    }

    private async Task ApplyThemeSelectionAsync()
    {
        var desiredTheme = SelectedThemeOption == "Claro" ? DecorThemeStyle.Light : DecorThemeStyle.Dark;
        await SetThemeAsync(desiredTheme);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName == nameof(IconWeight)) OnPropertyChanged(nameof(IconWeightDisplay));
        return true;
    }

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class IconCatalogFamilyViewModel(string name)
{
    public string Name { get; } = name switch
    {
        "Application" => "Aplicativo",
        "Modules" => "Módulos",
        "Forms" => "Formulários",
        "Actions" => "Ações",
        "User" => "Usuário",
        "Common" => "Geral",
        "Navigation" => "Navegação",
        _ => name
    };
    public ObservableCollection<IconCatalogItemViewModel> Items { get; } = [];
}

public sealed class IconCatalogItemViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        ["Application.Home"] = "Início",
        ["Application.Settings"] = "Configurações",
        ["Application.Help"] = "Ajuda",
        ["Application.Info"] = "Informações",
        ["Application.ThemeToggle"] = "Alternar tema",
        ["Modules.Cadastros"] = "Cadastros",
        ["Modules.Compras"] = "Compras",
        ["Modules.Estoque"] = "Estoque",
        ["Modules.Comercial"] = "Comercial",
        ["Modules.Servicos"] = "Serviços",
        ["Modules.Financeiro"] = "Financeiro",
        ["Modules.Configuracoes"] = "Configurações",
        ["Forms.Products"] = "Produtos",
        ["Forms.Brands"] = "Marcas",
        ["Forms.Classifications"] = "Classificações",
        ["Forms.TermDelivery"] = "Entrega",
        ["Forms.Users"] = "Usuários",
        ["Forms.PermissionGroups"] = "Grupos de permissões",
        ["Forms.Permissions"] = "Permissões",
        ["Forms.DatabaseMaintenance"] = "Manutenção do banco de dados",
        ["Actions.Search"] = "Pesquisar",
        ["Actions.View"] = "Visualizar",
        ["Actions.Create"] = "Adicionar",
        ["Actions.Edit"] = "Editar",
        ["Actions.Delete"] = "Excluir",
        ["Actions.Save"] = "Salvar",
        ["Actions.Cancel"] = "Cancelar",
        ["Actions.Add"] = "Adicionar",
        ["Actions.Remove"] = "Remover",
        ["Actions.Report"] = "Relatório",
        ["Actions.Close"] = "Fechar",
        ["Actions.Clear"] = "Limpar",
        ["Actions.Filter"] = "Filtrar",
        ["Actions.Copy"] = "Copiar",
        ["Actions.Cart"] = "Carrinho",
        ["User.Profile"] = "Perfil",
        ["User.Preferences"] = "Preferências",
        ["User.ChangePassword"] = "Alterar senha",
        ["User.Notifications"] = "Notificações",
        ["User.SignIn"] = "Entrar",
        ["User.SignOut"] = "Sair",
        ["Common.Calendar"] = "Calendário",
        ["Common.Clock"] = "Relógio",
        ["Common.Folder"] = "Pasta",
        ["Common.Database"] = "Banco de dados",
        ["Common.Backup"] = "Cópia de segurança",
        ["Common.Code"] = "Código",
        ["Common.Barcode"] = "Código de barras",
        ["Common.Status"] = "Bloqueado",
        ["Common.Unlocked"] = "Desbloqueado",
        ["Navigation.FirstPage"] = "Primeira página",
        ["Navigation.PreviousPage"] = "Página anterior",
        ["Navigation.NextPage"] = "Próxima página",
        ["Navigation.LastPage"] = "Última página",
        ["Navigation.Dropdown"] = "Abrir opções",
        ["Navigation.SortAscending"] = "Ordenar crescente",
        ["Navigation.SortDescending"] = "Ordenar decrescente"
    };

    private Geometry _geometry;
    private readonly IIconAppearanceService _iconAppearanceService;
    private ThemeVariant? _themeVariant;

    public IconCatalogItemViewModel(DecorIconId id, int iconWeight, IIconAppearanceService iconAppearanceService, ThemeVariant? themeVariant = null)
    {
        Id = id;
        _iconAppearanceService = iconAppearanceService;
        _themeVariant = themeVariant;
        _geometry = DecorIconCatalog.Get(id, iconWeight);
    }

    public IconCatalogItemViewModel(DecorIconId id, int iconWeight)
        : this(id, iconWeight, new StubIconAppearanceService())
    {
    }

    public DecorIconId Id { get; }
    public string Name => DisplayNames.GetValueOrDefault(Id.Value, GetShortName(Id.Value));
    public string FullId => Id.Value;
    public Geometry Geometry => _geometry;
    public string? CurrentColor => _iconAppearanceService.CurrentAppearance.GetColor(Id.Value);
    public IBrush ColorPreviewBrush => DecorIconColors.GetBrush(Id, _iconAppearanceService.CurrentAppearance, _themeVariant);

    public void RefreshColor(ThemeVariant? themeVariant = null)
    {
        if (themeVariant is not null)
            _themeVariant = themeVariant;
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(ColorPreviewBrush));
    }

    public async Task SetColorAsync(string? colorHex)
    {
        await _iconAppearanceService.SetIconColorOverrideAsync(Id.Value, colorHex);
        RefreshColor();
    }

    public void UpdateGeometry(int weight)
    {
        _geometry = DecorIconCatalog.Get(Id, weight);
        OnPropertyChanged(nameof(Geometry));
    }

    private sealed class StubIconAppearanceService : IIconAppearanceService
    {
        public IconAppearance CurrentAppearance { get; } = IconAppearance.Default;
        public event Action<IconAppearance>? AppearanceChanged
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetAppearanceAsync(int materialSymbolWeight, double strokeThickness, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetIconColorOverrideAsync(string iconId, string? colorHex, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetIconColorsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string GetShortName(string value)
    {
        var separatorIndex = value.LastIndexOf('.');
        return separatorIndex >= 0 && separatorIndex < value.Length - 1
            ? value[(separatorIndex + 1)..]
            : value;
    }
}
