using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class PaymentMethodRepositoryValidator(IPaymentMethodRepository repository) : IRepositoryValidator<PaymentMethod>
{
    private readonly IPaymentMethodRepository _repository = repository;

    public IEnumerable<string> Validate(PaymentMethod entity)
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(entity.Name) && _repository.NameExists(entity.Name, entity.PaymentMethodID))
        {
            errors.Add($"Já existe uma forma de pagamento com o nome '{entity.Name}'.");
        }

        return errors;
    }
}
