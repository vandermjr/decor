using System.Collections.ObjectModel;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class PurchaseOrdersViewModel
{
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly IGoodsReceiptService _goodsReceiptService;

    public PurchaseOrdersViewModel(IPurchaseOrderService purchaseOrderService, IGoodsReceiptService goodsReceiptService)
    {
        _purchaseOrderService = purchaseOrderService ?? throw new ArgumentNullException(nameof(purchaseOrderService));
        _goodsReceiptService = goodsReceiptService ?? throw new ArgumentNullException(nameof(goodsReceiptService));

        NewCommand = new RelayCommand(() => BeginNew(), () => !IsBusy);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        DeleteCommand = new RelayCommand(async () => await DeleteAsync(), () => CanDelete);
        RegisterReceiptCommand = new RelayCommand(async () => await RegisterReceiptAsync(), () => CanRegisterReceipt);

        _ = LoadAsync();
    }

    public ObservableCollection<PurchaseOrderSummaryRow> Items { get; } = [];

    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RegisterReceiptCommand { get; }

    public string StatusMessage { get; private set; } = "Carregando pedidos de compra...";
    public string ErrorMessage { get; private set; } = string.Empty;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get; private set; }
    public int PurchaseOrderId { get; set; }
    public int SupplierId { get; set; }
    public DateTimeOffset? OrderDate { get; set; } = DateTimeOffset.Now.Date;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Aberto;
    public string StatusText { get; set; } = "Aberto";
    public string Notes { get; set; } = string.Empty;
    public int PurchaseOrderItemId { get; set; }
    public decimal QuantityReceived { get; set; }
    public int ReceivedByEmployeeId { get; set; } = 1;
    public bool HasDivergence { get; set; }
    public string DivergenceNotes { get; set; } = string.Empty;
    public bool CanSave => !IsBusy && SupplierId > 0;
    public bool CanDelete => !IsBusy && PurchaseOrderId > 0;
    public bool CanRegisterReceipt => !IsBusy && PurchaseOrderItemId > 0 && QuantityReceived > 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            var orders = await _purchaseOrderService.GetAllPurchaseOrdersAsync(1, 100, cancellationToken);
            var rows = orders
                .OrderByDescending(order => order.OrderDate)
                .Select(order => new PurchaseOrderSummaryRow(
                    order.PurchaseOrderID,
                    order.SupplierID,
                    order.OrderDate,
                    order.Status.ToString(),
                    string.IsNullOrWhiteSpace(order.Notes) ? "Sem observações" : order.Notes))
                .ToList();

            Items.Clear();
            foreach (var row in rows)
                Items.Add(row);

            StatusMessage = rows.Count == 0
                ? "Nenhum pedido de compra cadastrado."
                : $"{rows.Count} pedido(s) de compra encontrado(s).";
        }
        catch
        {
            Items.Clear();
            StatusMessage = "Não foi possível carregar os pedidos de compra.";
            ErrorMessage = "Falha ao consultar os pedidos de compra.";
        }
        finally
        {
            IsBusy = false;
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RegisterReceiptCommand).RaiseCanExecuteChanged();
        }
    }

    private void BeginNew()
    {
        PurchaseOrderId = 0;
        SupplierId = 0;
        OrderDate = DateTimeOffset.Now.Date;
        Status = PurchaseOrderStatus.Aberto;
        StatusText = "Aberto";
        Notes = string.Empty;
        ErrorMessage = string.Empty;
        StatusMessage = "Preencha os dados do novo pedido de compra.";
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
            return;

        try
        {
            IsBusy = true;
            Status = ParseStatus(StatusText);
            var orderDateValue = OrderDate?.DateTime ?? DateTime.Today;
            var dto = new PurchaseOrderDTO(PurchaseOrderId, SupplierId, orderDateValue, Status, Notes);
            await _purchaseOrderService.SavePurchaseOrderAsync(dto);
            PurchaseOrderId = dto.PurchaseOrderID;
            ErrorMessage = string.Empty;
            StatusMessage = "Pedido de compra salvo com sucesso.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Não foi possível salvar o pedido de compra.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (!CanDelete)
            return;

        try
        {
            IsBusy = true;
            await _purchaseOrderService.DeletePurchaseOrderAsync(PurchaseOrderId);
            PurchaseOrderId = 0;
            SupplierId = 0;
            Notes = string.Empty;
            ErrorMessage = string.Empty;
            StatusMessage = "Pedido removido com sucesso.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Não foi possível excluir o pedido.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RegisterReceiptAsync()
    {
        if (!CanRegisterReceipt)
            return;

        try
        {
            IsBusy = true;
            await _goodsReceiptService.RegisterReceiptAsync(
                PurchaseOrderItemId,
                QuantityReceived,
                ReceivedByEmployeeId,
                HasDivergence,
                string.IsNullOrWhiteSpace(DivergenceNotes) ? null : DivergenceNotes);

            QuantityReceived = 0m;
            PurchaseOrderItemId = 0;
            DivergenceNotes = string.Empty;
            HasDivergence = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Recebimento registrado com sucesso.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Não foi possível registrar o recebimento.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static PurchaseOrderStatus ParseStatus(string value)
    {
        return value switch
        {
            "Aberto" => PurchaseOrderStatus.Aberto,
            "ParcialmenteRecebido" => PurchaseOrderStatus.ParcialmenteRecebido,
            "Recebido" => PurchaseOrderStatus.Recebido,
            "Cancelado" => PurchaseOrderStatus.Cancelado,
            _ => PurchaseOrderStatus.Aberto,
        };
    }
}

public sealed record PurchaseOrderSummaryRow(int PurchaseOrderID, int SupplierID, DateTime OrderDate, string Status, string Notes);
