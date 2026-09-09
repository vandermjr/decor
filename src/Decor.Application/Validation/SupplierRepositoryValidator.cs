using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;
public class SupplierRepositoryValidator(ISupplierRepository Repository) : IRepositoryValidator<Supplier>
{
    private readonly ISupplierRepository _repository = Repository;

    public IEnumerable<string> Validate(Supplier supplier)
    {
        var errors = new List<string>();

        // Por enquanto, não há validações de repositório

        return errors;
    }
}
