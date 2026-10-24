using System.ComponentModel.DataAnnotations;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Configuration;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.AvaloniaUI.Views;

public partial class ProductsView : UserControl
{
    private readonly IUserSettingsService? _userSettingsService;
    private bool _columnOrderLoaded;
    private bool _catalogSortEventsAttached;
    private bool _catalogSortWasCleared;
    private CancellationTokenSource? _catalogCarouselScrollCancellation;

    public ProductsView()
    {
        InitializeComponent();
        _userSettingsService = (Avalonia.Application.Current as App)?.Services.GetService<IUserSettingsService>();
        ConfigureGridColumns();
        ProductsGrid.Loaded += ProductsGrid_Loaded;
        SizeChanged += (_, _) => UpdateCatalogLayout();
        UpdateCatalogLayout();
        ProductsGrid.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(async () =>
        {
            ApplyProductColumnWidths();
            await RestoreProductColumnOrderAsync();
        });
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.FocusTextInput());
        ProductsGrid.ValueMatchChanged += (_, eventArgs) =>
        {
            if (DataContext is ProductsViewModel viewModel)
                viewModel.SetValueMatch(eventArgs.ColumnName, eventArgs.MatchCount);
        };
    }

    public ProductsView(ProductsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private void ConfigureGridColumns()
    {
        ProductsGrid.InitializeColumns(
            dtoType: typeof(ProductDTO),
            getDisplayName: propertyName => (propertyName == nameof(ProductDTO.ProductID)
                ? "Código"
                : propertyName == nameof(ProductDTO.StockStatus)
                    ? "Situação"
                    : typeof(ProductDTO).GetProperty(propertyName)?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? propertyName)
                .ToUpperInvariant(),
            getColumnWidth: GetColumnWidth,
            propertyNames:
            [
                nameof(ProductDTO.ProductID),
                nameof(ProductDTO.ProductName),
                nameof(ProductDTO.BrandName),
                nameof(ProductDTO.StockUnitCode),
                nameof(ProductDTO.CurrentStockQuantity),
                nameof(ProductDTO.ReservedStockQuantity),
                nameof(ProductDTO.AvailableStockQuantity),
                nameof(ProductDTO.MinimumStock),
                nameof(ProductDTO.StockStatus)
            ]
        );
    }

    private async void ClassificationFilterFlyout_Opened(object? sender, EventArgs eventArgs)
    {
        if (DataContext is ProductsViewModel viewModel)
            await viewModel.EnsureClassificationTreeAsync();
    }

    private void ClassificationFilterConfirm_Click(object? sender, RoutedEventArgs eventArgs) =>
        this.FindControl<Button>("ClassificationFilterButton")?.Flyout?.Hide();

    private void CloseClassificationFilterFlyout_Click(object? sender, RoutedEventArgs eventArgs) =>
        (Resources["ClassificationFilterFlyout"] as Flyout)?.Hide();

    private void CloseCatalogIndicatorsFlyout_Click(object? sender, RoutedEventArgs eventArgs) =>
        (Resources["CatalogIndicatorsFlyout"] as Flyout)?.Hide();

    private void CloseExportProductsFlyout_Click(object? sender, RoutedEventArgs eventArgs) =>
        ExportProductsButton.Flyout?.Hide();

    private async void CatalogSummaryExpander_Expanded(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is ProductsViewModel viewModel)
            await viewModel.EnsureCatalogSummaryAsync();
    }

    private async void CatalogIndicatorCard_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not ProductsViewModel viewModel || sender is not Button button)
            return;

        await viewModel.ShowCatalogIndicatorAsync(button.Tag as string);
    }

    private void ProductsGrid_Loaded(object? sender, RoutedEventArgs eventArgs)
    {
        if (_catalogSortEventsAttached || ProductsGrid.FindControl<DataGrid>("InnerDataGrid") is not { } grid)
            return;

        _catalogSortEventsAttached = true;
        if (grid.CollectionView?.SortDescriptions is INotifyCollectionChanged sortDescriptions)
            sortDescriptions.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(UpdateCatalogSortDisplay);
        grid.Sorting += (_, _) => Dispatcher.UIThread.Post(
            UpdateCatalogSortDisplay,
            DispatcherPriority.Background);
        Dispatcher.UIThread.Post(UpdateCatalogSortDisplay);
    }

    private void UpdateCatalogSortDisplay()
    {
        if (DataContext is not ProductsViewModel viewModel
            || ProductsGrid.FindControl<DataGrid>("InnerDataGrid") is not { CollectionView: { } collectionView })
            return;

        var sort = collectionView.SortDescriptions.FirstOrDefault();
        if (sort is null || !sort.HasPropertyPath || string.IsNullOrWhiteSpace(sort.PropertyPath))
        {
            if (!_catalogSortWasCleared)
            {
                var defaultPath = ProductsGrid.DefaultSortMemberPath;
                var defaultColumn = ProductsGrid.Columns.FirstOrDefault(candidate => candidate.SortMemberPath == defaultPath);
                var defaultHeader = defaultColumn?.Header?.ToString() ?? defaultPath;
                if (!string.IsNullOrWhiteSpace(defaultHeader))
                    viewModel.SetCatalogSortDisplay($"{defaultHeader}: Crescente");
            }
            return;
        }

        _catalogSortWasCleared = false;
        var column = ProductsGrid.Columns.FirstOrDefault(candidate => candidate.SortMemberPath == sort.PropertyPath);
        var header = column?.Header?.ToString() ?? sort.PropertyPath;
        var direction = sort.Direction == System.ComponentModel.ListSortDirection.Ascending ? "Crescente" : "Decrescente";
        viewModel.SetCatalogSortDisplay($"{header}: {direction}");
    }

    private void ClearCatalogSortClicked(object? sender, RoutedEventArgs eventArgs)
    {
        _catalogSortWasCleared = true;
        if (ProductsGrid.FindControl<DataGrid>("InnerDataGrid")?.CollectionView?.SortDescriptions is { } sorts)
            sorts.Clear();
        if (DataContext is ProductsViewModel viewModel)
            viewModel.SetCatalogSortDisplay(null);
    }

    private void ApplyProductColumnWidths()
    {
        foreach (var propertyName in new[]
                 {
                     nameof(ProductDTO.ProductID),
                     nameof(ProductDTO.ProductName),
                     nameof(ProductDTO.BrandName),
                     nameof(ProductDTO.StockUnitCode),
                     nameof(ProductDTO.CurrentStockQuantity),
                     nameof(ProductDTO.ReservedStockQuantity),
                     nameof(ProductDTO.AvailableStockQuantity),
                     nameof(ProductDTO.MinimumStock),
                     nameof(ProductDTO.StockStatus)
                 })
        {
            SetColumnWidth(propertyName, DataGridLength.Auto);
        }
    }

    private void SetColumnWidth(string propertyName, DataGridLength width)
    {
        var column = ProductsGrid.Columns.FirstOrDefault(candidate => candidate.SortMemberPath == propertyName);
        if (column is not null) column.Width = width;
    }

    private static DataGridLength GetColumnWidth(string propertyName) => propertyName switch
    {
        nameof(ProductDTO.ProductID) => DataGridLength.Auto,
        nameof(ProductDTO.IsActive) => DataGridLength.Auto,
        nameof(ProductDTO.StockQuantity) => DataGridLength.Auto,
        nameof(ProductDTO.MinimumStock) => DataGridLength.Auto,
        nameof(ProductDTO.CurrentStockQuantity) => DataGridLength.Auto,
        nameof(ProductDTO.ReservedStockQuantity) => DataGridLength.Auto,
        nameof(ProductDTO.AvailableStockQuantity) => DataGridLength.Auto,
        nameof(ProductDTO.StockStatus) => DataGridLength.Auto,
        nameof(ProductDTO.StockUnitCode) => DataGridLength.Auto,
        nameof(ProductDTO.Barcode) => DataGridLength.Auto,
        nameof(ProductDTO.ProductName) => DataGridLength.Auto,
        nameof(ProductDTO.BrandName) => DataGridLength.Auto,
        nameof(ProductDTO.ManufacturerRef) => DataGridLength.Auto,
        nameof(ProductDTO.Dimensions) => DataGridLength.Auto,
        _ => DataGridLength.Auto
    };

    private void UpdateCatalogLayout()
    {
        var layout = this.FindControl<Grid>("CatalogLayout");
        if (layout is null) return;

        var summaryExpander = this.FindControl<Expander>("CatalogSummaryExpander");
        var searchToolsFrame = this.FindControl<Border>("SearchToolsFrame");
        var searchActionsLayout = this.FindControl<Grid>("CatalogSearchActionsLayout");
        ConfigureCatalogLayout(layout, summaryExpander, searchToolsFrame, ProductsGridFrame, ProductDetailsPanel, Bounds.Width);
        ConfigureCatalogSearchActionsLayout(searchActionsLayout, searchToolsFrame?.Bounds.Width ?? Bounds.Width);
    }

    private async Task ScrollCatalogIndicatorsAsync(int direction)
    {
        const double cardStep = 232;
        var maximumOffset = Math.Max(0, CatalogSummaryCarousel.Extent.Width - CatalogSummaryCarousel.Viewport.Width);
        var initialOffset = CatalogSummaryCarousel.Offset.X;
        var targetOffset = Math.Clamp(initialOffset + direction * cardStep, 0, maximumOffset);
        if (Math.Abs(targetOffset - initialOffset) < 0.1) return;

        _catalogCarouselScrollCancellation?.Cancel();
        _catalogCarouselScrollCancellation?.Dispose();
        _catalogCarouselScrollCancellation = new CancellationTokenSource();
        var cancellationToken = _catalogCarouselScrollCancellation.Token;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (stopwatch.ElapsedMilliseconds < 220)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = stopwatch.ElapsedMilliseconds / 220d;
                var easedProgress = 1 - Math.Pow(1 - progress, 3);
                var offset = initialOffset + (targetOffset - initialOffset) * easedProgress;
                CatalogSummaryCarousel.Offset = new Vector(offset, 0);
                await Task.Delay(16, cancellationToken);
            }

            CatalogSummaryCarousel.Offset = new Vector(targetOffset, 0);
        }
        catch (OperationCanceledException)
        {
            // A new navigation request takes over the animation.
        }
    }

    private async void PreviousCatalogIndicator_Click(object? sender, RoutedEventArgs eventArgs) => await ScrollCatalogIndicatorsAsync(-1);

    private async void NextCatalogIndicator_Click(object? sender, RoutedEventArgs eventArgs) => await ScrollCatalogIndicatorsAsync(1);

    private void ConfigureCatalogSearchActionsLayout(Grid? layout, double width)
    {
        if (layout is null) return;

        var compact = width < 720;
        layout.ColumnDefinitions = compact
            ? new ColumnDefinitions("Auto,Auto,Auto")
            : new ColumnDefinitions("*,Auto,Auto,Auto");
        layout.RowDefinitions = compact ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");

        Grid.SetRow(SearchTextBox, 0);
        Grid.SetColumn(SearchTextBox, 0);
        Grid.SetColumnSpan(SearchTextBox, compact ? 3 : 1);

        var actions = new[] { ClassificationFilterButton, ExportProductsButton, ConfigureColumnsButton };
        for (var index = 0; index < actions.Length; index++)
        {
            Grid.SetRow(actions[index], compact ? 1 : 0);
            Grid.SetColumn(actions[index], compact ? index : index + 1);
        }
    }

    private static void ConfigureCatalogLayout(Grid layout, Expander? summaryExpander, Border? searchToolsFrame, Border productsGridFrame, Border productDetailsPanel, double width)
    {
        var compact = width < 1120;
        layout.ColumnDefinitions = compact ? new ColumnDefinitions("*") : new ColumnDefinitions("2*,420");
        layout.RowDefinitions = compact
            ? new RowDefinitions("Auto,Auto,3*,2*")
            : new RowDefinitions("Auto,Auto,*");
        if (summaryExpander is not null)
        {
            Grid.SetColumn(summaryExpander, 0);
            Grid.SetRow(summaryExpander, 0);
            Grid.SetRowSpan(summaryExpander, 1);
        }
        if (searchToolsFrame is not null)
        {
            Grid.SetColumn(searchToolsFrame, 0);
            Grid.SetRow(searchToolsFrame, 1);
            Grid.SetRowSpan(searchToolsFrame, 1);
        }
        Grid.SetColumn(productsGridFrame, 0);
        Grid.SetRow(productsGridFrame, 2);
        Grid.SetRowSpan(productsGridFrame, 1);
        Grid.SetColumn(productDetailsPanel, compact ? 0 : 1);
        Grid.SetRow(productDetailsPanel, compact ? 3 : 0);
        Grid.SetRowSpan(productDetailsPanel, 1);
        if (!compact)
            Grid.SetRowSpan(productDetailsPanel, 3);
    }

    private async void ExportProductsCsvClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ExportProductsAsync(ProductExportFormat.Csv);

    private async void ExportProductsXlsClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ExportProductsAsync(ProductExportFormat.Xls);

    private async void ExportProductsXlsxClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ExportProductsAsync(ProductExportFormat.Xlsx);

    private async void ExportProductsOdtClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ExportProductsAsync(ProductExportFormat.Odt);

    private async Task ExportProductsAsync(ProductExportFormat format)
    {
        if (DataContext is not ProductsViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var (extension, description) = format switch
        {
            ProductExportFormat.Csv => ("csv", "Arquivo CSV"),
            ProductExportFormat.Xls => ("xls", "Planilha Excel 97-2003"),
            ProductExportFormat.Xlsx => ("xlsx", "Planilha Excel"),
            ProductExportFormat.Odt => ("odt", "Planilha OpenDocument"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        try
        {
            var columns = ProductsGrid.Columns
                .Where(column => column.IsVisible && !string.IsNullOrWhiteSpace(column.SortMemberPath))
                .Select(column => (Header: column.Header?.ToString() ?? string.Empty,
                    Property: typeof(ProductDTO).GetProperty(column.SortMemberPath!)))
                .Where(column => column.Property is not null)
                .Select(column => (column.Header, Property: column.Property!))
                .ToArray();

            var exportDirectory = Path.Combine(Path.GetTempPath(), "Decor", "Exportacoes");
            Directory.CreateDirectory(exportDirectory);
            var exportPath = Path.Combine(exportDirectory, $"produtos-{DateTime.Now:yyyyMMdd-HHmmss-fff}.{extension}");
            await using (var stream = new FileStream(exportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                await ProductExporter.WriteAsync(stream, viewModel.Listing.AllItems, columns, format);

            Process.Start(new ProcessStartInfo
            {
                FileName = exportPath,
                UseShellExecute = true
            });

            viewModel.ReportProductsExported(exportPath);
        }
        catch
        {
            viewModel.ReportProductsExportFailure();
        }
    }

    private void ConfigureColumnsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Control anchor) return;

        var items = new StackPanel { Spacing = 4 };
        PopulateColumnMenu(items);
        Flyout? flyout = null;
        var closeIcon = new PathIcon
        {
            Width = 10,
            Height = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        DecorIcon.SetId(closeIcon, DecorIconId.Actions.Close);

        var closeButton = new Button
        {
            Content = closeIcon,
            Width = 18,
            Height = 18,
            MinWidth = 18,
            MinHeight = 18,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        ToolTip.SetTip(closeButton, "Fechar");
        AutomationProperties.SetName(closeButton, "Fechar");
        closeButton.Click += (_, _) => flyout?.Hide();

        var content = new StackPanel { Spacing = 8 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        header.Children.Add(new TextBlock
        {
            Text = "Ordenar colunas",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        Grid.SetColumn(closeButton, 1);
        header.Children.Add(closeButton);
        content.Children.Add(header);
        content.Children.Add(items);

        flyout = new Flyout
        {
            Content = new Border
            {
                Classes = { "column-menu" },
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Child = content
            }
        };
        flyout.ShowAt(anchor);
    }

    private void PopulateColumnMenu(StackPanel items)
    {
        items.Children.Clear();
        foreach (var column in ProductsGrid.Columns.OrderBy(column => column.DisplayIndex))
        {
            if (column.Header is not string header || string.IsNullOrWhiteSpace(column.SortMemberPath)) continue;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 3 };
            var visibility = new CheckBox { Content = header, IsChecked = column.IsVisible, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            visibility.Click += (_, _) => column.IsVisible = visibility.IsChecked == true;
            row.Children.Add(visibility);

            var columnIndex = column.DisplayIndex;
            var moveUp = CreateColumnMoveButton("↑", "Mover coluna para cima", columnIndex > 0);
            Grid.SetColumn(moveUp, 1);
            moveUp.Click += async (_, _) => await MoveColumnAsync(column, -1, items);
            row.Children.Add(moveUp);

            var moveDown = CreateColumnMoveButton("↓", "Mover coluna para baixo", columnIndex < ProductsGrid.Columns.Count - 1);
            Grid.SetColumn(moveDown, 2);
            moveDown.Click += async (_, _) => await MoveColumnAsync(column, 1, items);
            row.Children.Add(moveDown);
            items.Children.Add(row);
        }
    }

    private static Button CreateColumnMoveButton(string glyph, string description, bool enabled)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = glyph, FontSize = 16, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
            Width = 28,
            Height = 28,
            MinHeight = 28,
            Padding = new Thickness(0),
            IsEnabled = enabled
        };
        button.Classes.Add("product-secondary-action");
        ToolTip.SetTip(button, description);
        AutomationProperties.SetName(button, description);
        return button;
    }

    private async Task MoveColumnAsync(DataGridColumn column, int offset, StackPanel menuItems)
    {
        var columns = ProductsGrid.Columns;
        var index = column.DisplayIndex;
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= columns.Count) return;

        column.DisplayIndex = targetIndex;
        PopulateColumnMenu(menuItems);
        await SaveProductColumnOrderAsync();
    }

    private async Task RestoreProductColumnOrderAsync()
    {
        if (_columnOrderLoaded || _userSettingsService is null) return;
        _columnOrderLoaded = true;
        try
        {
            var json = await _userSettingsService.GetValueAsync(DecorUserSettings.ProductGridColumnOrder);
            var savedOrder = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<string[]>(json);
            if (savedOrder is null || savedOrder.Length == 0) return;

            var byProperty = ProductsGrid.Columns
                .Where(column => !string.IsNullOrWhiteSpace(column.SortMemberPath))
                .ToDictionary(column => column.SortMemberPath!, StringComparer.Ordinal);
            var ordered = savedOrder.Where(byProperty.ContainsKey).Select(name => byProperty[name])
                .Concat(ProductsGrid.Columns.Where(column => string.IsNullOrWhiteSpace(column.SortMemberPath)
                    || !savedOrder.Contains(column.SortMemberPath, StringComparer.Ordinal)))
                .ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                if (ordered[index].DisplayIndex != index)
                    ordered[index].DisplayIndex = index;
            }
        }
        catch (Exception)
        {
            _columnOrderLoaded = false;
        }
    }

    private async Task SaveProductColumnOrderAsync()
    {
        if (_userSettingsService is null) return;
        try
        {
            var order = ProductsGrid.Columns.Where(column => !string.IsNullOrWhiteSpace(column.SortMemberPath))
                .OrderBy(column => column.DisplayIndex)
                .Select(column => column.SortMemberPath).ToArray();
            await _userSettingsService.SetValueAsync(DecorUserSettings.ProductGridColumnOrder, JsonSerializer.Serialize(order));
        }
        catch (InvalidOperationException)
        {
            // The grid remains usable when no authenticated user can persist settings.
        }
    }

}
