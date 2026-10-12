using System.Collections.ObjectModel;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class GoodsReceiptsViewModel
{
    private readonly IGoodsReceiptService _goodsReceiptService;

    public GoodsReceiptsViewModel(IGoodsReceiptService goodsReceiptService)
    {
        _goodsReceiptService = goodsReceiptService ?? throw new ArgumentNullException(nameof(goodsReceiptService));
        RegisterReceiptCommand = new RelayCommand(async () => await RegisterReceiptAsync(), () => CanRegisterReceipt);
        _ = LoadAsync();
    }

    public ObservableCollection<GoodsReceiptSummaryRow> Items { get; } = [];

    public ICommand RegisterReceiptCommand { get; }
    public string StatusMessage { get; private set; } = "Carregando recebimentos...";
    public string ErrorMessage { get; private set; } = string.Empty;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get; private set; }
    public int PurchaseOrderItemId { get; set; }
    public decimal QuantityReceived { get; set; }
    public int ReceivedByEmployeeId { get; set; } = 1;
    public bool HasDivergence { get; set; }
    public string DivergenceNotes { get; set; } = string.Empty;
    public bool CanRegisterReceipt => !IsBusy && PurchaseOrderItemId > 0 && QuantityReceived > 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var receipts = PurchaseOrderItemId > 0
                ? (await _goodsReceiptService.GetByPurchaseOrderItemIdAsync(PurchaseOrderItemId, cancellationToken)).ToList()
                : [];

            var rows = receipts
                .OrderByDescending(receipt => receipt.ReceiptDate)
                .Select(receipt => new GoodsReceiptSummaryRow(
                    receipt.GoodsReceiptID,
                    receipt.PurchaseOrderItemID,
                    receipt.ReceiptDate,
                    receipt.QuantityReceived,
                    receipt.ReceivedByEmployeeID,
                    receipt.Status.ToString(),
                    receipt.HasDivergence ? receipt.DivergenceNotes ?? "Divergência de recebimento" : "Sem divergência"))
                .ToList();

            Items.Clear();
            foreach (var row in rows)
                Items.Add(row);

            StatusMessage = rows.Count == 0
                ? "Nenhum recebimento registrado para o item informado."
                : $"{rows.Count} recebimento(s) registrado(s).";
        }
        catch
        {
            Items.Clear();
            StatusMessage = "Não foi possível carregar os recebimentos.";
            ErrorMessage = "Falha ao consultar os recebimentos.";
        }
        finally
        {
            IsBusy = false;
            ((RelayCommand)RegisterReceiptCommand).RaiseCanExecuteChanged();
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
            DivergenceNotes = string.Empty;
            HasDivergence = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Recebimento registrado com sucesso.";
            await LoadAsync();
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
}

public sealed record GoodsReceiptSummaryRow(int GoodsReceiptID, int PurchaseOrderItemID, DateTime ReceiptDate, decimal QuantityReceived, int ReceivedByEmployeeID, string Status, string Notes);
