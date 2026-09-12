using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class StockReservationRepositoryValidator(IStockReservationRepository repository) : IRepositoryValidator<StockReservation>
{
    private readonly IStockReservationRepository _repository = repository;

    public IEnumerable<string> Validate(StockReservation reservation)
    {
        var errors = new List<string>();
        return errors;
    }
}
