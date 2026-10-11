using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class SuppliersViewModel : ContactRegistrationViewModel<SupplierDTO>
{
    public SuppliersViewModel(ISupplierService supplierService, IAuthorizationService authorizationService)
        : base(
            authorizationService,
            async (query, page, pageSize) => await supplierService.SearchSuppliersAsync(query, page, pageSize),
            async (page, pageSize) => await supplierService.GetAllSuppliersAsync(page, pageSize),
            supplier => supplier.SupplierID,
            supplier => supplier.IsActive,
            supplier => supplier.CorporateName,
            supplier => supplier.Document,
            supplier => supplier.Phone,
            supplier => supplier.Email,
            _ => null,
            (id, name, document, phone, email, _, active) => new SupplierDTO(id, name, document, phone, email, active),
            supplier => supplierService.SaveSupplierAsync(supplier),
            id => supplierService.DeleteSupplierAsync(id),
            DecorPermissions.SuppliersView,
            DecorPermissions.SuppliersCreate,
            DecorPermissions.SuppliersEdit,
            DecorPermissions.SuppliersDelete,
            "fornecedor",
            "fornecedores",
            "Razão social",
            "Pesquisar fornecedores")
    {
    }
}