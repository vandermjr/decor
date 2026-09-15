using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class AccountsPayableService(
    IAccountsPayableRepository repository,
    IEmployeeRepository employeeRepository,
    IPartnerRepository partnerRepository,
    ITailorQuotationRepository tailorQuotationRepository,
    IServiceExecutionRecordRepository serviceExecutionRecordRepository,
    IDTOValidator<AccountsPayableDTO> dtoValidator,
    IRepositoryValidator<AccountsPayable> repositoryValidator,
    IAuthorizationService authorizationService) : IAccountsPayableService
{
    public async Task<AccountsPayableDTO> CreateAsync(AccountsPayableDTO dto, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.AccountsPayableCreate);
        if (dto.Status != AccountsPayableStatus.Pending)
            throw new ValidationException("Uma conta a pagar deve ser criada com status Pendente.");

        var entity = dto.FromDTO();
        entity.AccountsPayableID = 0;
        entity.Description = dto.Description.Trim();
        entity.CreatedAt = dto.CreatedAt == default ? DateTime.UtcNow : dto.CreatedAt;
        entity.PaidAt = null;
        entity.PaidByEmployeeID = null;
        var errors = dtoValidator.Validate(entity.ToDTO());
        if (errors.Any()) throw new ValidationException(string.Join("\n", errors));
        var repositoryErrors = repositoryValidator.Validate(entity);
        if (repositoryErrors.Any()) throw new ValidationException(string.Join("\n", repositoryErrors));

        await ValidatePayeeAsync(entity, cancellationToken);
        await ValidateSourceAsync(entity, cancellationToken);
        if (await repository.SaveAsync(entity, cancellationToken) != 1)
            throw new InvalidOperationException("Não foi possível criar a conta a pagar.");
        return entity.ToDTO();
    }

    public async Task<AccountsPayableDTO> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.AccountsPayableView);
        var entity = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Conta a pagar com ID {id} não encontrada.");
        return entity.ToDTO();
    }

    public Task RegisterPaymentAsync(int id, int paidByEmployeeId, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(id, AccountsPayableStatus.Paid, DecorPermissions.AccountsPayableRegisterPayment, paidByEmployeeId, cancellationToken);

    public Task CancelAsync(int id, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(id, AccountsPayableStatus.Cancelled, DecorPermissions.AccountsPayableCancel, null, cancellationToken);

    private async Task ChangeStatusAsync(int id, AccountsPayableStatus status, string permission, int? paidByEmployeeId, CancellationToken cancellationToken)
    {
        Require(permission);
        var entity = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Conta a pagar com ID {id} não encontrada.");
        if (entity.Status != AccountsPayableStatus.Pending)
            throw new ValidationException("Apenas contas a pagar pendentes podem sofrer esta operação.");
        entity.Status = status;
        if (status == AccountsPayableStatus.Paid)
        {
            entity.PaidAt = DateTime.UtcNow;
            entity.PaidByEmployeeID = paidByEmployeeId;
        }
        await repository.SaveAsync(entity, cancellationToken);
    }

    private async Task ValidatePayeeAsync(AccountsPayable entity, CancellationToken cancellationToken)
    {
        var exists = entity.PayeeType switch
        {
            AccountsPayablePayeeType.Partner => (await partnerRepository.SearchGetByAsync(entity.PayeeID.ToString(), 1, 100, cancellationToken)).Any(p => p.PartnerID == entity.PayeeID),
            AccountsPayablePayeeType.Employee => (await employeeRepository.SearchGetByAsync(entity.PayeeID.ToString(), 1, 100, cancellationToken)).Any(e => e.EmployeeID == entity.PayeeID),
            _ => false
        };
        if (!exists) throw new ValidationException("O favorecido informado não foi encontrado para o tipo selecionado.");
    }

    private async Task ValidateSourceAsync(AccountsPayable entity, CancellationToken cancellationToken)
    {
        if (!entity.SourceType.HasValue) return;
        var exists = entity.SourceType.Value switch
        {
            AccountsPayableSourceType.TailorQuotationRevision => await tailorQuotationRepository.GetRevisionByIdAsync(entity.SourceID!.Value, cancellationToken) is not null,
            AccountsPayableSourceType.ServiceExecutionRecord => await serviceExecutionRecordRepository.GetByIdAsync(entity.SourceID!.Value, cancellationToken) is not null,
            _ => false
        };
        if (!exists) throw new ValidationException("A referência de origem informada não foi encontrada.");
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}