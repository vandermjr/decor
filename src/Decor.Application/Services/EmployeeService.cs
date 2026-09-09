using System.ComponentModel.DataAnnotations;
using Decor.Core.Common;
using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class EmployeeService(
        IEmployeeRepository employeeRepository,
        IDTOValidator<EmployeeDTO> dtoValidator,
        IRepositoryValidator<Employee> repoValidator,
        IAuthorizationService authorizationService) : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;
    private readonly IDTOValidator<EmployeeDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Employee> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<EmployeeDTO>> SearchEmployeesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Delega a busca para o repositório e mapeia o resultado para DTO
        var employees = await _employeeRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return employees.ToDTO();
    }

    public async Task<IEnumerable<EmployeeDTO>> GetAllEmployeesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Para obter todos, chamamos a busca com um argumento nulo
        var employees = await _employeeRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return employees.ToDTO();
    }

    public async Task<EmployeeDTO> GetEmployeeByIdAsync(int employeeID, CancellationToken cancellationToken = default)
    {
        var employee = (await _employeeRepository.SearchGetByAsync(employeeID.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return employee == null
            ? throw new KeyNotFoundException($"Funcionário com ID {employeeID} não encontrado.")
            : employee.ToDTO();
    }

    public async Task SaveEmployeeAsync(EmployeeDTO employeeDto, CancellationToken cancellationToken = default)
    {
        Require(employeeDto.EmployeeID == 0 ? DecorPermissions.EmployeesCreate : DecorPermissions.EmployeesEdit);
        // 1. Validação do DTO (regras de negócio que não dependem do banco)
        var dtoErrors = _dtoValidator.Validate(employeeDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        // 2. Mapeamento de DTO para Entidade de Domínio
        var employeeEntity = employeeDto.FromDTO();

        // 3. Validação de Repositório (regras que dependem do banco, ex: duplicidade)
        var repoErrors = _repoValidator.Validate(employeeEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        // 4. Persistência
        var affectedRows = await _employeeRepository.SaveAsync(employeeEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o funcionário.");
    }

    public async Task DeleteEmployeeAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.EmployeesDelete);
        var affectedRows = await _employeeRepository.DeleteAsync(employeeId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O funcionário não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
