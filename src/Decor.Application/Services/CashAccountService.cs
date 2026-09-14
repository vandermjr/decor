using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class CashAccountService(ICashAccountRepository repository, IAuthorizationService authorizationService) : ICashAccountService
{
    public async Task<CashAccountDTO> CreateAsync(string name, CashAccountType accountType, CancellationToken cancellationToken = default)
        => await SaveAsync(new CashAccount { Name = name, AccountType = accountType }, DecorPermissions.CashAccountsCreate, cancellationToken);

    public async Task<CashAccountDTO> UpdateAsync(int cashAccountId, string name, CashAccountType accountType, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashAccountsUpdate);
        var current = await repository.GetByIdAsync(cashAccountId, cancellationToken) ?? throw new KeyNotFoundException($"Conta de caixa {cashAccountId} não encontrada.");
        return await SaveAsync(new CashAccount { CashAccountID = cashAccountId, Name = name, AccountType = accountType, IsActive = current.IsActive }, DecorPermissions.CashAccountsUpdate, cancellationToken);
    }

    public async Task<CashAccountDTO> GetByIdAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashAccountsView);
        var account = await repository.GetByIdAsync(cashAccountId, cancellationToken) ?? throw new KeyNotFoundException($"Conta de caixa {cashAccountId} não encontrada.");
        return account.ToDTO();
    }

    public async Task<IReadOnlyList<CashAccountDTO>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashAccountsView);
        return (await repository.GetAllAsync(cancellationToken)).Select(a => a.ToDTO()).ToList();
    }

    public async Task DeactivateAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CashAccountsDeactivate);
        if (await repository.DeactivateAsync(cashAccountId, cancellationToken) != 1) throw new KeyNotFoundException($"Conta de caixa {cashAccountId} não encontrada.");
    }

    private async Task<CashAccountDTO> SaveAsync(CashAccount account, string permission, CancellationToken cancellationToken)
    {
        Require(permission);
        if (string.IsNullOrWhiteSpace(account.Name)) throw new ValidationException("O nome da conta é obrigatório.");
        if (!Enum.IsDefined(account.AccountType)) throw new ValidationException("O tipo da conta é inválido.");
        if (await repository.NameExistsAsync(account.Name, account.CashAccountID, cancellationToken)) throw new ValidationException("Já existe uma conta de caixa com esse nome.");
        if (await repository.SaveAsync(account, cancellationToken) != 1) throw new InvalidOperationException("Não foi possível salvar a conta de caixa.");
        return account.ToDTO();
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission)) throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}