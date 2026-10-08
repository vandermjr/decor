using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public enum LookupSearchContext
{
    Customer,
    Employee,
    Partner,
    Product
}

public sealed record LookupSearchItem(int Code, string Name, string Detail, string Category, object Value);

public sealed class ContextualSearchViewModel : INotifyPropertyChanged
{
    private readonly ICustomerService _customerService;
    private readonly IEmployeeService _employeeService;
    private readonly IPartnerService _partnerService;
    private readonly IProductService _productService;
    private string _searchText = string.Empty;
    private string _title = "Pesquisar";
    private string _searchHint = "Digite para pesquisar";
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private LookupSearchItem? _selectedItem;
    private LookupSearchContext _context;

    public ContextualSearchViewModel(ICustomerService customerService, IEmployeeService employeeService,
        IPartnerService partnerService, IProductService productService)
    {
        _customerService = customerService;
        _employeeService = employeeService;
        _partnerService = partnerService;
        _productService = productService;
        SearchCommand = new RelayCommand(async () => await SearchAsync(), () => !IsBusy);
    }

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
        Title = context switch
        {
            LookupSearchContext.Customer => "Pesquisar cliente",
            LookupSearchContext.Employee => "Pesquisar vendedor",
            LookupSearchContext.Partner => "Pesquisar parceiro",
            LookupSearchContext.Product => "Pesquisar produto ou serviço",
            _ => "Pesquisar"
        };
        SearchHint = context switch
        {
            LookupSearchContext.Customer => "Nome, CPF/CNPJ ou telefone",
            LookupSearchContext.Employee => "Nome do funcionário",
            LookupSearchContext.Partner => "Nome ou documento do parceiro",
            LookupSearchContext.Product => "Nome, código ou código de barras",
            _ => "Digite para pesquisar"
        };
        SearchText = string.Empty;
        await SearchAsync();
    }

    public async Task SearchAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var results = _context switch
            {
                LookupSearchContext.Customer => (await _customerService.SearchCustomersAsync(SearchText, 1, 200))
                    .Where(item => item.IsActive)
                    .Select(item => new LookupSearchItem(item.CustomerID, item.Name ?? "(sem nome)",
                        JoinDetails(item.Document, item.Phone), "Cliente", item)).ToArray(),
                LookupSearchContext.Employee => (await _employeeService.SearchEmployeesAsync(SearchText, 1, 200))
                    .Where(item => item.IsActive)
                    .Select(item => new LookupSearchItem(item.EmployeeID, item.Name ?? "(sem nome)",
                        item.JobTitle ?? item.Document ?? string.Empty, "Vendedor", item)).ToArray(),
                LookupSearchContext.Partner => (await _partnerService.SearchPartnersAsync(SearchText, 1, 200))
                    .Where(item => item.IsActive)
                    .Select(item => new LookupSearchItem(item.PartnerID, item.Name ?? "(sem nome)",
                        item.Document ?? item.PartnerType.ToString(), "Parceiro", item)).ToArray(),
                LookupSearchContext.Product => (await _productService.SearchProductsAsync(SearchText, 1, 200))
                    .Where(item => item.IsActive)
                    .Select(item => new LookupSearchItem(item.ProductID, item.Description ?? "(sem descrição)",
                        JoinDetails(item.Barcode, item.SalePrice?.ToString("C2")),
                        item.ProductType == 2 ? "Serviço" : "Produto", item)).ToArray(),
                _ => []
            };
            Results.Clear();
            foreach (var item in results) Results.Add(item);
            SelectedItem = null;
            OnPropertyChanged(nameof(ResultCount));
        }
        catch (UnauthorizedAccessException)
        {
            Results.Clear();
            SelectedItem = null;
            ErrorMessage = "Você não tem permissão para pesquisar este tipo de registro.";
        }
        catch (Exception)
        {
            Results.Clear();
            SelectedItem = null;
            ErrorMessage = "Não foi possível carregar os resultados da pesquisa.";
        }
        finally { IsBusy = false; }
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