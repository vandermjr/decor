namespace Decor.Core.Common;

public sealed record DecorFunctionalPermissionContext(
    string ModuleName,
    string ContextName,
    IReadOnlyList<string> PermissionCodes);

public static class DecorFunctionalPermissionCatalog
{
    public static IReadOnlyList<DecorFunctionalPermissionContext> Contexts { get; } =
        Array.AsReadOnly(new[]
        {
            new DecorFunctionalPermissionContext("Cadastros", "Produtos", new[]
            {
                DecorPermissions.ProductsView,
                DecorPermissions.ProductsCreate,
                DecorPermissions.ProductsEdit,
                DecorPermissions.ProductSpecificationAttributesView,
                DecorPermissions.ProductSpecificationAttributesCreate,
                DecorPermissions.ProductSpecificationAttributesEdit,
                DecorPermissions.ProductSpecificationAttributesDelete,
                DecorPermissions.ProductKitComponentsView,
                DecorPermissions.ProductKitComponentsCreate,
                DecorPermissions.ProductKitComponentsEdit,
                DecorPermissions.ProductKitComponentsDelete,
                DecorPermissions.UnitsOfMeasureView,
                DecorPermissions.UnitsOfMeasureCreate,
                DecorPermissions.UnitsOfMeasureEdit,
                DecorPermissions.UnitsOfMeasureDeactivate
            }),
            new DecorFunctionalPermissionContext("Cadastros", "Marcas", new[]
            {
                DecorPermissions.BrandsView,
                DecorPermissions.BrandsCreate,
                DecorPermissions.BrandsEdit,
                DecorPermissions.BrandsDelete
            }),
            new DecorFunctionalPermissionContext("Cadastros", "Classificações", new[]
            {
                DecorPermissions.ClassificationsView
            }),
            new DecorFunctionalPermissionContext("Cadastros", "Clientes", new[]
            {
                DecorPermissions.CustomersView,
                DecorPermissions.CustomersCreate,
                DecorPermissions.CustomersEdit,
                DecorPermissions.CustomersDelete
            }),
            new DecorFunctionalPermissionContext("Cadastros", "Fornecedores", new[]
            {
                DecorPermissions.SuppliersView,
                DecorPermissions.SuppliersCreate,
                DecorPermissions.SuppliersEdit,
                DecorPermissions.SuppliersDelete
            }),
            new DecorFunctionalPermissionContext("Cadastros", "Funcionários", new[]
            {
                DecorPermissions.EmployeesView,
                DecorPermissions.EmployeesCreate,
                DecorPermissions.EmployeesEdit,
                DecorPermissions.EmployeesDelete
            }),
            new DecorFunctionalPermissionContext("Cadastros", "Parceiros", new[]
            {
                DecorPermissions.PartnersView,
                DecorPermissions.PartnersCreate,
                DecorPermissions.PartnersEdit,
                DecorPermissions.PartnersDelete
            }),
            new DecorFunctionalPermissionContext("Compras", "Pedidos de Compra", new[]
            {
                DecorPermissions.PurchaseOrdersView,
                DecorPermissions.PurchaseOrdersCreate,
                DecorPermissions.PurchaseOrdersEdit,
                DecorPermissions.PurchaseOrdersDelete,
                DecorPermissions.PurchaseOrderItemsView,
                DecorPermissions.PurchaseOrderItemsCreate,
                DecorPermissions.PurchaseOrderItemsEdit,
                DecorPermissions.PurchaseOrderItemsDelete,
                DecorPermissions.PurchaseOrderInstallmentsView,
                DecorPermissions.PurchaseOrderInstallmentsCreatePlan,
                DecorPermissions.PurchaseOrderInstallmentsRegisterPayment,
                DecorPermissions.PurchaseOrderInstallmentsMarkOverdue,
                DecorPermissions.PurchaseOrderInstallmentsCancel
            }),
            new DecorFunctionalPermissionContext("Compras", "Recebimentos", new[]
            {
                DecorPermissions.GoodsReceiptsView,
                DecorPermissions.GoodsReceiptsRegister
            }),
            new DecorFunctionalPermissionContext("Estoque", "Estoque", new[]
            {
                DecorPermissions.StockMovementsView,
                DecorPermissions.StockMovementsEntry,
                DecorPermissions.StockMovementsExit
            }),
            new DecorFunctionalPermissionContext("Estoque", "Transferências", new[]
            {
                DecorPermissions.StockMovementsTransfer,
                DecorPermissions.StockMovementsReview
            }),
            new DecorFunctionalPermissionContext("Estoque", "Ajustes", new[]
            {
                DecorPermissions.StockMovementsAdjust
            }),
            new DecorFunctionalPermissionContext("Estoque", "Locais de Estoque", new[]
            {
                DecorPermissions.StockLocationsView,
                DecorPermissions.StockLocationsCreate,
                DecorPermissions.StockLocationsEdit,
                DecorPermissions.StockLocationsDelete
            }),
            new DecorFunctionalPermissionContext("Comercial", "Orçamentos", new[]
            {
                DecorPermissions.QuotesView,
                DecorPermissions.QuotesCreate,
                DecorPermissions.QuotesEdit,
                DecorPermissions.QuotesDelete,
                DecorPermissions.QuotesSend,
                DecorPermissions.QuotesApprove,
                DecorPermissions.TailorQuotationsView,
                DecorPermissions.TailorQuotationsCreate,
                DecorPermissions.TailorQuotationsRespond,
                DecorPermissions.TailorQuotationsClose,
                DecorPermissions.PartnerPriceTablesView,
                DecorPermissions.PartnerPriceTablesCreate,
                DecorPermissions.PartnerPriceTablesEdit,
                DecorPermissions.PartnerPriceTablesDelete
            }),
            new DecorFunctionalPermissionContext("Comercial", "Pedidos", new[]
            {
                DecorPermissions.OrdersView,
                DecorPermissions.OrdersConvertFromQuote,
                DecorPermissions.OrdersApprove,
                DecorPermissions.OrdersCancel,
                DecorPermissions.OrdersSendToProduction,
                DecorPermissions.StockReservationsView,
                DecorPermissions.StockReservationsCreate,
                DecorPermissions.StockReservationsRelease,
                DecorPermissions.OrderInstallmentsView,
                DecorPermissions.OrderInstallmentsCreatePlan,
                DecorPermissions.OrderInstallmentsRegisterPayment,
                DecorPermissions.OrderInstallmentsMarkOverdue,
                DecorPermissions.OrderInstallmentsCancel,
                DecorPermissions.OrderOccurrencesView,
                DecorPermissions.OrderOccurrencesRegister,
                DecorPermissions.OccurrenceReasonsView,
                DecorPermissions.OccurrenceReasonsCreate,
                DecorPermissions.OccurrenceReasonsEdit,
                DecorPermissions.OccurrenceReasonsDelete
            }),
            new DecorFunctionalPermissionContext("Serviços", "Serviços", new[]
            {
                DecorPermissions.ServicesView,
                DecorPermissions.ServicesCreate,
                DecorPermissions.ServicesEdit,
                DecorPermissions.ServicesDelete
            }),
            new DecorFunctionalPermissionContext("Serviços", "Agenda", new[]
            {
                DecorPermissions.InstallationAppointmentsView,
                DecorPermissions.InstallationAppointmentsCreate,
                DecorPermissions.InstallationAppointmentsReschedule,
                DecorPermissions.InstallationAppointmentsCancel,
                DecorPermissions.ServiceExecutionRecordsView,
                DecorPermissions.ServiceExecutionRecordsCreate
            }),
            new DecorFunctionalPermissionContext("Financeiro", "Contas a Pagar", new[]
            {
                DecorPermissions.AccountsPayableView,
                DecorPermissions.AccountsPayableCreate,
                DecorPermissions.AccountsPayableRegisterPayment,
                DecorPermissions.AccountsPayableCancel
            }),
            new DecorFunctionalPermissionContext("Financeiro", "Caixa", new[]
            {
                DecorPermissions.CashAccountsView,
                DecorPermissions.CashAccountsCreate,
                DecorPermissions.CashAccountsUpdate,
                DecorPermissions.CashAccountsDeactivate,
                DecorPermissions.CashTransactionsView,
                DecorPermissions.CashTransactionsCreate
            }),
            new DecorFunctionalPermissionContext("Configurações", "Usuários", new[]
            {
                DecorPermissions.UsersView,
                DecorPermissions.UsersCreate,
                DecorPermissions.UsersEdit,
                DecorPermissions.UsersActivate,
                DecorPermissions.UsersDeactivate,
                DecorPermissions.UsersResetPassword
            }),
            new DecorFunctionalPermissionContext("Configurações", "Grupos", new[]
            {
                DecorPermissions.RolesView,
                DecorPermissions.RolesEdit
            }),
            new DecorFunctionalPermissionContext("Configurações", "Permissões", new[]
            {
                DecorPermissions.UsersAssignRoles,
                DecorPermissions.UsersManagePermissions,
                DecorPermissions.UsersRestorePermissions,
                DecorPermissions.RolesManagePermissions,
                DecorPermissions.RolesRestoreDefaults
            }),
            new DecorFunctionalPermissionContext("Configurações", "Backup e restauração", new[]
            {
                DecorPermissions.DatabaseMaintenanceView,
                DecorPermissions.DatabaseMaintenanceRestore
            })
        });

    public static IReadOnlyList<string> ModuleNames { get; } =
        Array.AsReadOnly(new[]
        {
            "Cadastros",
            "Compras",
            "Estoque",
            "Comercial",
            "Serviços",
            "Financeiro",
            "Configurações"
        });

    public static IReadOnlyList<string> PermissionCodes { get; } =
        Array.AsReadOnly(Contexts.SelectMany(context => context.PermissionCodes).ToArray());

    public static IReadOnlyList<string> TransversalPermissionCodes { get; } =
        Array.AsReadOnly(new[]
        {
            DecorPermissions.PaymentMethodsView,
            DecorPermissions.PaymentMethodsCreate,
            DecorPermissions.PaymentMethodsEdit,
            DecorPermissions.PaymentMethodsDelete
        });

    public static IReadOnlyList<string> UnclassifiedPermissionCodes { get; } =
        Array.AsReadOnly(new[]
        {
            DecorPermissions.TermDeliveryView
        });
}