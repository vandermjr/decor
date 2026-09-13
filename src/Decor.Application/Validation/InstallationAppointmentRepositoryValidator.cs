using Decor.Core.Entities;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class InstallationAppointmentRepositoryValidator : IRepositoryValidator<InstallationAppointment>
{
    public IEnumerable<string> Validate(InstallationAppointment entity) => [];
}