using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IPartnerService
{
    Task<PartnerDTO> GetPartnerByIdAsync(int partnerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PartnerDTO>> GetAllPartnersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<PartnerDTO>> SearchPartnersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SavePartnerAsync(PartnerDTO partner, CancellationToken cancellationToken = default);
    Task DeletePartnerAsync(int partnerId, CancellationToken cancellationToken = default);
}
