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

public class ServiceCatalogService(
    IServiceRepository repository,
    IDTOValidator<ServiceDTO> dtoValidator,
    IRepositoryValidator<Service> repositoryValidator,
    IAuthorizationService authorizationService) : IServiceCatalogService
{
    public async Task<IEnumerable<ServiceDTO>> SearchServicesAsync(string? searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Require(DecorPermissions.ServicesView);
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var services = await repository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return services.ToDTO().ToArray();
    }

    public async Task<ServiceDTO> GetServiceByIdAsync(int serviceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Require(DecorPermissions.ServicesView);
        if (serviceId <= 0) throw new ArgumentOutOfRangeException(nameof(serviceId));
        var services = await repository.SearchGetByAsync(serviceId.ToString(CultureInfo.InvariantCulture), 1, 1, cancellationToken);
        var service = services.FirstOrDefault(service => service.ServiceID == serviceId);
        return service?.ToDTO() ?? throw new KeyNotFoundException($"Servico com ID {serviceId} nao encontrado.");
    }

    public async Task SaveServiceAsync(ServiceDTO serviceDto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(serviceDto);
        Require(serviceDto.ServiceID == 0 ? DecorPermissions.ServicesCreate : DecorPermissions.ServicesEdit);
        var dtoErrors = dtoValidator.Validate(serviceDto).ToArray();
        if (dtoErrors.Length > 0) throw new ValidationException(string.Join("\n", dtoErrors));
        var service = serviceDto.FromDTO();
        cancellationToken.ThrowIfCancellationRequested();
        var repositoryErrors = repositoryValidator.Validate(service).ToArray();
        if (repositoryErrors.Length > 0) throw new ValidationException(string.Join("\n", repositoryErrors));
        cancellationToken.ThrowIfCancellationRequested();
        if (await repository.SaveAsync(service, cancellationToken) != 1)
            throw new InvalidOperationException("Nao foi possivel salvar o servico.");
    }

    public async Task DeleteServiceAsync(int serviceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Require(DecorPermissions.ServicesDelete);
        if (serviceId <= 0) throw new ArgumentOutOfRangeException(nameof(serviceId));
        if (await repository.DeleteAsync(serviceId, cancellationToken) != 1)
            throw new InvalidOperationException("O servico nao foi encontrado ou nao pode ser excluido.");
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Voce nao possui permissao para esta operacao.");
    }
}