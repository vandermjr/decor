using System.ComponentModel.DataAnnotations;
using Decor.Core.Common;
using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class BrandService(
        IBrandRepository brandRepository,
        IDTOValidator<BrandDTO> dtoValidator,
        IRepositoryValidator<Brand> repoValidator,
        IAuthorizationService authorizationService) : IBrandService
{
    private readonly IBrandRepository _brandRepository = brandRepository;
    private readonly IDTOValidator<BrandDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Brand> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<BrandDTO>> SearchBrandsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Delega a busca para o repositório e mapeia o resultado para DTO
        var brands = await _brandRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return brands.ToDTO();
    }

    public async Task<IEnumerable<BrandDTO>> GetAllBrandsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Para obter todos, chamamos a busca com um argumento nulo
        var brands = await _brandRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return brands.ToDTO();
    }

    public async Task<BrandDTO> GetBrandByIdAsync(int brandID, CancellationToken cancellationToken = default)
    {
        var brand = (await _brandRepository.SearchGetByAsync(brandID.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        // Lançar exceção ou retornar nulo? Depende da regra de negócio.
        // Aqui, vamos retornar o DTO ou lançar uma exceção se não for encontrado.
        return brand == null 
            ? throw new KeyNotFoundException($"Marca com ID {brandID} não encontrada.") 
            : brand.ToDTO();
    }

    public async Task SaveBrandAsync(BrandDTO brandDto, CancellationToken cancellationToken = default)
    {
        Require(brandDto.BrandID == 0 ? DecorPermissions.BrandsCreate : DecorPermissions.BrandsEdit);
        // 1. Validação do DTO (regras de negócio que não dependem do banco)
        var dtoErrors = _dtoValidator.Validate(brandDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        // 2. Mapeamento de DTO para Entidade de Domínio
        var brandEntity = brandDto.FromDTO();

        // 3. Validação de Repositório (regras que dependem do banco, ex: duplicidade)
        var repoErrors = _repoValidator.Validate(brandEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        // 4. Persistência
        var affectedRows = await _brandRepository.SaveAsync(brandEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar a marca.");
    }

    public async Task DeleteBrandAsync(int brandId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.BrandsDelete);
        var affectedRows = await _brandRepository.DeleteAsync(brandId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("A marca não foi encontrada ou não pôde ser excluída.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}