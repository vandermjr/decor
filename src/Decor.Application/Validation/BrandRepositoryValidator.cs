using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;
public class BrandRepositoryValidator(IBrandRepository Repository) : IRepositoryValidator<Brand>
{
    private readonly IBrandRepository _repository = Repository;

    public IEnumerable<string> Validate(Brand brand)
    {
        var errors = new List<string>();

        // Por enquanto, não há validações de repositório

        return errors;
    }
}
