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
    IProductRepository productRepository,
    ITailorQuotationRepository? tailorQuotationRepository = null,
    IUnitOfMeasureRepository? unitOfMeasureRepository = null,
    IServiceRepository? serviceRepository = null,
    IServiceCatalogService? serviceCatalogService = null) : IQuoteService
{
    private readonly IQuoteRepository _quoteRepository = quoteRepository;
    private readonly IDTOValidator<QuoteDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Quote> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly IProductSpecificationAttributeRepository _productSpecificationAttributeRepository = productSpecificationAttributeRepository;
    private readonly IProductRepository _productRepository = productRepository;
    private readonly ITailorQuotationRepository? _tailorQuotationRepository = tailorQuotationRepository;
    private readonly IUnitOfMeasureRepository? _unitOfMeasureRepository = unitOfMeasureRepository;

    public async Task<IEnumerable<UnitOfMeasureDTO>> GetQuantityUnitsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (!_authorizationService.HasPermission(DecorPermissions.QuotesView)
            && !_authorizationService.HasPermission(DecorPermissions.QuotesCreate)
            && !_authorizationService.HasPermission(DecorPermissions.QuotesEdit))
            throw new UnauthorizedAccessException("Sem permissao para consultar unidades de quantidade de orcamentos.");

        cancellationToken.ThrowIfCancellationRequested();
        if (_unitOfMeasureRepository is null) return Array.Empty<UnitOfMeasureDTO>();
        var units = await _unitOfMeasureRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return units.Select(unit => unit.ToDTO());
    }

    public async Task<IEnumerable<QuoteDTO>> SearchQuotesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => (await _quoteRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken)).Select(q => q.ToDTO());

    public async Task<IEnumerable<QuoteDTO>> GetAllQuotesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => (await _quoteRepository.SearchGetByAsync(null, page, pageSize, cancellationToken)).Select(q => q.ToDTO());

    public async Task<QuoteDTO> GetQuoteByIdAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken);
        return quote == null ? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.") : quote.ToDTO();
    }

    public async Task<QuoteDTO> CreateOpenQuoteAsync(int? employeeId, int? userId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesCreate);
        cancellationToken.ThrowIfCancellationRequested();
        if (employeeId is <= 0 || userId is <= 0)
            throw new ValidationException("Os identificadores de funcionário e usuário devem ser positivos quando informados.");

        var quote = new Quote
        {
            CreatedByEmployeeID = employeeId,
            CreatedByUserID = userId,
            SourceType = QuoteSourceType.DirectCapture,
            CreatedAt = DateTime.UtcNow
        };
        quote.Sections.Add(new QuoteSection
        {
            SectionType = QuoteSectionType.Catalog,
            Status = QuoteSectionStatus.Draft,
            CreatedAt = quote.CreatedAt
        });

        var affectedRows = await _quoteRepository.SaveAsync(quote, cancellationToken);
        if (affectedRows != 1 || quote.QuoteID <= 0 || quote.Sections.Any(section => section.QuoteSectionID <= 0))
            throw new InvalidOperationException("Não foi possível abrir o orçamento e sua seção inicial.");
        return quote.ToDTO();
    }

    public async Task CancelQuoteAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesApprove);
        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.");
        if (quote.Sections.Any(section => section.Status == QuoteSectionStatus.ConvertedToOrder))
            throw new ValidationException("Não é possível cancelar um orçamento com seção já convertida em pedido.");

        foreach (var section in quote.Sections.Where(section => section.Status != QuoteSectionStatus.Rejected))
            ValidateStatusTransition(section.Status, QuoteSectionStatus.Rejected, section);
        foreach (var section in quote.Sections)
            section.Status = QuoteSectionStatus.Rejected;

        if (await _quoteRepository.SaveAsync(quote, cancellationToken) != 1)
            throw new InvalidOperationException("Não foi possível cancelar o orçamento.");
    }

    public async Task<int> SaveQuoteAsync(QuoteDTO quoteDto, CancellationToken cancellationToken = default)
    {
        Require(quoteDto.QuoteID == 0 ? DecorPermissions.QuotesCreate : DecorPermissions.QuotesEdit);
        if (quoteDto.QuoteID != 0)
        {
            var existing = await _quoteRepository.GetCompleteQuoteAsync(quoteDto.QuoteID, cancellationToken)
                ?? throw new KeyNotFoundException($"Orçamento com ID {quoteDto.QuoteID} não encontrado.");
            if (existing.Sections.Any(section => section.Status == QuoteSectionStatus.ConvertedToOrder)
                && existing.DiscountAmount != quoteDto.DiscountAmount)
                throw new ValidationException("Não é possível alterar o desconto após a conversão de uma seção em pedido.");
            quoteDto = quoteDto with { Sections = existing.Sections.ToDTO().ToArray() };
        }
        var dtoErrors = _dtoValidator.Validate(quoteDto);
        if (dtoErrors.Any()) throw new ValidationException(string.Join("\n", dtoErrors));

        var quoteEntity = quoteDto.FromDTO();
        var repoErrors = _repoValidator.Validate(quoteEntity);
        if (repoErrors.Any()) throw new ValidationException(string.Join("\n", repoErrors));

        var affectedRows = await _quoteRepository.SaveAsync(quoteEntity, cancellationToken);
        if (affectedRows != 1) throw new InvalidOperationException("Não foi possível salvar o orçamento.");
        return quoteEntity.QuoteID;
    }

    public async Task DeleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.FromException(new ValidationException("Orçamentos não podem ser excluídos. Cancele ou converta o orçamento em pedido."));
    }

    public async Task<QuoteSectionDTO> CreateSectionAsync(int quoteId, QuoteSectionType sectionType, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesEdit);
        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.");
        ValidateDiscountAllocationIsEditable(quote);
        if (!Enum.IsDefined(sectionType))
            throw new ValidationException("O tipo da seção é inválido.");

        var section = new QuoteSection
        {
            QuoteID = quote.QuoteID,
            SectionType = sectionType,
            Status = QuoteSectionStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            Items = []
        };
        var affectedRows = await _quoteRepository.SaveSectionAsync(section, cancellationToken);
        if (affectedRows != 1) throw new InvalidOperationException("Não foi possível criar a seção do orçamento.");
        return section.ToDTO();
    }

    public async Task<QuoteItemDTO> SaveQuoteItemAsync(int quoteId, QuoteItemDTO quoteItemDto, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesEdit);
        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.");
        ValidateDiscountAllocationIsEditable(quote);
        var section = quote.Sections.FirstOrDefault(item => item.QuoteSectionID == quoteItemDto.QuoteSectionID)
            ?? throw new KeyNotFoundException($"Seção com ID {quoteItemDto.QuoteSectionID} não pertence ao orçamento {quoteId}.");
        if (section.Status != QuoteSectionStatus.Draft)
            throw new ValidationException("Só é possível alterar itens de uma seção em rascunho.");
        if (quoteItemDto.Quantity <= 0)
            throw new ValidationException("A quantidade deve ser maior que zero.");
        if (quoteItemDto.UnitPrice is null or < 0)
            throw new ValidationException("Informe um preço unitário igual ou maior que zero.");

        if (quoteItemDto.QuoteItemID != 0 && !section.Items.Any(item => item.QuoteItemID == quoteItemDto.QuoteItemID))
            throw new ValidationException("O item informado não pertence à seção selecionada.");

        if (!CommercialItemReference.IsValid(quoteItemDto.ProductID, quoteItemDto.ServiceID))
            throw new ValidationException(CommercialItemReference.ValidationMessage);

        Product? product = null;
        if (quoteItemDto.ServiceID is int serviceId)
        {
            if (serviceRepository is null || !serviceRepository.ServiceExists(serviceId))
                throw new ValidationException("O servico informado nao existe.");
            var service = await (serviceCatalogService ?? throw new InvalidOperationException("O catalogo de servicos e necessario."))
                .GetServiceByIdAsync(serviceId, cancellationToken);
            if (!service.IsActive)
                throw new ValidationException("Nao e possivel incluir um servico inativo no orcamento.");
        }
        else
            product = await GetProductByIdAsync(quoteItemDto.ProductID!.Value, cancellationToken);

        if (product is { IsActive: false })
            throw new ValidationException("Não é possível incluir um produto inativo no orçamento.");

        UnitOfMeasure? unit = null;
        if (product?.StockUnitID is int unitId)
        {
            unit = _unitOfMeasureRepository is not null
                ? await _unitOfMeasureRepository.GetByIdAsync(unitId, cancellationToken)
                : product.StockUnit;
            if (unit is null)
                throw new ValidationException("A unidade de medida do produto não foi encontrada.");
        }
        var decimalPlaces = QuoteQuantityRules.DecimalPlaces(unit?.AllowsFraction);
        if (!QuoteQuantityRules.IsValid(quoteItemDto.Quantity, decimalPlaces))
            throw new ValidationException($"A quantidade deve ser positiva, no máximo {QuoteQuantityRules.Maximum}, com até {decimalPlaces} casas decimais.");

        var entity = quoteItemDto.FromDTO();
        var affectedRows = await _quoteRepository.SaveItemAsync(entity, cancellationToken);
        if (affectedRows != 1) throw new InvalidOperationException("Não foi possível salvar o item do orçamento.");
        return entity.ToDTO();
    }

    public async Task DeleteQuoteItemAsync(int quoteId, int quoteItemId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesEdit);
        cancellationToken.ThrowIfCancellationRequested();
        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orcamento com ID {quoteId} nao encontrado.");
        var section = quote.Sections.FirstOrDefault(candidate => candidate.QuoteID == quoteId
            && candidate.Items.Any(item => item.QuoteItemID == quoteItemId && item.QuoteSectionID == candidate.QuoteSectionID))
            ?? throw new KeyNotFoundException($"Item com ID {quoteItemId} nao pertence ao orcamento {quoteId}.");
        if (section.Status != QuoteSectionStatus.Draft)
            throw new ValidationException("So e possivel excluir itens de uma secao em rascunho.");
        ValidateDiscountAllocationIsEditable(quote);
        var subtotal = quote.Sections.SelectMany(candidate => candidate.Items)
            .Where(item => item.QuoteItemID != quoteItemId)
            .Sum(item => item.Quantity * (item.UnitPrice ?? 0m));
        if (subtotal < quote.DiscountAmount)
            throw new ValidationException("O subtotal restante nao pode ser menor que o desconto do orcamento.");

        cancellationToken.ThrowIfCancellationRequested();
        if (await _quoteRepository.DeleteItemAsync(quoteItemId, section.QuoteSectionID, cancellationToken) != 1)
            throw new InvalidOperationException("O item do orcamento nao foi encontrado ou nao pode ser excluido.");
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

        if (newStatus == QuoteSectionStatus.Sent && section.SectionType == QuoteSectionType.Custom && _tailorQuotationRepository != null)
        {
            var tailorRequests = await _tailorQuotationRepository.GetByQuoteSectionIdAsync(sectionId, cancellationToken);
            if (tailorRequests.Any(r => r.Status != TailorQuotationRequestStatus.Closed))
            {
                throw new ValidationException("Para seções do tipo Custom, todas as solicitações de cotação vinculadas devem estar com status Fechado (Closed) antes do envio.");
            }
        }

        section.Status = newStatus;
        if (newStatus == QuoteSectionStatus.Sent)
            section.SentToCustomerAt = DateTime.UtcNow;
        if (newStatus == QuoteSectionStatus.Approved)
            section.ApprovedAt = DateTime.UtcNow;

        await _quoteRepository.SaveSectionAsync(section, cancellationToken);
    }

    public async Task AddQuoteItemAsync(int quoteId, QuoteItemDTO quoteItemDto, CancellationToken cancellationToken = default)
    {
        await SaveQuoteItemAsync(quoteId, quoteItemDto, cancellationToken);
    }

    public async Task AddSpecificationValueAsync(int quoteId, int quoteItemId, QuoteItemSpecificationValueDTO valueDto, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.QuotesEdit);

        var quote = await _quoteRepository.GetCompleteQuoteAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Orçamento com ID {quoteId} não encontrado.");

        var item = quote.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.QuoteItemID == quoteItemId)
            ?? throw new KeyNotFoundException($"Item com ID {quoteItemId} não encontrado.");

        if (item.ServiceID.HasValue || item.ProductID is not > 0)
            throw new ValidationException("Especificacoes de produto nao se aplicam a itens de servico.");
        var product = await GetProductByIdAsync(item.ProductID.Value, cancellationToken);
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
        var product = (await _productRepository.SearchGetByAsync(productId.ToString(), 1, 1, cancellationToken))
            .FirstOrDefault(candidate => candidate.ProductID == productId);
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

    private static void ValidateDiscountAllocationIsEditable(Quote quote)
    {
        if (quote.DiscountAmount > 0 && quote.Sections.Any(section => section.Status == QuoteSectionStatus.ConvertedToOrder))
            throw new ValidationException("Não é possível alterar os itens ou criar seções após a conversão de um orçamento com desconto rateado.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
