using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Decor.AvaloniaUI.Controls.GridStyling;

namespace Decor.AvaloniaUI.Controls;

public sealed class ValueMatchChangedEventArgs(string columnName, int matchCount) : EventArgs
{
    public string ColumnName { get; } = columnName;
    public int MatchCount { get; } = matchCount;
}

/// <summary>
/// A custom DataGrid control that provides:
/// - Automatic "no data" message when ItemsSource is empty
/// - Column styling based on data type (colors, alignment)
/// - All visual enhancements centralized in the grid, not in Views
/// 
/// This control encapsulates the behaviors that were previously scattered across Views,
/// ensuring consistency across the application and reducing code duplication.
/// </summary>
public partial class DecorDataGridControl : UserControl
{
    private DataGrid? _innerGrid;
    private Panel? _emptyOverlay;
    private TextBlock? _emptyMessageTextBlock;
    private IGridControlStyler _styler;
    private Dictionary<string, PropertyInfo> _propertyCache = [];
    private bool _isUpdatingEmptyState;
    private INotifyCollectionChanged? _observedItemsSource;
    private PropertyInfo? _highlightedProperty;
    private object? _highlightedValue;
    private Border? _focusedCellContent;
    private Control? _rowsPresenter;
    private ScrollBar? _verticalScrollBar;
    private ScrollBar? _horizontalScrollBar;

    // Store pending initialization if called before Loaded
    private Type? _pendingDtoType;
    private Func<string, string>? _pendingGetDisplayName;
    private Func<string, DataGridLength>? _pendingGetColumnWidth;
    private Type? _configuredDtoType;
    private Func<string, string>? _configuredGetDisplayName;
    private Func<string, DataGridLength>? _configuredGetColumnWidth;
    private IReadOnlyList<string>? _configuredPropertyNames;
    private readonly List<DataGridColumn> _additionalColumns = [];

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<DecorDataGridControl, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<DecorDataGridControl, object?>(nameof(SelectedItem));

    /// <summary>Column the query orders by; defaults to the code column.</summary>
    public static readonly StyledProperty<string?> DefaultSortMemberPathProperty =
        AvaloniaProperty.Register<DecorDataGridControl, string?>(nameof(DefaultSortMemberPath));

    public string? DefaultSortMemberPath
    {
        get => GetValue(DefaultSortMemberPathProperty);
        set => SetValue(DefaultSortMemberPathProperty, value);
    }

    public event EventHandler<ValueMatchChangedEventArgs>? ValueMatchChanged;

    public DecorDataGridControl()
    {
        InitializeComponent();
        _styler = new DefaultGridControlStyler();
        _styler.ApplyTheme(CreateThemeColors());
        var application = global::Avalonia.Application.Current;
        if (application is not null)
            application.ActualThemeVariantChanged += (_, _) => RefreshTheme();
        
        // Initialize named elements after template is applied
        this.Loaded += (s, e) => InitializeGridElements();
    }

    private void InitializeGridElements()
    {
        _innerGrid = this.FindControl<DataGrid>("InnerDataGrid");
        _emptyOverlay = this.FindControl<Panel>("EmptyOverlay");
        _emptyMessageTextBlock = this.FindControl<TextBlock>("EmptyMessageTextBlock");

        if (_innerGrid is not null)
            _innerGrid.TemplateApplied += (_, _) => Dispatcher.UIThread.Post(KeepScrollBarsExpanded);
        Dispatcher.UIThread.Post(KeepScrollBarsExpanded);

        _innerGrid?.AddHandler(PointerPressedEvent, OnGridPointerPressed, handledEventsToo: true);

        // Monitor property changes
        this.PropertyChanged += (s, e) =>
        {
            if (e.Property == ItemsSourceProperty)
            {
                OnItemsSourceChanged(ItemsSource);
            }
        };

        OnItemsSourceChanged(ItemsSource);

        // If InitializeColumns was called before Loaded, apply it now
        if (_pendingDtoType != null)
        {
            InitializeColumns(_pendingDtoType, _pendingGetDisplayName, _pendingGetColumnWidth, _configuredPropertyNames);
            _pendingDtoType = null;
            _pendingGetDisplayName = null;
            _pendingGetColumnWidth = null;
        }
    }

    private void KeepScrollBarsExpanded()
    {
        if (_innerGrid is null)
            return;

        DetachScrollBarVisibilityHandlers();

        var visualDescendants = _innerGrid.GetVisualDescendants().ToArray();
        _rowsPresenter = visualDescendants.OfType<Control>()
            .FirstOrDefault(control => control.Name == "PART_RowsPresenter");
        _verticalScrollBar = visualDescendants.OfType<ScrollBar>()
            .FirstOrDefault(scrollBar => scrollBar.Name == "PART_VerticalScrollbar");
        _horizontalScrollBar = visualDescendants.OfType<ScrollBar>()
            .FirstOrDefault(scrollBar => scrollBar.Name == "PART_HorizontalScrollbar");

        foreach (var scrollViewer in _innerGrid.GetVisualDescendants().OfType<ScrollViewer>())
        {
            scrollViewer.AllowAutoHide = false;
        }

        foreach (var scrollBar in visualDescendants.OfType<ScrollBar>())
        {
            scrollBar.AllowAutoHide = false;

            if (scrollBar.Orientation == Avalonia.Layout.Orientation.Vertical)
                scrollBar.Width = 14;
            else
                scrollBar.Height = 14;
        }

        AttachScrollBarVisibilityHandlers();
        UpdateRowsPresenterMargin();
    }

    private void AttachScrollBarVisibilityHandlers()
    {
        if (_verticalScrollBar is not null)
            _verticalScrollBar.PropertyChanged += OnScrollBarPropertyChanged;
        if (_horizontalScrollBar is not null)
            _horizontalScrollBar.PropertyChanged += OnScrollBarPropertyChanged;
    }

    private void DetachScrollBarVisibilityHandlers()
    {
        if (_verticalScrollBar is not null)
            _verticalScrollBar.PropertyChanged -= OnScrollBarPropertyChanged;
        if (_horizontalScrollBar is not null)
            _horizontalScrollBar.PropertyChanged -= OnScrollBarPropertyChanged;
    }

    private void OnScrollBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == Visual.IsVisibleProperty)
            Dispatcher.UIThread.Post(UpdateRowsPresenterMargin);
    }

    private void UpdateRowsPresenterMargin()
    {
        if (_rowsPresenter is null)
            return;

        _rowsPresenter.Margin = new Thickness(
            0,
            0,
            _verticalScrollBar?.IsVisible == true ? 14 : 0,
            _horizontalScrollBar?.IsVisible == true ? 14 : 0);
    }

    /// <summary>
    /// Gets or sets the ItemsSource for the inner DataGrid.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected item in the inner DataGrid.
    /// </summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Exposed DataGrid columns collection for Views to configure columns.
    /// </summary>
    public IList<DataGridColumn> Columns
    {
        get
        {
            if (_innerGrid == null)
                throw new InvalidOperationException("Grid not initialized. Ensure control is loaded before accessing Columns.");
            return _innerGrid.Columns;
        }
    }

    /// <summary>
    /// Gets or sets whether column headers are visible.
    /// </summary>
    public DataGridHeadersVisibility HeadersVisibility
    {
        get => _innerGrid?.HeadersVisibility ?? DataGridHeadersVisibility.Column;
        set
        {
            if (_innerGrid != null)
                _innerGrid.HeadersVisibility = value;
        }
    }

    /// <summary>
    /// Gets or sets whether the grid is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get => _innerGrid?.IsReadOnly ?? true;
        set
        {
            if (_innerGrid != null)
                _innerGrid.IsReadOnly = value;
        }
    }

    /// <summary>
    /// Gets or sets whether users can resize columns.
    /// </summary>
    public bool CanUserResizeColumns
    {
        get => _innerGrid?.CanUserResizeColumns ?? true;
        set
        {
            if (_innerGrid != null)
                _innerGrid.CanUserResizeColumns = value;
        }
    }

    /// <summary>
    /// Gets or sets whether users can sort columns.
    /// </summary>
    public bool CanUserSortColumns
    {
        get => _innerGrid?.CanUserSortColumns ?? true;
        set
        {
            if (_innerGrid != null)
                _innerGrid.CanUserSortColumns = value;
        }
    }

    /// <summary>
    /// Gets or sets the grid lines visibility.
    /// </summary>
    public DataGridGridLinesVisibility GridLinesVisibility
    {
        get => _innerGrid?.GridLinesVisibility ?? DataGridGridLinesVisibility.All;
        set
        {
            if (_innerGrid != null)
                _innerGrid.GridLinesVisibility = value;
        }
    }

    /// <summary>
    /// Gets or sets the empty message text.
    /// </summary>
    public string EmptyMessage
    {
        get => _emptyMessageTextBlock?.Text ?? "Não há registros para exibir.";
        set
        {
            if (_emptyMessageTextBlock != null)
                _emptyMessageTextBlock.Text = value;
        }
    }

    /// <summary>
    /// Gets the current grid control styler.
    /// </summary>
    public IGridControlStyler Styler => _styler;

    /// <summary>
    /// Updates the property cache from the current ItemsSource.
    /// This is called automatically when ItemsSource changes.
    /// </summary>
    public void UpdatePropertyCacheFromDataSource()
    {
        _propertyCache.Clear();

        var enumerable = ItemsSource as IEnumerable;
        if (enumerable != null)
        {
            var firstItem = enumerable.Cast<object>().FirstOrDefault();
            if (firstItem != null)
            {
                CacheProperties(firstItem.GetType());
            }
        }
    }

    /// <summary>
    /// Initializes grid columns based on a DTO type and applies styling.
    /// </summary>
    public void InitializeColumns(Type dtoType, Func<string, string>? getDisplayName = null, Func<string, DataGridLength>? getColumnWidth = null,
        IReadOnlyList<string>? propertyNames = null)
    {
        _configuredDtoType = dtoType;
        _configuredGetDisplayName = getDisplayName;
        _configuredGetColumnWidth = getColumnWidth;
        _configuredPropertyNames = propertyNames;

        // If grid not initialized yet, store for later when Loaded fires
        if (_innerGrid == null)
        {
            _pendingDtoType = dtoType;
            _pendingGetDisplayName = getDisplayName;
            _pendingGetColumnWidth = getColumnWidth;
            return;
        }

        _innerGrid.Columns.Clear();
        CacheProperties(dtoType);

        var properties = propertyNames is null
            ? dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            : propertyNames.Select(name => dtoType.GetProperty(name)
                ?? throw new ArgumentException($"Propriedade '{name}' não existe em {dtoType.Name}.", nameof(propertyNames))).ToArray();

        foreach (var property in properties)
        {
            var browsableAttr = property.GetCustomAttribute<BrowsableAttribute>();
            if (browsableAttr is not null && !browsableAttr.Browsable)
                continue;

            var displayAttr = property.GetCustomAttribute<DisplayAttribute>();
            var displayName = getDisplayName?.Invoke(property.Name) ?? displayAttr?.GetName() ?? property.Name;
            displayName = property.Name == "IsActive" ? "Estado" : DecorGridHeader.NormalizeLabel(displayName);
            var width = getColumnWidth?.Invoke(property.Name) ?? new DataGridLength(1, DataGridLengthUnitType.Star);

            // Auto mede o cabeçalho mesmo quando a pesquisa ainda não trouxe registros.
            // Isso evita que uma largura fixa ou Star menor corte o texto do cabeçalho.
            width = DataGridLength.Auto;

            DataGridColumn column = property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?)
                ? new DataGridCheckBoxColumn()
                : CreateTextColumn(property);

            column.Header = displayName;
            column.HeaderTemplate = new DecorGridHeader();
            column.Width = width;
            column.SortMemberPath = property.Name;
            if (!string.IsNullOrEmpty(DecorGridHeader.GetIconId(displayName).Value))
                column.MinWidth = 130;

            if (column is DataGridCheckBoxColumn checkColumn)
            {
                checkColumn.Binding = new Binding(property.Name);
            }

            _innerGrid.Columns.Add(column);
        }

        foreach (var column in _additionalColumns) _innerGrid.Columns.Add(column);

        var defaultSort = _innerGrid.Columns.FirstOrDefault(column => DecorGridHeader.GetIconId(column.Header as string ?? string.Empty)
            == Icons.DecorIconId.Common.Code) ?? _innerGrid.Columns.FirstOrDefault();
        DecorGridSorting.SetDefaultSortMemberPath(_innerGrid, DefaultSortMemberPath ?? defaultSort?.SortMemberPath);
    }

    public void AddColumn(DataGridColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (_additionalColumns.Contains(column)) return;
        _additionalColumns.Add(column);
        _innerGrid?.Columns.Add(column);
    }

    private DataGridTemplateColumn CreateTextColumn(PropertyInfo propertyInfo)
    {
        var foreground = _styler.GetColumnForeground(propertyInfo);
        var alignment = _styler.GetColumnAlignment(propertyInfo);

        return new DataGridTemplateColumn
        {
            CellTemplate = new FuncDataTemplate<object?>((_, _) =>
            {
                var cellContent = new Border
                {
                    Padding = new Thickness(4, 0),
                    Tag = propertyInfo
                };
                var textBlock = new TextBlock
                {
                    Foreground = foreground,
                    TextAlignment = alignment,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                textBlock.Bind(TextBlock.TextProperty, new Binding(propertyInfo.Name));
                cellContent.Child = textBlock;
                cellContent.AttachedToVisualTree += (_, _) => ApplyHighlight(cellContent);
                return cellContent;
            })
        };
    }

    private GridControlThemeColors CreateThemeColors() => GridControlThemeColors.Create(
        global::Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Dark);

    private void RefreshTheme()
    {
        _styler.ApplyTheme(CreateThemeColors());

        if (_configuredDtoType is not null)
            InitializeColumns(_configuredDtoType, _configuredGetDisplayName, _configuredGetColumnWidth, _configuredPropertyNames);

        UpdateCellHighlights();
    }

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var visual = e.Source as Visual;
        var cell = visual as DataGridCell ?? visual?.GetVisualAncestors().OfType<DataGridCell>().FirstOrDefault();
        if (cell?.DataContext is null)
            return;

        var cellContent = cell.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Tag is PropertyInfo);
        if (cellContent?.Tag is not PropertyInfo propertyInfo)
            return;

        _highlightedProperty = propertyInfo;
        _highlightedValue = propertyInfo.GetValue(cell.DataContext);
        _focusedCellContent = cellContent;
        UpdateCellHighlights();
        var displayName = propertyInfo.Name == "IsActive" ? "Estado" : DecorGridHeader.NormalizeLabel(
            _configuredGetDisplayName?.Invoke(propertyInfo.Name)
            ?? propertyInfo.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? propertyInfo.Name);
        var matchCount = ItemsSource?.Cast<object>().Count(item => Equals(propertyInfo.GetValue(item), _highlightedValue)) ?? 0;
        ValueMatchChanged?.Invoke(this, new ValueMatchChangedEventArgs(displayName, matchCount));
    }

    private void UpdateCellHighlights()
    {
        if (_innerGrid is null)
            return;

        foreach (var cellContent in _innerGrid.GetVisualDescendants().OfType<Border>())
            ApplyHighlight(cellContent);
    }

    private void ApplyHighlight(Border cellContent)
    {
        if (cellContent.Tag is not PropertyInfo propertyInfo)
            return;

        cellContent.Background = null;
        if (_highlightedProperty?.Name != propertyInfo.Name || cellContent.DataContext is null)
            return;

        var value = propertyInfo.GetValue(cellContent.DataContext);
        if (!Equals(value, _highlightedValue))
            return;

        cellContent.Background = ReferenceEquals(cellContent, _focusedCellContent)
            ? _styler.Theme.FocusedCellBackground
            : _styler.Theme.ValueMatchBackground;
    }

    /// <summary>
    /// Caches the properties of a DTO type for quick access during styling.
    /// </summary>
    private void CacheProperties(Type dtoType)
    {
        _propertyCache.Clear();
        var properties = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            _propertyCache[prop.Name] = prop;
        }
    }

    /// <summary>
    /// Handles changes to the ItemsSource and sets up event handlers for empty state updates.
    /// </summary>
    private void OnItemsSourceChanged(IEnumerable? itemsSource)
    {
        if (_observedItemsSource is not null)
            _observedItemsSource.CollectionChanged -= OnItemsSourceCollectionChanged;

        _observedItemsSource = itemsSource as INotifyCollectionChanged;
        if (_observedItemsSource is not null)
            _observedItemsSource.CollectionChanged += OnItemsSourceCollectionChanged;

        UpdatePropertyCacheFromDataSource();
        UpdateEmptyMessageVisibility();
    }

    /// <summary>
    /// Handles collection change events to update the empty message visibility.
    /// </summary>
    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _highlightedProperty = null;
            _highlightedValue = null;
            _focusedCellContent = null;
            ValueMatchChanged?.Invoke(this, new ValueMatchChangedEventArgs(string.Empty, 0));
        }

        UpdateEmptyMessageVisibility();
        UpdateCellHighlights();
    }

    /// <summary>
    /// Updates the visibility of the empty message overlay based on whether the grid has items.
    /// </summary>
    private void UpdateEmptyMessageVisibility()
    {
        if (_isUpdatingEmptyState || _emptyOverlay == null)
            return;

        try
        {
            _isUpdatingEmptyState = true;

            bool isEmpty = !(ItemsSource as IEnumerable)?.Cast<object>().Any() ?? true;
            _emptyOverlay.IsVisible = isEmpty;
        }
        finally
        {
            _isUpdatingEmptyState = false;
        }
    }
}
