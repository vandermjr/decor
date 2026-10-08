using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class ServiceRepositoryValidator(IServiceRepository repository) : IRepositoryValidator<Service>
{
    public IEnumerable<string> Validate(Service service)
    {
        if (service.ServiceID > 0 && !repository.ServiceExists(service.ServiceID))
            return ["O servico informado nao existe."];
        return [];
    }
}