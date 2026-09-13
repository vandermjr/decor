using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class InstallationAppointmentDTOValidator : IDTOValidator<InstallationAppointmentDTO>
{
    public IEnumerable<string> Validate(InstallationAppointmentDTO dto)
    {
        var errors = new List<string>();
        if (dto.AppointmentID < 0) errors.Add("O ID do agendamento não pode ser negativo.");
        if (dto.OrderItemID <= 0) errors.Add("O item do pedido é obrigatório.");
        if (dto.CreatedByEmployeeID <= 0) errors.Add("O funcionário que criou o agendamento é obrigatório.");
        if ((dto.ExecutorEmployeeID.HasValue ? 1 : 0) + (dto.ExecutorPartnerID.HasValue ? 1 : 0) != 1) errors.Add("Informe exatamente um executor: funcionário ou parceiro.");
        return errors;
    }
}