using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class ProductService(
    IProductRepository productRepository,
    IDTOValidator<ProductDTO> dtoValidator,
    IRepositoryValidator<Product> repoValidator,
    IAuthorizationService authorizationService) : IProductService
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IDTOValidator<ProductDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Product> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<ProductDTO>> SearchProductsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return products.ToDTO();
    }

    public async Task<ProductDTO> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        // A busca no repositório já faz os JOINs necessários
        var product = (await _productRepository.SearchGetByAsync(productId.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        // Lançar exceção ou retornar nulo? Depende da regra de negócio.
        // Aqui, vamos retornar o DTO ou lançar uma exceção se não for encontrado.
        return product == null 
            ? throw new KeyNotFoundException($"Produto com ID {productId} não encontrado.") 
            : product.ToDTO();
    }

    // MÉTODO NOVO
    public async Task<IEnumerable<ProductDTO>> GetAllProductsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // NOTA: O método SearchGetBy no seu repositório atual retorna uma lista vazia se o argumento for nulo ou vazio.
        // Para que GetAll funcione, o ideal seria criar um método GetAll() no repositório que não tenha o filtro WHERE.
        // Por enquanto, podemos usar um termo de busca que provavelmente retornará muitos itens, como um único caractere.
        // A solução ideal é ajustar o repositório.
        var products = await _productRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return products.ToDTO();
    }

    public async Task SaveProductAsync(ProductDTO productDto, CancellationToken cancellationToken = default)
    {
        Require(productDto.ProductID == 0 ? DecorPermissions.ProductsCreate : DecorPermissions.ProductsEdit);
        var dtoErrors = _dtoValidator.Validate(productDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        var productEntity = productDto.FromDTO();

        var repoErrors = _repoValidator.Validate(productEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        var affectedRows = await _productRepository.SaveAsync(productEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o produto.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}