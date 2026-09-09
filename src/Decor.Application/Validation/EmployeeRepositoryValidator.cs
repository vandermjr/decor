using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;
public class EmployeeRepositoryValidator(IEmployeeRepository Repository) : IRepositoryValidator<Employee>
{
    private readonly IEmployeeRepository _repository = Repository;

    public IEnumerable<string> Validate(Employee employee)
    {
        var errors = new List<string>();

        // Por enquanto, não há validações de repositório

        return errors;
    }
}
