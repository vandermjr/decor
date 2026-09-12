using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class TailorQuotationRepositoryValidator(ITailorQuotationRepository repository) : IRepositoryValidator<TailorQuotationRequest>
{
    private readonly ITailorQuotationRepository _repository = repository;

    public IEnumerable<string> Validate(TailorQuotationRequest entity)
    {
        var errors = new List<string>();
        return errors;
    }
}
