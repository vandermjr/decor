using Decor.Core.Entities;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class OrderOccurrenceRepositoryValidator : IRepositoryValidator<OrderOccurrence>
{
    public IEnumerable<string> Validate(OrderOccurrence entity) => [];
}