using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class ServiceExecutionRecordRepositoryValidator : IRepositoryValidator<ServiceExecutionRecord>
{
    public IEnumerable<string> Validate(ServiceExecutionRecord entity) => [];
}
