using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class PaymentMethodService(
    IPaymentMethodRepository paymentMethodRepository,
    IDTOValidator<PaymentMethodDTO> dtoValidator,
    IRepositoryValidator<PaymentMethod> repoValidator,
    IAuthorizationService authorizationService) : IPaymentMethodService
{
    private readonly IPaymentMethodRepository _paymentMethodRepository = paymentMethodRepository;
    private readonly IDTOValidator<PaymentMethodDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<PaymentMethod> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<PaymentMethodDTO>> SearchPaymentMethodsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PaymentMethodsView);
        var paymentMethods = await _paymentMethodRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return paymentMethods.ToDTO();
    }

    public async Task<IEnumerable<PaymentMethodDTO>> GetAllPaymentMethodsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PaymentMethodsView);
        var paymentMethods = await _paymentMethodRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return paymentMethods.ToDTO();
    }

    public async Task<PaymentMethodDTO> GetPaymentMethodByIdAsync(int paymentMethodId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PaymentMethodsView);
        var paymentMethod = (await _paymentMethodRepository.SearchGetByAsync(paymentMethodId.ToString(), 1, 1, cancellationToken)).FirstOrDefault(p => p.PaymentMethodID == paymentMethodId);
        return paymentMethod == null
            ? throw new KeyNotFoundException($"Forma de pagamento com ID {paymentMethodId} não encontrada.")
            : paymentMethod.ToDTO();
    }

    public async Task SavePaymentMethodAsync(PaymentMethodDTO paymentMethodDto, CancellationToken cancellationToken = default)
    {
        Require(paymentMethodDto.PaymentMethodID == 0 ? DecorPermissions.PaymentMethodsCreate : DecorPermissions.PaymentMethodsEdit);

        var dtoErrors = _dtoValidator.Validate(paymentMethodDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        var paymentMethodEntity = paymentMethodDto.FromDTO();

        var repoErrors = _repoValidator.Validate(paymentMethodEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        var affectedRows = await _paymentMethodRepository.SaveAsync(paymentMethodEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar a forma de pagamento.");
    }

    public async Task DeletePaymentMethodAsync(int paymentMethodId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PaymentMethodsDelete);

        var inUse = await _paymentMethodRepository.IsInUseAsync(paymentMethodId, cancellationToken);
        if (inUse)
            throw new ValidationException("Não é possível excluir a forma de pagamento pois ela está em uso por parcelas de pedidos.");

        var affectedRows = await _paymentMethodRepository.DeleteAsync(paymentMethodId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("A forma de pagamento não foi encontrada ou não pôde ser excluída.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
