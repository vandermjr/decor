using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class QuoteService(
    IQuoteRepository quoteRepository,
    IDTOValidator<QuoteDTO> dtoValidator,
    IRepositoryValidator<Quote> repoValidator,
    IAuthorizationService authorizationService,
    IProductSpecificationAttributeRepository productSpecificationAttributeRepository,
    IProductRepository productRepository) : IQuoteService
{
    private readonly IQuoteRepository _quoteRepository = quoteRepository;
    private readonly IDTOValidator<QuoteDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Quote> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly IProductSpecificationAttributeRepository _productSpecificationAttributeRepository = productSpecificationAttributeRepository;
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<IEnumerable<QuoteDTO>> SearchQuotesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => (await _quoteRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken)).Select(q => q.ToDTO());

    public async Task<IEnumerable<QuoteDTO>> GetAllQuotesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => (await _quoteRepository.SearchGetByAsync(null, page, pageSize, cancellationToken)).Select(q => q.ToDTO());

    public async Task<QuoteDTO> GetQuoteByIdAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        var quote = await _quoteRepository.GetByIdAsync(quoteId, cancellationToken);
        return quote == null ? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.") : quote.ToDTO();
    }

    public async Task SaveQuoteAsync(QuoteDTO quoteDto, CancellationToken cancellationToken = default)
    {
        Require(quoteDto.QuoteID == 0 ? DecorPermissions.QuotesCreate : DecorPermissions.QuotesEdit);
        var dtoErrors = _dtoValidator.Validate(quoteDto);
        if (dtoErrors.Any()) throw new ValidationException(string.Join("\n", dtoErrors));

        var quoteEntity = quoteDto.FromDTO();
        var repoErrors = _repoValidator.Validate(quoteEntity);
        if (repoErrors.Any()) throw new ValidationException(string.Join("\n", repoErrors));

        var affectedRows = await _quoteRepository.SaveAsync(quoteEntity, cancellationToken);
        if (affectedRows != 1) throw new InvalidOperationException("Não foi possível salvar o orçamento.");
    }

    public async Task DeleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesDelete);
        var affectedRows = await _quoteRepository.DeleteAsync(quoteId, cancellationToken);
        if (affectedRows != 1) throw new InvalidOperationException("O orçamento não foi encontrado ou não pôde ser excluído.");
    }

    public async Task UpdateSectionStatusAsync(int quoteId, int sectionId, QuoteSectionStatus newStatus, CancellationToken cancellationToken = default)
    {
        if (newStatus == QuoteSectionStatus.Sent)
            Require(DecorPermissions.QuotesSend);
        else if (newStatus == QuoteSectionStatus.Approved || newStatus == QuoteSectionStatus.Rejected)
            Require(DecorPermissions.QuotesApprove);
        else
            Require(DecorPermissions.QuotesEdit);

        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.");

        var section = quote.Sections.FirstOrDefault(s => s.QuoteSectionID == sectionId)
            ?? throw new KeyNotFoundException($"Seção com ID {sectionId} não encontrada.");

        ValidateStatusTransition(section.Status, newStatus, section);

        section.Status = newStatus;
        if (newStatus == QuoteSectionStatus.Sent)
            section.SentToCustomerAt = DateTime.UtcNow;
        if (newStatus == QuoteSectionStatus.Approved)
            section.ApprovedAt = DateTime.UtcNow;

        await _quoteRepository.SaveSectionAsync(section, cancellationToken);
    }

    public async Task AddQuoteItemAsync(int quoteId, QuoteItemDTO quoteItemDto, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesEdit);
        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.");

        var item = quoteItemDto.FromDTO();
        var section = quote.Sections.FirstOrDefault(s => s.QuoteSectionID == item.QuoteSectionID)
            ?? throw new KeyNotFoundException($"Seção com ID {item.QuoteSectionID} não encontrada.");

        if (item.UnitPrice is null)
            throw new ValidationException("O preço unitário é obrigatório para itens do orçamento.");

        await _quoteRepository.SaveItemAsync(item, cancellationToken);
    }

    public async Task AddSpecificationValueAsync(int quoteId, int quoteItemId, QuoteItemSpecificationValueDTO valueDto, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesEdit);

        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.");

        var item = quote.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.QuoteItemID == quoteItemId)
            ?? throw new KeyNotFoundException($"Item com ID {quoteItemId} não encontrado.");

        var product = await GetProductByIdAsync(item.ProductID, cancellationToken);
        var attribute = await _productSpecificationAttributeRepository.SearchGetByAsync(valueDto.AttributeID.ToString(), 1, 1, cancellationToken) is var attrs && attrs.Any() ? attrs.First() : null;
        if (attribute == null) throw new KeyNotFoundException("Atributo de especificação não encontrado.");

        var errors = ValidateQuoteItemSpecificationValue(item, attribute, product, valueDto.Value);
        if (errors.Any()) throw new ValidationException(string.Join("\n", errors));

        var entity = valueDto.FromDTO();
        await _quoteRepository.SaveSpecificationValueAsync(entity, cancellationToken);
    }

    public static IEnumerable<string> ValidateQuoteItemSpecificationValue(QuoteItem item, ProductSpecificationAttribute attribute, Product product, string value)
    {
        var errors = new List<string>();

        if (attribute.ProductCategoryID != product.SubgroupID)
            errors.Add("O atributo informado não pertence à categoria do produto referenciado.");

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("O valor da especificação é obrigatório.");
            return errors;
        }

        switch (attribute.DataType)
        {
            case ProductSpecificationDataType.Number:
                if (!decimal.TryParse(value, out _))
                    errors.Add("O valor do atributo deve ser numérico válido.");
                break;
            case ProductSpecificationDataType.Boolean:
                if (!bool.TryParse(value, out _))
                    errors.Add("O valor do atributo deve ser 'true' ou 'false'.");
                break;
            case ProductSpecificationDataType.Enum:
                if (string.IsNullOrWhiteSpace(attribute.EnumOptions))
                {
                    errors.Add("O atributo do tipo Enum deve ter EnumOptions cadastrados.");
                    break;
                }

                var enumOptions = attribute.EnumOptions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (!enumOptions.Contains(value, StringComparer.OrdinalIgnoreCase))
                    errors.Add("O valor do atributo não está entre as opções cadastradas do Enum.");
                break;
            case ProductSpecificationDataType.Text:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return errors;
    }

    private async Task<Product> GetProductByIdAsync(int productId, CancellationToken cancellationToken)
    {
        var product = (await _productRepository.SearchGetByAsync(productId.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        if (product == null) throw new KeyNotFoundException($"Produto com ID {productId} não encontrado.");
        return product;
    }

    private static void ValidateStatusTransition(QuoteSectionStatus currentStatus, QuoteSectionStatus newStatus, QuoteSection section)
    {
        var canRejectFrom = new[] { QuoteSectionStatus.Draft, QuoteSectionStatus.AwaitingQuotation, QuoteSectionStatus.Sent, QuoteSectionStatus.Approved };

        if (newStatus == QuoteSectionStatus.ConvertedToOrder)
            throw new ValidationException("A conversão para pedido não está disponível neste passo.");

        if (newStatus == QuoteSectionStatus.Rejected && !canRejectFrom.Contains(currentStatus))
            throw new ValidationException("A seção só pode ser rejeitada a partir de um status anterior a 'ConvertedToOrder'.");

        var allowedTransitions = new Dictionary<QuoteSectionStatus, QuoteSectionStatus[]>
        {
            [QuoteSectionStatus.Draft] = [QuoteSectionStatus.AwaitingQuotation, QuoteSectionStatus.Rejected],
            [QuoteSectionStatus.AwaitingQuotation] = [QuoteSectionStatus.Sent, QuoteSectionStatus.Rejected],
            [QuoteSectionStatus.Sent] = [QuoteSectionStatus.Approved, QuoteSectionStatus.Rejected],
            [QuoteSectionStatus.Approved] = [QuoteSectionStatus.Rejected]
        };

        if (currentStatus == newStatus)
            throw new ValidationException("A transição de status não pode ser idêntica.");

        if (!allowedTransitions.TryGetValue(currentStatus, out var nextStatuses) || !nextStatuses.Contains(newStatus))
            throw new ValidationException("Transição de status inválida para a seção do orçamento.");

        if (newStatus == QuoteSectionStatus.Sent && section.Items.Any(i => i.UnitPrice is null))
            throw new ValidationException("A seção só pode ser enviada quando todos os itens têm preço unitário preenchido.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
