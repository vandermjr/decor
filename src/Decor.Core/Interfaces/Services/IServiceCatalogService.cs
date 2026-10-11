using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IServiceCatalogService
{
    Task<IEnumerable<ServiceDTO>> SearchServicesAsync(string? searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<ServiceDTO> GetServiceByIdAsync(int serviceId, CancellationToken cancellationToken = default);
    Task SaveServiceAsync(ServiceDTO serviceDto, CancellationToken cancellationToken = default);
    Task DeleteServiceAsync(int serviceId, CancellationToken cancellationToken = default);
}