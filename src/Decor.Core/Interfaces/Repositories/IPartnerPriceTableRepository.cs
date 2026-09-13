using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IPartnerPriceTableRepository : IRepository<PartnerPriceTable>
{
    Task<PartnerPriceTable?> GetByIdAsync(int priceTableId, CancellationToken cancellationToken = default);
    Task<PartnerPriceTable?> GetActiveByPartnerAndGroupAsync(int partnerId, int groupId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PartnerPriceTable>> GetByPartnerIdAsync(int partnerId, CancellationToken cancellationToken = default);
    bool ActiveEntryExists(int partnerId, int groupId, int currentPriceTableId = 0);
    Task<bool> ActiveEntryExistsAsync(int partnerId, int groupId, int currentPriceTableId = 0, CancellationToken cancellationToken = default);
}
