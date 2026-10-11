using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class CustomersViewModel : ContactRegistrationViewModel<CustomerDTO>
{
    public CustomersViewModel(ICustomerService customerService, IAuthorizationService authorizationService)
        : base(
            authorizationService,
            async (query, page, pageSize) => await customerService.SearchCustomersAsync(query, page, pageSize),
            async (page, pageSize) => await customerService.GetAllCustomersAsync(page, pageSize),
            customer => customer.CustomerID,
            customer => customer.IsActive,
            customer => customer.Name,
            customer => customer.Document,
            customer => customer.Phone,
            customer => customer.Email,
            customer => customer.Address,
            (id, name, document, phone, email, address, active) => new CustomerDTO(id, name, document, phone, email, address, active),
            customer => customerService.SaveCustomerAsync(customer),
            id => customerService.DeleteCustomerAsync(id),
            DecorPermissions.CustomersView,
            DecorPermissions.CustomersCreate,
            DecorPermissions.CustomersEdit,
            DecorPermissions.CustomersDelete,
            "cliente",
            "clientes",
            "Nome",
            "Pesquisar clientes")
    {
    }
}