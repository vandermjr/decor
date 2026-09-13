using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class ServiceExecutionRecordDTOValidator : IDTOValidator<ServiceExecutionRecordDTO>
{
    public IEnumerable<string> Validate(ServiceExecutionRecordDTO dto)
    {
        var errors = new List<string>();
        if (dto.ExecutionID < 0) errors.Add("O ID da execução não pode ser negativo.");
        if (dto.AppointmentID <= 0) errors.Add("O agendamento é obrigatório.");
        return errors;
    }
}
