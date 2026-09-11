using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class PurchaseOrderItemRepositoryValidator(IPurchaseOrderItemRepository repository) : IRepositoryValidator<PurchaseOrderItem>
{
    private readonly IPurchaseOrderItemRepository _repository = repository;

    public IEnumerable<string> Validate(PurchaseOrderItem purchaseOrderItem)
    {
        var errors = new List<string>();

        if (purchaseOrderItem.FinalDestination == PurchaseOrderFinalDestination.DepositoEmpresa && purchaseOrderItem.StockLocationID == null)
            errors.Add("Um item destinado ao depósito da empresa deve informar o depósito.");

        if (purchaseOrderItem.FinalDestination == PurchaseOrderFinalDestination.DiretoCliente && purchaseOrderItem.CustomerID == null)
            errors.Add("Um item destinado diretamente ao cliente deve informar o cliente.");

        return errors;
    }
}