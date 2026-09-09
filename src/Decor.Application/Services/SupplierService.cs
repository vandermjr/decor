using System.ComponentModel.DataAnnotations;
using Decor.Core.Common;
using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class SupplierService(
        ISupplierRepository supplierRepository,
        IDTOValidator<SupplierDTO> dtoValidator,
        IRepositoryValidator<Supplier> repoValidator,
        IAuthorizationService authorizationService) : ISupplierService
{
    private readonly ISupplierRepository _supplierRepository = supplierRepository;
    private readonly IDTOValidator<SupplierDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Supplier> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<SupplierDTO>> SearchSuppliersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Delega a busca para o repositório e mapeia o resultado para DTO
        var suppliers = await _supplierRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return suppliers.ToDTO();
    }

    public async Task<IEnumerable<SupplierDTO>> GetAllSuppliersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Para obter todos, chamamos a busca com um argumento nulo
        var suppliers = await _supplierRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return suppliers.ToDTO();
    }

    public async Task<SupplierDTO> GetSupplierByIdAsync(int supplierID, CancellationToken cancellationToken = default)
    {
        var supplier = (await _supplierRepository.SearchGetByAsync(supplierID.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return supplier == null
            ? throw new KeyNotFoundException($"Fornecedor com ID {supplierID} não encontrado.")
            : supplier.ToDTO();
    }

    public async Task SaveSupplierAsync(SupplierDTO supplierDto, CancellationToken cancellationToken = default)
    {
        Require(supplierDto.SupplierID == 0 ? DecorPermissions.SuppliersCreate : DecorPermissions.SuppliersEdit);
        // 1. Validação do DTO (regras de negócio que não dependem do banco)
        var dtoErrors = _dtoValidator.Validate(supplierDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        // 2. Mapeamento de DTO para Entidade de Domínio
        var supplierEntity = supplierDto.FromDTO();

        // 3. Validação de Repositório (regras que dependem do banco, ex: duplicidade)
        var repoErrors = _repoValidator.Validate(supplierEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        // 4. Persistência
        var affectedRows = await _supplierRepository.SaveAsync(supplierEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o fornecedor.");
    }

    public async Task DeleteSupplierAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.SuppliersDelete);
        var affectedRows = await _supplierRepository.DeleteAsync(supplierId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O fornecedor não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
