using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class StockAdjustmentsViewModel : INotifyPropertyChanged
{
    private readonly IProductService _productService;
    private readonly IStockMovementService _stockMovementService;
    private readonly IStockLocationService _stockLocationService;
    private IReadOnlyList<ProductDTO> _products = [];
    private int _productId;

    public StockAdjustmentsViewModel(IProductService productService, IStockMovementService stockMovementService, IStockLocationService stockLocationService)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _stockMovementService = stockMovementService ?? throw new ArgumentNullException(nameof(stockMovementService));
        _stockLocationService = stockLocationService ?? throw new ArgumentNullException(nameof(stockLocationService));

        RegisterAdjustmentCommand = new RelayCommand(async () => await RegisterAdjustmentAsync(), () => CanRegisterAdjustment);
        _ = LoadAsync();
    }

    public ObservableCollection<StockAdjustmentRow> Items { get; } = [];
    public ObservableCollection<StockLocationDTO> Locations { get; } = [];
    public string QuantityUnitCode => _products.FirstOrDefault(product => product.ProductID == ProductId)?.StockUnitCode ?? string.Empty;

    public ICommand RegisterAdjustmentCommand { get; }
    public string StatusMessage { get; private set; } = "Carregando ajustes do catálogo...";
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
    public decimal QuantityDelta { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string Reason { get; set; } = "Outro";
    public bool CanRegisterAdjustment => !IsBusy && ProductId > 0 && LocationId > 0 && QuantityDelta != 0m;

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

            var rows = new List<StockAdjustmentRow>();
            foreach (var product in products.OrderBy(p => p.ProductID))
            {
                var movements = await _stockMovementService.GetByProductAsync(product.ProductID, cancellationToken);
                foreach (var movement in movements.Where(m => m.MovementType == StockMovementType.Ajuste))
                {
                    rows.Add(new StockAdjustmentRow(
                        $"AJ-{movement.StockMovementID:0000}",
                        string.IsNullOrWhiteSpace(product.ProductName) ? $"Produto {product.ProductID}" : product.ProductName,
                        movement.StockLocationID.ToString(),
                        movement.Quantity,
                        product.StockUnitCode ?? string.Empty,
                        movement.Reason?.ToString() ?? "Outro",
                        movement.Justification ?? "Ajuste manual"));
                }
            }

            Items.Clear();
            foreach (var row in rows.OrderByDescending(r => r.AdjustmentCode))
                Items.Add(row);

            StatusMessage = rows.Count == 0
                ? "Nenhum ajuste vinculado a produtos cadastrados."
                : $"{rows.Count} ajuste(s) encontrado(s).";
        }
        catch
        {
            Items.Clear();
            StatusMessage = "Não foi possível carregar ajustes.";
            ErrorMessage = "Falha ao consultar movimentações de ajuste.";
        }
        finally
        {
            IsBusy = false;
            ((RelayCommand)RegisterAdjustmentCommand).RaiseCanExecuteChanged();
        }
    }

    private async Task RegisterAdjustmentAsync()
    {
        if (!CanRegisterAdjustment)
            return;

        try
        {
            IsBusy = true;
            var parsedReason = Enum.TryParse<StockAdjustmentReason>(Reason, out var selectedReason)
                ? selectedReason
                : StockAdjustmentReason.Outro;

            await _stockMovementService.RegisterAdjustmentAsync(
                ProductId,
                LocationId,
                QuantityDelta,
                parsedReason,
                string.IsNullOrWhiteSpace(Justification) ? "Ajuste manual da tela de estoque" : Justification,
                1,
                1,
                Justification);

            QuantityDelta = 0m;
            Justification = string.Empty;
            Reason = "Outro";
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record StockAdjustmentRow(string AdjustmentCode, string Product, string Location, decimal QuantityDelta, string UnitCode, string Reason, string Justification);
