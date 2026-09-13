using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IPartnerPriceTableService
{
    Task<PartnerPriceTableDTO> GetPartnerPriceTableByIdAsync(int priceTableId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PartnerPriceTableDTO>> GetAllPartnerPriceTablesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<PartnerPriceTableDTO>> SearchPartnerPriceTablesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<PartnerPriceTableDTO>> GetByPartnerIdAsync(int partnerId, CancellationToken cancellationToken = default);
    Task<PartnerPriceTableDTO?> GetActiveByPartnerAndGroupAsync(int partnerId, int groupId, CancellationToken cancellationToken = default);
    Task SavePartnerPriceTableAsync(PartnerPriceTableDTO dto, CancellationToken cancellationToken = default);
    Task DeletePartnerPriceTableAsync(int priceTableId, CancellationToken cancellationToken = default);
}
