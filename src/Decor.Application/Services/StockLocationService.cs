using System.ComponentModel.DataAnnotations;
using Decor.Core.Common;
using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class StockLocationService(
        IStockLocationRepository stockLocationRepository,
        IDTOValidator<StockLocationDTO> dtoValidator,
        IRepositoryValidator<StockLocation> repoValidator,
        IAuthorizationService authorizationService) : IStockLocationService
{
    private readonly IStockLocationRepository _stockLocationRepository = stockLocationRepository;
    private readonly IDTOValidator<StockLocationDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<StockLocation> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<StockLocationDTO>> SearchStockLocationsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Delega a busca para o repositório e mapeia o resultado para DTO
        var stockLocations = await _stockLocationRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return stockLocations.ToDTO();
    }

    public async Task<IEnumerable<StockLocationDTO>> GetAllStockLocationsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Para obter todos, chamamos a busca com um argumento nulo
        var stockLocations = await _stockLocationRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return stockLocations.ToDTO();
    }

    public async Task<StockLocationDTO> GetStockLocationByIdAsync(int stockLocationID, CancellationToken cancellationToken = default)
    {
        var stockLocation = (await _stockLocationRepository.SearchGetByAsync(stockLocationID.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return stockLocation == null
            ? throw new KeyNotFoundException($"Depósito com ID {stockLocationID} não encontrado.")
            : stockLocation.ToDTO();
    }

    public async Task SaveStockLocationAsync(StockLocationDTO stockLocationDto, CancellationToken cancellationToken = default)
    {
        Require(stockLocationDto.StockLocationID == 0 ? DecorPermissions.StockLocationsCreate : DecorPermissions.StockLocationsEdit);
        // 1. Validação do DTO (regras de negócio que não dependem do banco)
        var dtoErrors = _dtoValidator.Validate(stockLocationDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        // 2. Mapeamento de DTO para Entidade de Domínio
        var stockLocationEntity = stockLocationDto.FromDTO();

        // 3. Validação de Repositório (regras que dependem do banco, ex: duplicidade)
        var repoErrors = _repoValidator.Validate(stockLocationEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        // 4. Persistência
        var affectedRows = await _stockLocationRepository.SaveAsync(stockLocationEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o depósito.");
    }

    public async Task DeleteStockLocationAsync(int stockLocationId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockLocationsDelete);
        var affectedRows = await _stockLocationRepository.DeleteAsync(stockLocationId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O depósito não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
