using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Decor.AvaloniaUI.Icons;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Decor.Core.DTOs;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.AvaloniaUI.Views;

public partial class QuotesView : UserControl
{
    private readonly IServiceProvider? _services;
    private readonly INavigationService? _navigationService;

    public QuotesView()
    {
        InitializeComponent();
        QuotesGrid.InitializeColumns(typeof(QuoteListItem), propertyName =>
        {
            var dtoPropertyName = propertyName switch
            {
                nameof(QuoteListItem.QuoteID) => nameof(QuoteDTO.QuoteID),
                nameof(QuoteListItem.CreatedAt) => nameof(QuoteDTO.CreatedAt),
                nameof(QuoteListItem.CustomerName) => nameof(QuoteDTO.CustomerName),
                nameof(QuoteListItem.CreatedByEmployeeName) => nameof(QuoteDTO.CreatedByEmployeeName),
                nameof(QuoteListItem.Status) => nameof(QuoteDTO.ListStatus),
                nameof(QuoteListItem.TotalDisplay) => nameof(QuoteDTO.ListTotal),
                _ => propertyName
            };
            var displayName = typeof(QuoteDTO).GetProperty(dtoPropertyName)?.GetCustomAttribute<DisplayAttribute>()?.GetName()
                ?? propertyName;
            return propertyName == nameof(QuoteListItem.QuoteID) ? "Código" : displayName;
        }, propertyName => propertyName == nameof(QuoteListItem.QuoteID)
            ? new DataGridLength(100)
            : new DataGridLength(1, DataGridLengthUnitType.Star),
            [nameof(QuoteListItem.QuoteID), nameof(QuoteListItem.CreatedAt), nameof(QuoteListItem.CustomerName),
                nameof(QuoteListItem.CreatedByEmployeeName), nameof(QuoteListItem.Status), nameof(QuoteListItem.TotalDisplay)]);
        QuotesGrid.ValueMatchChanged += (_, args) =>
        {
            if (DataContext is QuotesViewModel viewModel) viewModel.SetValueMatch(args.ColumnName, args.MatchCount);
        };
        foreach (var catalogGrid in new[] { ProductsCatalogGrid, ServicesCatalogGrid })
            catalogGrid.InitializeColumns(typeof(QuoteProductOption), propertyName => propertyName switch
            {
                nameof(QuoteProductOption.Code) => "Código",
                nameof(QuoteProductOption.Description) => "Descrição",
                nameof(QuoteProductOption.Category) => "Categoria",
                nameof(QuoteProductOption.Price) => "Preço de venda",
                nameof(QuoteProductOption.Unit) => "Unidade",
                _ => propertyName
            }, propertyName => propertyName switch
            {
                nameof(QuoteProductOption.Description) => new DataGridLength(1, DataGridLengthUnitType.Star),
                nameof(QuoteProductOption.Code) => new DataGridLength(55),
                nameof(QuoteProductOption.Unit) => new DataGridLength(65),
                _ => new DataGridLength(120)
            }, [nameof(QuoteProductOption.Code), nameof(QuoteProductOption.Description),
                nameof(QuoteProductOption.Unit), nameof(QuoteProductOption.Price)]);
        QuoteItemsGrid.InitializeColumns(typeof(QuoteLineOption), propertyName => propertyName switch
        {
            nameof(QuoteLineOption.Item) => "Item",
            nameof(QuoteLineOption.ProductName) => "Descrição",
            nameof(QuoteLineOption.Category) => "Categoria",
            nameof(QuoteLineOption.UnitPrice) => "Preço de venda",
            nameof(QuoteLineOption.Quantity) => "Quantidade",
            nameof(QuoteLineOption.Total) => "Valor Total",
            _ => propertyName
        }, propertyName => propertyName == nameof(QuoteLineOption.ProductName)
            ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(propertyName switch
            {
                nameof(QuoteLineOption.Item) => 55,
                nameof(QuoteLineOption.Category) => 85,
                nameof(QuoteLineOption.UnitPrice) => 120,
                _ => 105
            }),
            [nameof(QuoteLineOption.Item), nameof(QuoteLineOption.ProductName), nameof(QuoteLineOption.Category),
                nameof(QuoteLineOption.UnitPrice), nameof(QuoteLineOption.Quantity), nameof(QuoteLineOption.Total)]);
        QuoteItemsGrid.AddColumn(new DataGridTemplateColumn
        {
            Header = string.Empty,
            Width = new DataGridLength(36),
            CanUserSort = false,
            CanUserResize = false,
            CellTemplate = new FuncDataTemplate<QuoteLineOption>((line, _) =>
            {
                var icon = new Avalonia.Controls.Shapes.Path { Width = 14, Height = 14, Stretch = Stretch.Uniform };
                DecorIcon.SetId(icon, DecorIconId.Actions.Delete);
                var button = new Button { Content = icon, Width = 28, Height = 26, MinHeight = 0,
                    Padding = new Avalonia.Thickness(0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                icon.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, new Binding(nameof(Button.Foreground)) { Source = button });
                ToolTip.SetTip(button, "Remover item");
                if (line is not null && DataContext is QuotesViewModel model)
                {
                    button.Bind(Button.IsEnabledProperty, new Binding(nameof(QuotesViewModel.CanManageSections)) { Source = model });
                    if (model.Sections.Any(section => section.DTO.QuoteSectionID == line.DTO.QuoteSectionID
                        && section.DTO.Status == (int)Decor.Core.Entities.QuoteSectionStatus.Draft))
                        button.Bind(Button.IsVisibleProperty, new Binding(nameof(Button.IsEffectivelyEnabled)) { Source = button });
                    else
                        button.IsVisible = false;
                }
                button.Click += async (_, _) =>
                {
                    if (line is not null && DataContext is QuotesViewModel viewModel)
                        await viewModel.DeleteLineAsync(line);
                };
                return button;
            })
        });
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.FocusTextInput());
    }

    public QuotesView(QuotesViewModel viewModel, IServiceProvider services, INavigationService navigationService) : this()
    {
        _services = services;
        _navigationService = navigationService;
        DataContext = viewModel;
        viewModel.PdfRequested += GeneratePdf;
        _ = viewModel.InitializeAsync();
    }

    private async void SearchCustomer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await OpenLookupAsync(LookupSearchContext.Customer);

    private async void CopyQuoteNumber_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is QuotesViewModel viewModel && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(viewModel.CurrentQuoteId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private async void SearchEmployee_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await OpenLookupAsync(LookupSearchContext.Employee);

    private void QuoteDateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is not Calendar { SelectedDate: { } selectedDate } || DataContext is not QuotesViewModel viewModel)
            return;

        viewModel.SelectedQuoteDate = selectedDate;
        ChooseQuoteDateButton.Flyout?.Hide();
    }

    private async void SearchPartner_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await OpenLookupAsync(LookupSearchContext.Partner);

    private async void SearchProduct_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await OpenLookupAsync(DataContext is QuotesViewModel { CatalogTabIndex: 1 }
            ? LookupSearchContext.Service : LookupSearchContext.Product);

    private async void GeneratePdf(object? sender, EventArgs args)
    {
        if (sender is not QuotesViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel) return;
        try
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Salvar PDF do orçamento",
                SuggestedFileName = $"Orcamento-{viewModel.CurrentQuoteId}.pdf",
                DefaultExtension = "pdf",
                ShowOverwritePrompt = true,
                FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }]
            });
            if (file is null) return;
            await using var output = await file.OpenWriteAsync();
            QuotePdfExporter.Export(output, viewModel.CurrentQuoteId, viewModel.QuoteDate,
                viewModel.CustomerSummary, viewModel.EmployeeSummary, viewModel.QuoteState,
                viewModel.AllLines, viewModel.QuoteTotal, viewModel.DiscountAmount, viewModel.Notes);
            viewModel.ReportPdfResult();
        }
        catch (Exception exception) { viewModel.ReportPdfResult(exception.Message); }
    }

    private async Task OpenLookupAsync(LookupSearchContext context)
    {
        if (_services is null || _navigationService is null || DataContext is not QuotesViewModel quoteViewModel
            || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        Control lookupView;
        INotifyPropertyChanged lookupViewModel;
        Func<object?> getSelected;
        string selectionProperty;
        var title = context switch
        {
            LookupSearchContext.Customer => "Selecionar cliente",
            LookupSearchContext.Employee => "Selecionar funcionário",
            LookupSearchContext.Partner => "Selecionar parceiro",
            LookupSearchContext.Product => "Selecionar produto",
            _ => "Selecionar serviço"
        };

        switch (context)
        {
            case LookupSearchContext.Customer:
            {
                var viewModel = _services.GetRequiredService<CustomersViewModel>();
                viewModel.SelectionMode = true;
                lookupView = new CustomersView(viewModel);
                lookupViewModel = viewModel;
                getSelected = () => viewModel.SelectedItem is { IsActive: true } customer ? customer : null;
                selectionProperty = nameof(viewModel.SelectedItem);
                viewModel.SearchCommand.Execute(null);
                break;
            }
            case LookupSearchContext.Employee:
            {
                var viewModel = _services.GetRequiredService<EmployeesViewModel>();
                viewModel.SelectionMode = true;
                lookupView = new EmployeesView(viewModel);
                lookupViewModel = viewModel;
                getSelected = () => viewModel.SelectedEmployee is { IsActive: true } employee ? employee : null;
                selectionProperty = nameof(viewModel.SelectedEmployee);
                break;
            }
            case LookupSearchContext.Partner:
            {
                var viewModel = _services.GetRequiredService<PartnersViewModel>();
                viewModel.SelectionMode = true;
                lookupView = new PartnersView(viewModel);
                lookupViewModel = viewModel;
                getSelected = () => viewModel.SelectedPartner is { IsActive: true } partner ? partner : null;
                selectionProperty = nameof(viewModel.SelectedPartner);
                viewModel.SearchCommand.Execute(null);
                break;
            }
            case LookupSearchContext.Product:
            {
                var viewModel = _services.GetRequiredService<ProductsViewModel>();
                viewModel.SelectionMode = true;
                lookupView = new ProductsView(viewModel);
                lookupViewModel = viewModel;
                getSelected = () => viewModel.SelectedProduct is { IsActive: true } product ? product : null;
                selectionProperty = nameof(viewModel.SelectedProduct);
                viewModel.SearchCommand.Execute(null);
                break;
            }
            case LookupSearchContext.Service:
            {
                var viewModel = _services.GetRequiredService<ServicesViewModel>();
                viewModel.SelectionMode = true;
                lookupView = new ServicesView(viewModel);
                lookupViewModel = viewModel;
                getSelected = () => viewModel.SelectedService is { IsActive: true } service ? service : null;
                selectionProperty = nameof(viewModel.SelectedService);
                viewModel.SearchCommand.Execute(null);
                break;
            }
            default:
                return;
        }

        var dialog = new LookupSelectionWindow(lookupView, lookupViewModel, getSelected, selectionProperty, title);
        var accepted = await _navigationService.ShowDialogAsync<bool>(owner, dialog);
        if (accepted && dialog.SelectedValue is { } selected)
            quoteViewModel.ApplyLookupSelection(context, selected);
    }

}