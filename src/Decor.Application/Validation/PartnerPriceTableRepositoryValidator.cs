using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class PartnerPriceTableRepositoryValidator(IPartnerPriceTableRepository repository) : IRepositoryValidator<PartnerPriceTable>
{
    private readonly IPartnerPriceTableRepository _repository = repository;

    public IEnumerable<string> Validate(PartnerPriceTable table)
    {
        var errors = new List<string>();

        if (table.IsActive && _repository.ActiveEntryExists(table.PartnerID, table.GroupID, table.PriceTableID))
        {
            errors.Add("Já existe uma tabela de preço ativa para este parceiro e grupo.");
        }

        return errors;
    }
}
