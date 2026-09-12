using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class OrderInstallmentRepositoryValidator(IOrderInstallmentRepository repository) : IRepositoryValidator<OrderInstallment>
{
    private readonly IOrderInstallmentRepository _repository = repository;

    public IEnumerable<string> Validate(OrderInstallment entity)
    {
        var errors = new List<string>();
        return errors;
    }
}
