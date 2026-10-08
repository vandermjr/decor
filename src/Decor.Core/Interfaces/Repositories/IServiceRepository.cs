using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IServiceRepository : IRepository<Service>
{
    bool ServiceExists(int serviceId);
}