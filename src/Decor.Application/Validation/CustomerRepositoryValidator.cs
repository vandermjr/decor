using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;
public class CustomerRepositoryValidator(ICustomerRepository Repository) : IRepositoryValidator<Customer>
{
    private readonly ICustomerRepository _repository = Repository;

    public IEnumerable<string> Validate(Customer customer)
    {
        var errors = new List<string>();

        // Por enquanto, não há validações de repositório

        return errors;
    }
}
