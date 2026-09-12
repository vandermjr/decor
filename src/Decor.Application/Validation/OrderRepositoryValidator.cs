using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class OrderRepositoryValidator(IOrderRepository repository) : IRepositoryValidator<Order>
{
    private readonly IOrderRepository _repository = repository;

    public IEnumerable<string> Validate(Order entity)
    {
        var errors = new List<string>();

        if (entity.CustomerID <= 0)
            errors.Add("O cliente do pedido é obrigatório.");

        if (entity.QuoteSectionID <= 0)
            errors.Add("A seção de orçamento do pedido é obrigatória.");

        return errors;
    }
}
