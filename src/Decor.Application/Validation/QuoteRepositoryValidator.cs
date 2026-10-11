using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class QuoteRepositoryValidator(IQuoteRepository repository, IPartnerRepository partnerRepository, IServiceRepository? serviceRepository = null) : IRepositoryValidator<Quote>
{
    private readonly IQuoteRepository _repository = repository;

    public IEnumerable<string> Validate(Quote entity)
    {
        var errors = new List<string>();

        foreach (var item in entity.Sections.SelectMany(section => section.Items))
        {
            if (!CommercialItemReference.IsValid(item.ProductID, item.ServiceID))
                errors.Add(CommercialItemReference.ValidationMessage);
            else if (item.ServiceID is int serviceId && (serviceRepository is null || !serviceRepository.ServiceExists(serviceId)))
                errors.Add("O servico informado nao existe.");
        }

        if (entity.CustomerID is null or <= 0)
            errors.Add("O cliente do orçamento é obrigatório.");

        if (entity.CreatedByEmployeeID is null or <= 0)
            errors.Add("O funcionário responsável pela criação do orçamento é obrigatório.");

        if ((int)entity.SourceType is < 1 or > 3)
            errors.Add("O tipo de origem do orçamento é inválido.");

        if (entity.SourcePartnerID is int partnerId && (partnerId <= 0 ||
            !partnerRepository.SearchGetBy(partnerId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Any(partner => partner.PartnerID == partnerId)))
            errors.Add("O parceiro de origem do orçamento é inválido.");

        return errors;
    }
}
