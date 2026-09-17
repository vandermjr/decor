using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class UnitOfMeasureRepositoryValidator(IUnitOfMeasureRepository repository) : IRepositoryValidator<UnitOfMeasure>
{
    private readonly IUnitOfMeasureRepository _repository = repository;

    public IEnumerable<string> Validate(UnitOfMeasure entity)
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(entity.Code) && _repository.CodeExists(entity.Code, entity.UnitOfMeasureID))
            errors.Add($"Já existe uma unidade de medida com o código '{entity.Code}'.");

        return errors;
    }
}
