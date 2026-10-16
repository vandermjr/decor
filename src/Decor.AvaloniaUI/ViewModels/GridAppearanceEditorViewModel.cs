using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Decor.AvaloniaUI.Controls.GridStyling;
using Decor.Core.Common;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class GridAppearanceEditorViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> ColorLabels = new Dictionary<string, string>
    {
        [nameof(GridControlThemeColors.GridBackground)] = "Fundo da grade",
        [nameof(GridControlThemeColors.GridLines)] = "Divisórias",
        [nameof(GridControlThemeColors.EmptyGridMessageColor)] = "Mensagem sem resultados",
        [nameof(GridControlThemeColors.HeaderBackground)] = "Fundo dos cabeçalhos",
        [nameof(GridControlThemeColors.HeaderText)] = "Texto dos cabeçalhos",
        [nameof(GridControlThemeColors.CellBackground)] = "Fundo das células",
        [nameof(GridControlThemeColors.CellText)] = "Texto padrão das células",
        [nameof(GridControlThemeColors.ValueMatchBackground)] = "Células com valores iguais",
        [nameof(GridControlThemeColors.FocusedCellBackground)] = "Célula de referência",
        [nameof(GridControlThemeColors.DataType_Int)] = "Números inteiros",
        [nameof(GridControlThemeColors.DataType_Text)] = "Textos",
        [nameof(GridControlThemeColors.DataType_DateTime)] = "Datas e horários",
        [nameof(GridControlThemeColors.DataType_Decimal_Positive)] = "Valores positivos",
        [nameof(GridControlThemeColors.DataType_Decimal_Negative)] = "Valores negativos",
        [nameof(GridControlThemeColors.DataType_Boolean)] = "Valores booleanos",
        [nameof(GridControlThemeColors.StockStatusNormal)] = "Estoque normal",
        [nameof(GridControlThemeColors.StockStatusAttention)] = "Estoque em atenção",
        [nameof(GridControlThemeColors.StockStatusNegative)] = "Estoque negativo",
        [nameof(GridControlThemeColors.StockStatusMadeToOrder)] = "Estoque sob demanda"
    };

    private readonly GridAppearanceSettingsService _settings;
    private string _selectedPalette;
    private string _fontFamily;
    private double _fontSize;
    private double _rowHeight;
    private string _headerFontWeight;
    private string _cellFontWeight;
    private string _descriptionFontWeight;
    private string _headerAlignment;
    private string _gridLinesOption;
    private string _headersVisibilityOption;
    private string _status = string.Empty;

    public ObservableCollection<GridAppearanceColorOption> Colors { get; } = [];
    public ObservableCollection<GridAppearanceColorOption> SurfaceColors { get; } = [];
    public ObservableCollection<GridAppearanceColorOption> SemanticColors { get; } = [];
    public ObservableCollection<string> PaletteOptions { get; } = ["Claro", "Escuro"];
    public ObservableCollection<string> FontFamilies { get; } = [.. GridAppearanceSettingsService.AvailableFontFamilies];
    public ObservableCollection<string> FontWeights { get; } = [.. GridAppearanceSettingsService.AvailableFontWeights];
    public ObservableCollection<string> GridLinesOptions { get; } = ["Todas", "Horizontais", "Verticais", "Nenhuma"];
    public ObservableCollection<string> HeadersVisibilityOptions { get; } = ["Colunas", "Linhas", "Colunas e linhas", "Ocultos"];
    public ObservableCollection<string> HeaderAlignmentOptions { get; } = ["Esquerda", "Centro", "Direita"];
    public ObservableCollection<string> RowAlignmentOptions { get; } = ["Automático", "Esquerda", "Centro", "Direita"];
    public ObservableCollection<GridAppearanceAlignmentOption> RowAlignments { get; } = [];
    public ObservableCollection<GridAppearancePreviewRow> PreviewRows { get; } =
    [
        new(1042, "Mesa lateral Arco", 129.90m, true, DateTime.Today, "Normal"),
        new(1048, "Luminária de piso Nilo", 489.00m, false, DateTime.Today.AddDays(-2), "Atenção"),
        new(1053, "Cadeira Serena", -24.50m, true, DateTime.Today.AddDays(-7), "Negativo"),
        new(1057, "Cadeira Serena", 229.00m, true, DateTime.Today.AddDays(-9), "Sob Demanda")
    ];

    public GridAppearanceEditorViewModel(GridAppearanceSettingsService settings, IThemeService themeService)
    {
        _settings = settings;
        _selectedPalette = themeService.CurrentTheme == DecorThemeStyle.Dark ? "Escuro" : "Claro";
        _fontFamily = settings.FontFamily;
        _fontSize = settings.FontSize;
        _rowHeight = settings.RowHeight;
        _headerFontWeight = settings.HeaderFontWeight;
        _cellFontWeight = settings.CellFontWeight;
        _descriptionFontWeight = settings.DescriptionFontWeight;
        _headerAlignment = ToHeaderAlignmentOption(settings.HeaderAlignment);
        _gridLinesOption = ToGridLinesOption(settings.GridLinesVisibility);
        _headersVisibilityOption = ToHeadersVisibilityOption(settings.HeadersVisibility);
        foreach (var type in GridAppearanceSettingsService.RowAlignmentTypes)
            RowAlignments.Add(new GridAppearanceAlignmentOption(type, GetAlignmentLabel(type), RowAlignmentOptions,
                settings.GetRowAlignment(type), PersistRowAlignment));
        RefreshColors();
    }

    public string SelectedPalette
    {
        get => _selectedPalette;
        set
        {
            if (!SetField(ref _selectedPalette, value))
                return;
            OnPropertyChanged(nameof(PreviewIsDark));
            RefreshColors();
        }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            if (SetField(ref _fontFamily, value)) PersistTypography();
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (SetField(ref _fontSize, Math.Clamp(value, 9, MaximumFontSize))) PersistTypography();
        }
    }

    public double MaximumFontSize => GridAppearanceSettingsService.GetMaximumFontSize(RowHeight);

    public double RowHeight
    {
        get => _rowHeight;
        set
        {
            if (!SetField(ref _rowHeight, Math.Clamp(value, 20, 80)))
                return;

            OnPropertyChanged(nameof(MaximumFontSize));
            if (_fontSize > MaximumFontSize)
                SetField(ref _fontSize, MaximumFontSize, nameof(FontSize));
            PersistTypography();
        }
    }

    public string HeaderFontWeight
    {
        get => _headerFontWeight;
        set
        {
            if (SetField(ref _headerFontWeight, value)) PersistTypography();
        }
    }

    public string CellFontWeight
    {
        get => _cellFontWeight;
        set
        {
            if (SetField(ref _cellFontWeight, value)) PersistTypography();
        }
    }

    public string DescriptionFontWeight
    {
        get => _descriptionFontWeight;
        set
        {
            if (SetField(ref _descriptionFontWeight, value)) PersistTypography();
        }
    }

    public string HeaderAlignment
    {
        get => _headerAlignment;
        set
        {
            if (SetField(ref _headerAlignment, value)) PersistTypography();
        }
    }

    public string GridLinesOption
    {
        get => _gridLinesOption;
        set
        {
            if (SetField(ref _gridLinesOption, value)) PersistTypography();
        }
    }

    public string HeadersVisibilityOption
    {
        get => _headersVisibilityOption;
        set
        {
            if (SetField(ref _headersVisibilityOption, value)) PersistTypography();
        }
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public bool PreviewIsDark => SelectedPalette == "Escuro";

    public void ResetToDefaults()
    {
        _settings.ResetToDefaults();
        _fontFamily = _settings.FontFamily;
        _fontSize = _settings.FontSize;
        _rowHeight = _settings.RowHeight;
        _headerFontWeight = _settings.HeaderFontWeight;
        _cellFontWeight = _settings.CellFontWeight;
        _descriptionFontWeight = _settings.DescriptionFontWeight;
        _headerAlignment = ToHeaderAlignmentOption(_settings.HeaderAlignment);
        _gridLinesOption = ToGridLinesOption(_settings.GridLinesVisibility);
        _headersVisibilityOption = ToHeadersVisibilityOption(_settings.HeadersVisibility);
        OnPropertyChanged(nameof(FontFamily));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(MaximumFontSize));
        OnPropertyChanged(nameof(RowHeight));
        OnPropertyChanged(nameof(HeaderFontWeight));
        OnPropertyChanged(nameof(CellFontWeight));
        OnPropertyChanged(nameof(DescriptionFontWeight));
        OnPropertyChanged(nameof(HeaderAlignment));
        foreach (var alignment in RowAlignments)
            alignment.SetSelectedAlignment(_settings.GetRowAlignment(alignment.DataType), notifyChanged: false);
        OnPropertyChanged(nameof(GridLinesOption));
        OnPropertyChanged(nameof(HeadersVisibilityOption));
        RefreshColors();
        Status = "A aparência padrão das grades foi restaurada.";
    }

    private bool IsDarkPalette => SelectedPalette == "Escuro";

    private void RefreshColors()
    {
        Colors.Clear();
        SurfaceColors.Clear();
        SemanticColors.Clear();
        foreach (var propertyName in GridAppearanceSettingsService.AvailableColorProperties)
        {
            var option = new GridAppearanceColorOption(ColorLabels[propertyName], propertyName,
                _settings.GetColor(IsDarkPalette, propertyName), UpdateColor);
            Colors.Add(option);
            (IsSemanticColor(propertyName) ? SemanticColors : SurfaceColors).Add(option);
        }
    }

    private static bool IsSemanticColor(string propertyName) => propertyName.StartsWith("DataType_", StringComparison.Ordinal)
        || propertyName.StartsWith("StockStatus", StringComparison.Ordinal);

    private void UpdateColor(GridAppearanceColorOption option, string value)
    {
        try
        {
            Color.Parse(value);
            _settings.SetColor(IsDarkPalette, option.PropertyName, value);
            Status = string.Empty;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            Status = "Informe uma cor hexadecimal válida, por exemplo #2C3035.";
        }
    }

    private void PersistTypography()
    {
        try
        {
            _settings.SetTypography(FontFamily, FontSize, RowHeight, HeaderFontWeight, CellFontWeight, DescriptionFontWeight,
                ParseAlignmentOption(HeaderAlignment),
                ParseGridLinesOption(GridLinesOption), ParseHeadersVisibilityOption(HeadersVisibilityOption));
            Status = string.Empty;
        }
        catch (Exception)
        {
            Status = "Não foi possível salvar as opções visuais da grade.";
        }
    }

    private void PersistRowAlignment(GridAppearanceAlignmentOption option, string value)
    {
        try
        {
            _settings.SetRowAlignment(option.DataType, ParseAlignmentOption(value));
            Status = string.Empty;
        }
        catch (Exception)
        {
            Status = "Não foi possível salvar o alinhamento das linhas.";
        }
    }

    private static string ParseAlignmentOption(string option) => option switch
    {
        "Esquerda" => "Left",
        "Direita" => "Right",
        "Automático" => "Auto",
        _ => "Center"
    };

    private static string ToHeaderAlignmentOption(string alignment) => alignment switch
    {
        "Left" => "Esquerda",
        "Right" => "Direita",
        _ => "Centro"
    };

    private static string GetAlignmentLabel(string type) => type switch
    {
        "Integer" => "Números inteiros",
        "Text" => "Textos",
        "DateTime" => "Datas e horários",
        "Decimal" => "Números decimais",
        "Boolean" => "Booleanos",
        "Status" => "Status",
        _ => type
    };

    private static DataGridGridLinesVisibility ParseGridLinesOption(string option) => option switch
    {
        "Horizontais" => DataGridGridLinesVisibility.Horizontal,
        "Verticais" => DataGridGridLinesVisibility.Vertical,
        "Nenhuma" => DataGridGridLinesVisibility.None,
        _ => DataGridGridLinesVisibility.All
    };

    private static string ToGridLinesOption(DataGridGridLinesVisibility visibility) => visibility switch
    {
        DataGridGridLinesVisibility.Horizontal => "Horizontais",
        DataGridGridLinesVisibility.Vertical => "Verticais",
        DataGridGridLinesVisibility.None => "Nenhuma",
        _ => "Todas"
    };

    private static DataGridHeadersVisibility ParseHeadersVisibilityOption(string option) => option switch
    {
        "Linhas" => DataGridHeadersVisibility.Row,
        "Colunas e linhas" => DataGridHeadersVisibility.All,
        "Ocultos" => DataGridHeadersVisibility.None,
        _ => DataGridHeadersVisibility.Column
    };

    private static string ToHeadersVisibilityOption(DataGridHeadersVisibility visibility) => visibility switch
    {
        DataGridHeadersVisibility.Row => "Linhas",
        DataGridHeadersVisibility.All => "Colunas e linhas",
        DataGridHeadersVisibility.None => "Ocultos",
        _ => "Colunas"
    };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class GridAppearanceAlignmentOption(
    string dataType,
    string label,
    IReadOnlyList<string> alignmentOptions,
    string selectedAlignment,
    Action<GridAppearanceAlignmentOption, string> update) : INotifyPropertyChanged
{
    private string _selectedAlignment = selectedAlignment switch
    {
        "Left" => "Esquerda",
        "Center" => "Centro",
        "Right" => "Direita",
        _ => "Automático"
    };

    public string DataType { get; } = dataType;
    public string Label { get; } = label;
    public IReadOnlyList<string> AlignmentOptions { get; } = alignmentOptions;
    public string SelectedAlignment
    {
        get => _selectedAlignment;
        set => SetSelectedAlignment(value, notifyChanged: true);
    }

    public void SetSelectedAlignment(string alignment, bool notifyChanged)
    {
        var mapped = alignment switch
        {
            "Left" => "Esquerda",
            "Center" => "Centro",
            "Right" => "Direita",
            "Automático" => "Automático",
            "Esquerda" => "Esquerda",
            "Centro" => "Centro",
            "Direita" => "Direita",
            _ => "Automático"
        };
        if (_selectedAlignment == mapped)
            return;
        _selectedAlignment = mapped;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedAlignment)));
        if (notifyChanged)
            update(this, mapped);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class GridAppearanceColorOption(
    string label,
    string propertyName,
    string colorHex,
    Action<GridAppearanceColorOption, string> update) : INotifyPropertyChanged
{
    private string _colorHex = colorHex;

    public string Label { get; } = label;
    public string PropertyName { get; } = propertyName;
    public IBrush PreviewBrush
    {
        get
        {
            try { return new SolidColorBrush(Color.Parse(ColorHex)); }
            catch (FormatException) { return Brushes.Transparent; }
        }
    }

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (_colorHex == value) return;
            _colorHex = value;
            OnPropertyChanged(nameof(ColorHex));
            OnPropertyChanged(nameof(PreviewBrush));
            update(this, value);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record GridAppearancePreviewRow(
    [property: Display(Name = "Código")] int Code,
    [property: Display(Name = "Descrição")] string Description,
    [property: Display(Name = "Valor")] decimal Amount,
    [property: Display(Name = "Ativo")] bool IsActive,
    [property: Display(Name = "Atualizado em")] DateTime UpdatedAt,
    [property: Display(Name = "Reposição")] string StockStatus);