using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class TermDeliveryViewModel : INotifyPropertyChanged
{
    private readonly IProductService _productService;
    private string _productIdText = string.Empty;
    private double _quantity;
    private string? _customerName;
    private string? _customerAddress;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private Term? _selectedTerm;
    private bool _showDeleteConfirmation;
    private Term? _termToDelete;
    private readonly Dictionary<string, string> _fieldErrors = [];

    public TermDeliveryViewModel(IProductService productService)
    {
        _productService = productService;

        AddProductCommand = new RelayCommand(async () => await AddProductAsync(), () => !IsBusy);
        RemoveTermCommand = new RelayCommand(BeginRemove, () => SelectedTerm is not null && !IsBusy);
        GenerateReportCommand = new RelayCommand(async () => await GenerateReportAsync(), () => !IsBusy && Terms.Count > 0);
        ConfirmDeleteCommand = new RelayCommand(async () => await ConfirmDeleteAsync(), () => !IsBusy);
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => true);
    }

    public ObservableCollection<Term> Terms { get; } = [];

    public ICommand AddProductCommand { get; }
    public ICommand RemoveTermCommand { get; }
    public ICommand GenerateReportCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }

    public Term? SelectedTerm
    {
        get => _selectedTerm;
        set => SetField(ref _selectedTerm, value);
    }

    public bool ShowDeleteConfirmation
    {
        get => _showDeleteConfirmation;
        private set => SetField(ref _showDeleteConfirmation, value);
    }

    public Term? TermToDelete => _termToDelete;

    public string DeleteConfirmationMessage => TermToDelete != null 
        ? $"Tem certeza que deseja remover \"{TermToDelete.Description}\" do termo de entrega?" 
        : string.Empty;

    public string ProductIdText
    {
        get => _productIdText;
        set => SetField(ref _productIdText, value);
    }

    public double Quantity
    {
        get => _quantity;
        set => SetField(ref _quantity, value);
    }

    // Propriedades de erro para validação
    public string ProductIdError => GetFieldError("ProductId");
    public string QuantityError => GetFieldError("Quantity");

    public bool HasProductIdError => HasFieldError("ProductId");
    public bool HasQuantityError => HasFieldError("Quantity");

    public string? CustomerName
    {
        get => _customerName;
        set => SetField(ref _customerName, value);
    }

    public string? CustomerAddress
    {
        get => _customerAddress;
        set => SetField(ref _customerAddress, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public Task InitializeAsync()
    {
        StatusMessage = "Termo de entrega pronto.";
        return Task.CompletedTask;
    }

    public void BeginRemove()
    {
        if (SelectedTerm is null)
        {
            return;
        }

        _termToDelete = SelectedTerm;
        ShowDeleteConfirmation = true;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public void CancelDelete()
    {
        _termToDelete = null;
        ShowDeleteConfirmation = false;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public Task ConfirmDeleteAsync()
    {
        if (_termToDelete is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            Terms.Remove(_termToDelete);
            StatusMessage = $"Produto \"{_termToDelete.Description}\" removido do termo.";
            _termToDelete = null;
            ShowDeleteConfirmation = false;
            SelectedTerm = null;
            RaiseCommandStates();
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível remover o produto do termo.";
        }

        return Task.CompletedTask;
    }

    private async Task AddProductAsync()
    {
        ClearFieldErrors();
        bool hasErrors = false;

        if (string.IsNullOrWhiteSpace(ProductIdText))
        {
            _fieldErrors["ProductId"] = "Informe o código do produto.";
            hasErrors = true;
        }
        else if (!int.TryParse(ProductIdText, out _))
        {
            _fieldErrors["ProductId"] = "Código do produto deve ser um número.";
            hasErrors = true;
        }

        if (Quantity <= 0)
        {
            _fieldErrors["Quantity"] = "Informe a quantidade maior que zero.";
            hasErrors = true;
        }

        if (hasErrors)
        {
            NotifyErrorPropertiesChanged();
            StatusMessage = "Corrija os erros antes de continuar.";
            return;
        }

        IsBusy = true;

        try
        {
            var productId = Convert.ToInt32(ProductIdText);
            var product = await _productService.GetProductByIdAsync(productId);

            var existing = Terms.FirstOrDefault(term => term.ProductID == productId);
            if (existing is null)
            {
                Terms.Add(new Term
                {
                    ProductID = product.ProductID,
                    Description = product.Description,
                    Qtde = Quantity
                });
            }
            else
            {
                existing.Qtde += Quantity;
            }

            ProductIdText = string.Empty;
            Quantity = 0;
            ClearFieldErrors();
            StatusMessage = $"Produto {product.Description} incluído no termo.";
            RaiseCommandStates();
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível localizar o produto.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task GenerateReportAsync()
    {
        if (Terms.Count == 0)
        {
            StatusMessage = "Não há itens para gerar o relatório.";
            return Task.CompletedTask;
        }

        IsBusy = true;

        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Decor");
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, "termo-entrega.csv");
            using var writer = new StreamWriter(filePath, false);
            writer.WriteLine("Cliente;Endereco");
            writer.WriteLine($"{CustomerName ?? string.Empty};{CustomerAddress ?? string.Empty}");
            writer.WriteLine();
            writer.WriteLine("ProductID;Description;Qtde");

            foreach (var term in Terms)
            {
                writer.WriteLine($"{term.ProductID};{term.Description};{term.Qtde}");
            }

            StatusMessage = $"Relatório gerado em: {filePath}";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível gerar o relatório.";
        }
        finally
        {
            IsBusy = false;
        }

        return Task.CompletedTask;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void RaiseCommandStates()
    {
        if (AddProductCommand is RelayCommand addCommand)
        {
            addCommand.RaiseCanExecuteChanged();
        }

        if (GenerateReportCommand is RelayCommand generateCommand)
        {
            generateCommand.RaiseCanExecuteChanged();
        }
    }

    private string GetFieldError(string fieldName) => _fieldErrors.TryGetValue(fieldName, out var error) ? error : string.Empty;

    private bool HasFieldError(string fieldName) => _fieldErrors.ContainsKey(fieldName);

    private void ClearFieldErrors()
    {
        _fieldErrors.Clear();
        NotifyErrorPropertiesChanged();
    }

    private void NotifyErrorPropertiesChanged()
    {
        OnPropertyChanged(nameof(ProductIdError));
        OnPropertyChanged(nameof(HasProductIdError));
        OnPropertyChanged(nameof(QuantityError));
        OnPropertyChanged(nameof(HasQuantityError));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
