using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;
public class PartnerRepositoryValidator(IPartnerRepository Repository) : IRepositoryValidator<Partner>
{
    private readonly IPartnerRepository _repository = Repository;

    public IEnumerable<string> Validate(Partner partner)
    {
        var errors = new List<string>();

        // Por enquanto, não há validações de repositório

        return errors;
    }
}
