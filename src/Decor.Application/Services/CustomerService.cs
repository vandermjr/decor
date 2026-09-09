using System.ComponentModel.DataAnnotations;
using Decor.Core.Common;
using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class CustomerService(
        ICustomerRepository customerRepository,
        IDTOValidator<CustomerDTO> dtoValidator,
        IRepositoryValidator<Customer> repoValidator,
        IAuthorizationService authorizationService) : ICustomerService
{
    private readonly ICustomerRepository _customerRepository = customerRepository;
    private readonly IDTOValidator<CustomerDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Customer> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<CustomerDTO>> SearchCustomersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Delega a busca para o repositório e mapeia o resultado para DTO
        var customers = await _customerRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return customers.ToDTO();
    }

    public async Task<IEnumerable<CustomerDTO>> GetAllCustomersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Para obter todos, chamamos a busca com um argumento nulo
        var customers = await _customerRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return customers.ToDTO();
    }

    public async Task<CustomerDTO> GetCustomerByIdAsync(int customerID, CancellationToken cancellationToken = default)
    {
        var customer = (await _customerRepository.SearchGetByAsync(customerID.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return customer == null
            ? throw new KeyNotFoundException($"Cliente com ID {customerID} não encontrado.")
            : customer.ToDTO();
    }

    public async Task SaveCustomerAsync(CustomerDTO customerDto, CancellationToken cancellationToken = default)
    {
        Require(customerDto.CustomerID == 0 ? DecorPermissions.CustomersCreate : DecorPermissions.CustomersEdit);
        // 1. Validação do DTO (regras de negócio que não dependem do banco)
        var dtoErrors = _dtoValidator.Validate(customerDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        // 2. Mapeamento de DTO para Entidade de Domínio
        var customerEntity = customerDto.FromDTO();

        // 3. Validação de Repositório (regras que dependem do banco, ex: duplicidade)
        var repoErrors = _repoValidator.Validate(customerEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        // 4. Persistência
        var affectedRows = await _customerRepository.SaveAsync(customerEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o cliente.");
    }

    public async Task DeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.CustomersDelete);
        var affectedRows = await _customerRepository.DeleteAsync(customerId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O cliente não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
