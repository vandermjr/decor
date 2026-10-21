using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class StockViewModel : INotifyPropertyChanged
{
    private readonly IProductService _productService;
    private readonly IStockMovementService _stockMovementService;
    private readonly IStockLocationService _stockLocationService;
    private IReadOnlyList<ProductDTO> _products = [];
    private int _productId;

    public StockViewModel(IProductService productService)
        : this(productService, new EmptyStockMovementService(), new EmptyStockLocationService())
    {
    }

    public StockViewModel(IProductService productService, IStockMovementService stockMovementService, IStockLocationService stockLocationService)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _stockMovementService = stockMovementService ?? throw new ArgumentNullException(nameof(stockMovementService));
        _stockLocationService = stockLocationService ?? throw new ArgumentNullException(nameof(stockLocationService));

        RegisterEntryCommand = new RelayCommand(async () => await RegisterEntryAsync(), () => CanRegisterMovement);
        RegisterExitCommand = new RelayCommand(async () => await RegisterExitAsync(), () => CanRegisterMovement);
        RegisterAdjustmentCommand = new RelayCommand(async () => await RegisterAdjustmentAsync(), () => CanRegisterMovement);
        RegisterTransferCommand = new RelayCommand(async () => await RegisterTransferAsync(), () => CanRegisterTransfer);

        _ = LoadAsync();
    }

    public ObservableCollection<StockSummaryRow> Items { get; } = [];
    public ObservableCollection<StockLocationDTO> Locations { get; } = [];
    public string QuantityUnitCode => _products.FirstOrDefault(product => product.ProductID == ProductId)?.StockUnitCode ?? string.Empty;

    public ICommand RegisterEntryCommand { get; }
    public ICommand RegisterExitCommand { get; }
    public ICommand RegisterAdjustmentCommand { get; }
    public ICommand RegisterTransferCommand { get; }

    public string StatusMessage { get; private set; } = "Carregando estoque do catálogo de produtos...";
    public string ErrorMessage { get; private set; } = string.Empty;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get; private set; }
    public int ProductId
    {
        get => _productId;
        set
        {
            if (_productId == value) return;
            _productId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(QuantityUnitCode));
        }
    }
    public int LocationId { get; set; }
    public int SourceLocationId { get; set; }
    public int DestinationLocationId { get; set; }
    public decimal Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool CanRegisterMovement => !IsBusy && ProductId > 0 && LocationId > 0 && Quantity > 0;
    public bool CanRegisterTransfer => !IsBusy && ProductId > 0 && SourceLocationId > 0 && DestinationLocationId > 0 && SourceLocationId != DestinationLocationId && Quantity > 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var products = await _productService.GetAllProductsAsync(1, 250, cancellationToken);
            _products = products.ToArray();
            OnPropertyChanged(nameof(QuantityUnitCode));
            var locations = await _stockLocationService.GetAllStockLocationsAsync(1, 250, cancellationToken);

            Locations.Clear();
            foreach (var location in locations)
                Locations.Add(location);

            if (ProductId <= 0 && products.Any())
                ProductId = products.First().ProductID;
            if (LocationId <= 0 && locations.Any())
                LocationId = locations.First().StockLocationID;
            if (SourceLocationId <= 0 && locations.Skip(1).Any())
                SourceLocationId = locations.First().StockLocationID;
            if (DestinationLocationId <= 0 && locations.Any())
                DestinationLocationId = locations.Last().StockLocationID;

            var rows = products
                .Where(product => product.ProductID > 0)
                .OrderBy(product => product.ProductID)
                .Select(product => new StockSummaryRow(
                    product.ProductID,
                    string.IsNullOrWhiteSpace(product.ProductName) ? $"Produto {product.ProductID}" : product.ProductName,
                    "Estoque geral",
                    product.AvailableStockQuantity,
                    product.StockUnitCode ?? string.Empty,
                    product.StockStatus))
                .ToList();

            Items.Clear();
            foreach (var row in rows)
                Items.Add(row);

            StatusMessage = rows.Count == 0
                ? "Nenhum produto cadastrado para exibir no estoque."
                : $"Estoque consolidado para {rows.Count} produto(s) cadastrado(s).";
        }
        catch
        {
            Items.Clear();
            StatusMessage = "Não foi possível carregar o estoque do catálogo de produtos.";
            ErrorMessage = "Falha ao consultar os dados de estoque.";
        }
        finally
        {
            IsBusy = false;
            ((RelayCommand)RegisterEntryCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RegisterExitCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RegisterAdjustmentCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RegisterTransferCommand).RaiseCanExecuteChanged();
        }
    }

    private async Task RegisterEntryAsync()
    {
        if (!CanRegisterMovement)
            return;

        try
        {
            IsBusy = true;
            await _stockMovementService.RegisterEntryAsync(ProductId, LocationId, Quantity, 1, Notes);
            Notes = string.Empty;
            Quantity = 0m;
            ErrorMessage = string.Empty;
            StatusMessage = "Entrada registrada com sucesso.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Não foi possível registrar a entrada.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RegisterExitAsync()
    {
        if (!CanRegisterMovement)
            return;

        try
        {
            IsBusy = true;
            await _stockMovementService.RegisterExitAsync(ProductId, LocationId, Quantity, 1, Notes);
            Notes = string.Empty;
            Quantity = 0m;
            ErrorMessage = string.Empty;
            StatusMessage = "Saída registrada com sucesso.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Não foi possível registrar a saída.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RegisterAdjustmentAsync()
    {
        if (!CanRegisterMovement)
            return;

        try
        {
            IsBusy = true;
            await _stockMovementService.RegisterAdjustmentAsync(
                ProductId,
                LocationId,
                Quantity,
                StockAdjustmentReason.Outro,
                string.IsNullOrWhiteSpace(Notes) ? "Ajuste manual da tela de estoque" : Notes,
                1,
                1,
                Notes);
            Notes = string.Empty;
            Quantity = 0m;
            ErrorMessage = string.Empty;
            StatusMessage = "Ajuste registrado com sucesso.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Não foi possível registrar o ajuste.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RegisterTransferAsync()
    {
        if (!CanRegisterTransfer)
            return;

        try
        {
            IsBusy = true;
            await _stockMovementService.RegisterTransferAsync(ProductId, SourceLocationId, DestinationLocationId, Quantity, 1, Notes);
            Notes = string.Empty;
            Quantity = 0m;
            ErrorMessage = string.Empty;
            StatusMessage = "Transferência registrada com sucesso.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Não foi possível registrar a transferência.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private sealed class EmptyStockMovementService : IStockMovementService
    {
        public Task<int> RegisterEntryAsync(int productId, int stockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RegisterExitAsync(int productId, int stockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RegisterAdjustmentAsync(int productId, int stockLocationId, decimal quantityDelta, StockAdjustmentReason reason, string justification, int authorizedByEmployeeId, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Guid> RegisterTransferAsync(int productId, int sourceStockLocationId, int destinationStockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, int? orderItemId = null, CancellationToken cancellationToken = default) => Task.FromResult(Guid.Empty);
        public Task ReviewTransferAsync(Guid transferId, StockMovementReviewStatus reviewStatus, int reviewedByEmployeeId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<StockMovementDTO> GetByIdAsync(int stockMovementId, CancellationToken cancellationToken = default)
            => Task.FromResult(new StockMovementDTO(0, 0, 0, 0m, StockMovementType.Entrada, null, null, null, null, 0, null, null, null, DateTime.UtcNow, null));
        public Task<IEnumerable<StockMovementDTO>> GetByProductAsync(int productId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<StockMovementDTO>>([]);
        public Task<IEnumerable<StockMovementDTO>> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<StockMovementDTO>>([]);
    }

    private sealed class EmptyStockLocationService : IStockLocationService
    {
        public Task<StockLocationDTO> GetStockLocationByIdAsync(int stockLocationId, CancellationToken cancellationToken = default)
            => Task.FromResult(new StockLocationDTO(0, "Depósito padrão", StockLocationType.Empresa, null, true));
        public Task<IEnumerable<StockLocationDTO>> GetAllStockLocationsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<StockLocationDTO>>([]);
        public Task<IEnumerable<StockLocationDTO>> SearchStockLocationsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<StockLocationDTO>>([]);
        public Task SaveStockLocationAsync(StockLocationDTO stockLocation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteStockLocationAsync(int stockLocationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record StockSummaryRow(int ProductId, string Description, string Location, decimal Quantity, string UnitCode, string Status);
