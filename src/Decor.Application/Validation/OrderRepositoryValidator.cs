using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class OrderRepositoryValidator(IOrderRepository repository, IServiceRepository? serviceRepository = null) : IRepositoryValidator<Order>
{
    private readonly IOrderRepository _repository = repository;

    public IEnumerable<string> Validate(Order entity)
    {
        var errors = new List<string>();

        foreach (var item in entity.Items)
        {
            if (!CommercialItemReference.IsValid(item.ProductID, item.ServiceID))
                errors.Add(CommercialItemReference.ValidationMessage);
            else if (item.ServiceID is int serviceId && (serviceRepository is null || !serviceRepository.ServiceExists(serviceId)))
                errors.Add("O servico informado nao existe.");
        }

        if (entity.CustomerID <= 0)
            errors.Add("O cliente do pedido é obrigatório.");

        if (entity.QuoteSectionID <= 0)
            errors.Add("A seção de orçamento do pedido é obrigatória.");

        return errors;
    }
}
