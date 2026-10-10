using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public enum LookupSearchContext
{
    Customer,
    Employee,
    Partner,
    Product,
    Service
}

public sealed record LookupSearchItem(int Code, string Name, string Detail, string Category, object Value);

public sealed class ContextualSearchViewModel : INotifyPropertyChanged
{
    private readonly ICustomerService _customerService;
    private readonly IEmployeeService _employeeService;
    private readonly IPartnerService _partnerService;
    private readonly IProductService _productService;
    private readonly IServiceCatalogService? _serviceCatalogService;
    private readonly IAuthorizationService? _authorization;
    private string _searchText = string.Empty;
    private string _title = "Pesquisar";
    private string _searchHint = "Digite para pesquisar";
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private LookupSearchItem? _selectedItem;
    private LookupSearchContext _context;
    private CancellationTokenSource? _searchCancellation;
    private int _searchGeneration;

    public ContextualSearchViewModel(ICustomerService customerService, IEmployeeService employeeService,
        IPartnerService partnerService, IProductService productService, IServiceCatalogService? serviceCatalogService = null,
        IAuthorizationService? authorization = null)
    {
        _customerService = customerService;
        _employeeService = employeeService;
        _partnerService = partnerService;
        _productService = productService;
        _serviceCatalogService = serviceCatalogService;
        _authorization = authorization;
        SearchCommand = new RelayCommand(async () => await SearchAsync(), () => !IsBusy && HasViewPermission);
    }

    private bool HasViewPermission => _authorization?.HasPermission(_context switch
    {
        LookupSearchContext.Customer => DecorPermissions.CustomersView,
        LookupSearchContext.Employee => DecorPermissions.EmployeesView,
        LookupSearchContext.Partner => DecorPermissions.PartnersView,
        LookupSearchContext.Product => DecorPermissions.ProductsView,
        LookupSearchContext.Service => DecorPermissions.ServicesView,
        _ => string.Empty
    }) ?? true;

    public ObservableCollection<LookupSearchItem> Results { get; } = [];
    public ICommand SearchCommand { get; }
    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }
    public string Title { get => _title; private set => SetField(ref _title, value); }
    public string SearchHint { get => _searchHint; private set => SetField(ref _searchHint, value); }
    public string ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value) && SearchCommand is RelayCommand command) command.RaiseCanExecuteChanged(); } }
    public LookupSearchItem? SelectedItem { get => _selectedItem; set => SetField(ref _selectedItem, value); }
    public string ResultCount => Results.Count == 1 ? "1 resultado" : $"{Results.Count} resultados";

    public async Task InitializeAsync(LookupSearchContext context)
    {
        _context = context;
        ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
        Title = context switch
        {
            LookupSearchContext.Customer => "Pesquisar cliente",
            LookupSearchContext.Employee => "Pesquisar vendedor",
            LookupSearchContext.Partner => "Pesquisar parceiro",
            LookupSearchContext.Product => "Pesquisar produto",
            LookupSearchContext.Service => "Pesquisar serviço",
            _ => "Pesquisar"
        };
        SearchHint = context switch
        {
            LookupSearchContext.Customer => "Nome, CPF/CNPJ ou telefone",
            LookupSearchContext.Employee => "Nome do funcionário",
            LookupSearchContext.Partner => "Nome ou documento do parceiro",
            LookupSearchContext.Product => "Nome, código ou código de barras",
            LookupSearchContext.Service => "Descrição ou código do serviço",
            _ => "Digite para pesquisar"
        };
        SearchText = string.Empty;
        await SearchAsync();
    }

    public async Task SearchAsync()
    {
        if (!HasViewPermission) return;
        var generation = ++_searchGeneration;
        _searchCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        var cancellationToken = cancellation.Token;
        var searchText = SearchText;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var results = _context switch
            {
                LookupSearchContext.Customer => (await LoadAllPagesAsync(
                    (page, size, token) => _customerService.SearchCustomersAsync(searchText, page, size, token), cancellationToken))
                    .Where(item => item.IsActive)
                    .Select(item => new LookupSearchItem(item.CustomerID, item.Name ?? "(sem nome)",
                        JoinDetails(item.Document, item.Phone), "Cliente", item)).ToArray(),
                LookupSearchContext.Employee => (await LoadAllPagesAsync(
                    (page, size, token) => _employeeService.SearchEmployeesAsync(searchText, page, size, token), cancellationToken))
                    .Where(item => item.IsActive)
                    .Select(item => new LookupSearchItem(item.EmployeeID, item.Name ?? "(sem nome)",
                        item.JobTitle ?? item.Document ?? string.Empty, "Vendedor", item)).ToArray(),
                LookupSearchContext.Partner => (await LoadAllPagesAsync(
                    (page, size, token) => _partnerService.SearchPartnersAsync(searchText, page, size, token), cancellationToken))
                    .Where(item => item.IsActive)
                    .Select(item => new LookupSearchItem(item.PartnerID, item.Name ?? "(sem nome)",
                        item.Document ?? item.PartnerType.ToString(), "Parceiro", item)).ToArray(),
                LookupSearchContext.Product => (await LoadAllPagesAsync(
                    (page, size, token) => _productService.SearchProductsAsync(searchText, page, size, token), cancellationToken))
                    .Where(item => item.IsActive && item.ProductType == (int)Decor.Core.Entities.ProductType.Good)
                    .Select(item => new LookupSearchItem(item.ProductID, item.Description ?? "(sem descrição)",
                        JoinDetails(item.Barcode, item.SalePrice?.ToString("C2")), "Produto", item)).ToArray(),
                LookupSearchContext.Service => (await LoadAllPagesAsync(
                    (page, size, token) => (_serviceCatalogService ?? throw new InvalidOperationException("O catálogo de serviços não está disponível."))
                        .SearchServicesAsync(searchText, page, size, token), cancellationToken))
                    .Where(item => item.IsActive)
                    .Select(item => new LookupSearchItem(item.ServiceID, item.Description ?? "(sem descrição)",
                        item.SalePrice?.ToString("C2") ?? string.Empty, "Serviço", item)).ToArray(),
                _ => []
            };
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _searchGeneration) return;
            Results.Clear();
            foreach (var item in results) Results.Add(item);
            SelectedItem = null;
            OnPropertyChanged(nameof(ResultCount));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (UnauthorizedAccessException) when (generation == _searchGeneration)
        {
            Results.Clear();
            SelectedItem = null;
            ErrorMessage = "Você não tem permissão para pesquisar este tipo de registro.";
            OnPropertyChanged(nameof(ResultCount));
        }
        catch (Exception) when (generation == _searchGeneration)
        {
            Results.Clear();
            SelectedItem = null;
            ErrorMessage = "Não foi possível carregar os resultados da pesquisa.";
            OnPropertyChanged(nameof(ResultCount));
        }
        catch (Exception) when (generation != _searchGeneration)
        {
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                _searchCancellation = null;
                IsBusy = false;
            }
        }
    }

    private static async Task<List<T>> LoadAllPagesAsync<T>(
        Func<int, int, CancellationToken, Task<IEnumerable<T>>> search, CancellationToken cancellationToken)
    {
        const int pageSize = 200;
        var results = new List<T>();
        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = (await search(page, pageSize, cancellationToken)).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            results.AddRange(batch);
            if (batch.Length < pageSize) return results;
        }
    }

    private static string JoinDetails(params string?[] parts) =>
        string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}