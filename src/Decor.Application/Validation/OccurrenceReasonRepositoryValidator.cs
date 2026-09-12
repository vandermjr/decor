using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class OccurrenceReasonRepositoryValidator(IOccurrenceReasonRepository repository) : IRepositoryValidator<OccurrenceReason>
{
    public IEnumerable<string> Validate(OccurrenceReason entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Description) && repository.DescriptionExists(entity.Description, entity.ReasonID))
            return [$"Já existe um motivo de ocorrência com a descrição '{entity.Description}'."];

        return [];
    }
}