using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class QuoteRepositoryValidator(IQuoteRepository repository) : IRepositoryValidator<Quote>
{
    private readonly IQuoteRepository _repository = repository;

    public IEnumerable<string> Validate(Quote entity)
    {
        var errors = new List<string>();

        if (entity.CustomerID <= 0)
            errors.Add("O cliente do orçamento é obrigatório.");

        if (entity.CreatedByEmployeeID <= 0)
            errors.Add("O funcionário responsável pela criação do orçamento é obrigatório.");

        return errors;
    }
}
