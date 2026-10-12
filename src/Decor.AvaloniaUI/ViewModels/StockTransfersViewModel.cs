using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class StockTransfersViewModel : INotifyPropertyChanged
{
    private readonly IProductService _productService;
    private readonly IStockMovementService _stockMovementService;
    private readonly IStockLocationService _stockLocationService;
    private IReadOnlyList<ProductDTO> _products = [];
    private int _productId;

    public StockTransfersViewModel(IProductService productService, IStockMovementService stockMovementService, IStockLocationService stockLocationService)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _stockMovementService = stockMovementService ?? throw new ArgumentNullException(nameof(stockMovementService));
        _stockLocationService = stockLocationService ?? throw new ArgumentNullException(nameof(stockLocationService));

        RegisterTransferCommand = new RelayCommand(async () => await RegisterTransferAsync(), () => CanRegisterTransfer);
        _ = LoadAsync();
    }

    public ObservableCollection<StockTransferRow> Items { get; } = [];
    public ObservableCollection<StockLocationDTO> Locations { get; } = [];
    public string QuantityUnitCode => _products.FirstOrDefault(product => product.ProductID == ProductId)?.StockUnitCode ?? string.Empty;

    public ICommand RegisterTransferCommand { get; }
    public string StatusMessage { get; private set; } = "Carregando transferências do catálogo...";
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
    public int SourceLocationId { get; set; }
    public int DestinationLocationId { get; set; }
    public decimal Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
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
            if (SourceLocationId <= 0 && locations.Any())
                SourceLocationId = locations.First().StockLocationID;
            if (DestinationLocationId <= 0 && locations.Count() > 1)
                DestinationLocationId = locations.Skip(1).First().StockLocationID;

            var rows = new List<StockTransferRow>();
            foreach (var product in products.OrderBy(p => p.ProductID))
            {
                var movements = await _stockMovementService.GetByProductAsync(product.ProductID, cancellationToken);
                foreach (var movement in movements.Where(m => m.MovementType == StockMovementType.Transferencia))
                {
                    var route = movement.StockLocationID == SourceLocationId ? $"Local {movement.StockLocationID} → {DestinationLocationId}" : $"Local {SourceLocationId} → {movement.StockLocationID}";
                    rows.Add(new StockTransferRow(
                        movement.TransferID?.ToString() ?? $"TR-{movement.StockMovementID:0000}",
                        route,
                        string.IsNullOrWhiteSpace(product.Description) ? $"Produto {product.ProductID}" : product.Description,
                        movement.Quantity,
                        product.StockUnitCode ?? string.Empty,
                        movement.ReviewStatus?.ToString() ?? "Pendente"));
                }
            }

            Items.Clear();
            foreach (var row in rows.OrderByDescending(r => r.TransferCode))
                Items.Add(row);

            StatusMessage = rows.Count == 0
                ? "Nenhuma transferência vinculada a produtos cadastrados."
                : $"{rows.Count} transferência(ões) encontradas.";
        }
        catch
        {
            Items.Clear();
            StatusMessage = "Não foi possível carregar transferências.";
            ErrorMessage = "Falha ao consultar movimentações de transferência.";
        }
        finally
        {
            IsBusy = false;
            ((RelayCommand)RegisterTransferCommand).RaiseCanExecuteChanged();
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
            Quantity = 0m;
            Notes = string.Empty;
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record StockTransferRow(string TransferCode, string Route, string Product, decimal Quantity, string UnitCode, string Status);
