using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;
public class StockLocationRepositoryValidator(IStockLocationRepository Repository) : IRepositoryValidator<StockLocation>
{
    private readonly IStockLocationRepository _repository = Repository;

    public IEnumerable<string> Validate(StockLocation stockLocation)
    {
        var errors = new List<string>();

        // D4: depósito de Parceiro exige PartnerID; depósito da Empresa não pode ter PartnerID
        if (stockLocation.LocationType == StockLocationType.Parceiro && stockLocation.PartnerID == null)
            errors.Add("Um depósito do tipo Parceiro deve informar o Parceiro associado.");

        if (stockLocation.LocationType == StockLocationType.Empresa && stockLocation.PartnerID != null)
            errors.Add("Um depósito do tipo Empresa não pode ter um Parceiro associado.");

        return errors;
    }
}
