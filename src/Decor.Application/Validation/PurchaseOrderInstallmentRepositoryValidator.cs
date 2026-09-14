using Decor.Core.Entities;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class PurchaseOrderInstallmentRepositoryValidator : IRepositoryValidator<PurchaseOrderInstallment>
{
    public IEnumerable<string> Validate(PurchaseOrderInstallment entity) => [];
}