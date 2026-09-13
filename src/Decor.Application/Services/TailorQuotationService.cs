using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class TailorQuotationService(
    ITailorQuotationRepository tailorQuotationRepository,
    IQuoteRepository quoteRepository,
    IPartnerPriceTableRepository partnerPriceTableRepository,
    IProductRepository productRepository,
    IProductSpecificationAttributeRepository productSpecificationAttributeRepository,
    IDTOValidator<TailorQuotationRequestDTO> requestDtoValidator,
    IDTOValidator<TailorQuotationRevisionDTO> revisionDtoValidator,
    IRepositoryValidator<TailorQuotationRequest> repoValidator,
    IAuthorizationService authorizationService) : ITailorQuotationService
{
    private readonly ITailorQuotationRepository _tailorQuotationRepository = tailorQuotationRepository;
    private readonly IQuoteRepository _quoteRepository = quoteRepository;
    private readonly IPartnerPriceTableRepository _partnerPriceTableRepository = partnerPriceTableRepository;
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IProductSpecificationAttributeRepository _productSpecificationAttributeRepository = productSpecificationAttributeRepository;
    private readonly IDTOValidator<TailorQuotationRequestDTO> _requestDtoValidator = requestDtoValidator;
    private readonly IDTOValidator<TailorQuotationRevisionDTO> _revisionDtoValidator = revisionDtoValidator;
    private readonly IRepositoryValidator<TailorQuotationRequest> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<TailorQuotationRequestDTO>> SearchRequestsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.TailorQuotationsView);
        var requests = await _tailorQuotationRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return requests.ToDTO();
    }

    public async Task<IEnumerable<TailorQuotationRequestDTO>> GetAllRequestsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.TailorQuotationsView);
        var requests = await _tailorQuotationRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return requests.ToDTO();
    }

    public async Task<TailorQuotationRequestDTO> GetRequestByIdAsync(int requestId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.TailorQuotationsView);
        var request = await _tailorQuotationRepository.GetByIdAsync(requestId, cancellationToken);
        return request == null
            ? throw new KeyNotFoundException($"Solicitação de cotação com ID {requestId} não encontrada.")
            : request.ToDTO();
    }

    public async Task<IEnumerable<TailorQuotationRevisionDTO>> GetRevisionsByRequestIdAsync(int requestId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.TailorQuotationsView);
        var revisions = await _tailorQuotationRepository.GetRevisionsByRequestIdAsync(requestId, cancellationToken);
        return revisions.ToDTO();
    }

    public async Task CreateRequestAsync(TailorQuotationRequestDTO requestDto, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.TailorQuotationsCreate);

        var dtoErrors = _requestDtoValidator.Validate(requestDto);
        if (dtoErrors.Any()) throw new ValidationException(string.Join("\n", dtoErrors));

        var section = await _quoteRepository.GetSectionByItemIdAsync(requestDto.QuoteItemID, cancellationToken);
        if (section == null)
            throw new KeyNotFoundException($"Item do orçamento com ID {requestDto.QuoteItemID} não encontrado.");

        if (section.SectionType != QuoteSectionType.Custom)
            throw new ValidationException("A solicitação de cotação só pode ser criada para itens de uma seção do tipo sob medida (Custom).");

        var existingRequests = await _tailorQuotationRepository.GetByQuoteItemIdAsync(requestDto.QuoteItemID, cancellationToken);
        if (existingRequests.Any(r => r.Status != TailorQuotationRequestStatus.Closed))
            throw new ValidationException("Já existe uma solicitação de cotação aberta ou respondida para este item.");

        var entity = requestDto.FromDTO();
        if (entity.RequestedAt == default)
            entity.RequestedAt = DateTime.UtcNow;
        entity.Status = TailorQuotationRequestStatus.Open;

        var repoErrors = _repoValidator.Validate(entity);
        if (repoErrors.Any()) throw new ValidationException(string.Join("\n", repoErrors));

        var affectedRows = await _tailorQuotationRepository.SaveAsync(entity, cancellationToken);
        if (affectedRows != 1) throw new InvalidOperationException("Não foi possível salvar a solicitação de cotação.");
    }

    public async Task AddRevisionAsync(TailorQuotationRevisionDTO revisionDto, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.TailorQuotationsRespond);

        var dtoErrors = _revisionDtoValidator.Validate(revisionDto);
        if (dtoErrors.Any()) throw new ValidationException(string.Join("\n", dtoErrors));

        var request = await _tailorQuotationRepository.GetByIdAsync(revisionDto.RequestID, cancellationToken)
            ?? throw new KeyNotFoundException($"Solicitação de cotação com ID {revisionDto.RequestID} não encontrada.");

        if (request.Status == TailorQuotationRequestStatus.Closed)
            throw new ValidationException("Não é possível adicionar revisão a uma solicitação já fechada.");

        var existingRevisions = await _tailorQuotationRepository.GetRevisionsByRequestIdAsync(request.RequestID, cancellationToken);
        var nextRevisionNumber = existingRevisions.Any() ? existingRevisions.Max(r => r.RevisionNumber) + 1 : 1;

        var revision = revisionDto.FromDTO();
        revision.RevisionNumber = nextRevisionNumber;
        if (revision.RespondedAt == default)
            revision.RespondedAt = DateTime.UtcNow;

        await _tailorQuotationRepository.SaveRevisionAsync(revision, cancellationToken);

        request.Status = TailorQuotationRequestStatus.Answered;
        await _tailorQuotationRepository.SaveAsync(request, cancellationToken);

        var item = await _quoteRepository.GetItemByIdAsync(request.QuoteItemID, cancellationToken)
            ?? throw new KeyNotFoundException($"Item de orçamento com ID {request.QuoteItemID} não encontrado.");

        item.UnitPrice = revision.Price;
        await _quoteRepository.SaveItemAsync(item, cancellationToken);
    }

    public async Task CloseRequestAsync(int requestId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.TailorQuotationsClose);

        var request = await _tailorQuotationRepository.GetByIdAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Solicitação de cotação com ID {requestId} não encontrada.");

        if (request.Status == TailorQuotationRequestStatus.Closed)
            throw new ValidationException("A solicitação de cotação já está fechada.");

        var existingRevisions = await _tailorQuotationRepository.GetRevisionsByRequestIdAsync(requestId, cancellationToken);
        if (!existingRevisions.Any())
            throw new ValidationException("Não é possível fechar uma solicitação de cotação sem ao menos uma revisão registrada.");

        request.Status = TailorQuotationRequestStatus.Closed;
        await _tailorQuotationRepository.SaveAsync(request, cancellationToken);
    }

    public async Task<decimal?> GetSuggestedFabricationPriceAsync(int quoteItemId, int partnerId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.TailorQuotationsView);

        var quoteItem = await _quoteRepository.GetItemByIdAsync(quoteItemId, cancellationToken);
        if (quoteItem == null)
            return null;

        var product = (await _productRepository.SearchGetByAsync(quoteItem.ProductID.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        if (product == null)
            return null;

        if (product.SubgroupID is null || product.Subgroup?.GroupID is null)
            return null;

        var groupId = product.Subgroup.GroupID;
        var partnerTable = await _partnerPriceTableRepository.GetActiveByPartnerAndGroupAsync(partnerId, groupId, cancellationToken);
        if (partnerTable == null)
            return null;

        var values = (await _quoteRepository.GetSpecificationValuesByItemIdAsync(quoteItemId, cancellationToken)).ToList();
        decimal? width = null;
        decimal? height = null;

        foreach (var value in values)
        {
            var attribute = (await _productSpecificationAttributeRepository.SearchGetByAsync(value.AttributeID.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
            if (attribute == null)
                continue;

            if (attribute.MeasurementRole == MeasurementRole.Width && decimal.TryParse(value.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedWidth))
                width = parsedWidth;
            else if (attribute.MeasurementRole == MeasurementRole.Height && decimal.TryParse(value.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedHeight))
                height = parsedHeight;
        }

        if (width is null || height is null)
            return null;

        var area = width.Value * height.Value;
        return area * partnerTable.PricePerSquareMeter;
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
