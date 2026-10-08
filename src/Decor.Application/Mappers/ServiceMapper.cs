using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Application.Mappers;

public static class ServiceMapper
{
    public static ServiceDTO ToDTO(this Service service) => new(
        service.ServiceID, service.Description, service.IsActive, service.CostPrice,
        service.SalePrice, service.EmployeeCommissionValue, service.Observations);

    public static IEnumerable<ServiceDTO> ToDTO(this IEnumerable<Service> services) => services.Select(ToDTO);

    public static Service FromDTO(this ServiceDTO dto) => new()
    {
        ServiceID = dto.ServiceID,
        Description = dto.Description ?? string.Empty,
        IsActive = dto.IsActive,
        CostPrice = dto.CostPrice,
        SalePrice = dto.SalePrice,
        EmployeeCommissionValue = dto.EmployeeCommissionValue,
        Observations = dto.Observations
    };
}