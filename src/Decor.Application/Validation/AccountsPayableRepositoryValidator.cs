using Decor.Core.Entities;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class AccountsPayableRepositoryValidator : IRepositoryValidator<AccountsPayable>
{
    public IEnumerable<string> Validate(AccountsPayable entity) => [];
}