namespace Decor.Core.Common;

/// <summary>Portuguese, hierarchical presentation metadata for persisted permission codes.</summary>
public sealed record DecorPermissionPresentation(string ModuleName, string FormName, string ActionName);

public static class DecorPermissionPresentationCatalog
{
    public static readonly IReadOnlyList<string> ModuleNames =
    ["Cadastros", "Compras", "Estoque", "Comercial", "Serviços", "Financeiro", "Configurações"];

    private static readonly IReadOnlyDictionary<string, (string Module, string Form)> Resources =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Products"] = ("Cadastros", "Produtos"), ["Brands"] = ("Cadastros", "Marcas"),
            ["Classifications"] = ("Cadastros", "Classificações"), ["Customers"] = ("Cadastros", "Clientes"),
            ["Suppliers"] = ("Cadastros", "Fornecedores"), ["Employees"] = ("Cadastros", "Funcionários"),
            ["Partners"] = ("Cadastros", "Parceiros"), ["StockLocations"] = ("Cadastros", "Locais de Estoque"),
            ["UnitsOfMeasure"] = ("Cadastros", "Unidades de Medida"), ["ProductSpecificationAttributes"] = ("Cadastros", "Atributos de Especificação de Produto"),
            ["ProductKitComponents"] = ("Cadastros", "Componentes de Kit de Produto"), ["PartnerPriceTables"] = ("Cadastros", "Tabelas de Preços de Parceiros"),
            ["TermDelivery"] = ("Cadastros", "Termo de Entrega"),
            ["PurchaseOrders"] = ("Compras", "Pedidos de Compra"), ["PurchaseOrderItems"] = ("Compras", "Itens de Pedido de Compra"),
            ["GoodsReceipts"] = ("Compras", "Recebimentos"), ["PurchaseOrderInstallments"] = ("Compras", "Parcelas de Pedido de Compra"),
            ["StockMovements"] = ("Estoque", "Movimentações de Estoque"), ["StockReservations"] = ("Estoque", "Reservas de Estoque"),
            ["Quotes"] = ("Comercial", "Orçamentos"), ["TailorQuotations"] = ("Comercial", "Cotações Sob Medida"),
            ["Orders"] = ("Comercial", "Pedidos"), ["OrderOccurrences"] = ("Comercial", "Ocorrências de Pedido"), ["OccurrenceReasons"] = ("Comercial", "Motivos de Ocorrência"),
            ["InstallationAppointments"] = ("Serviços", "Agendamentos de Instalação"), ["ServiceExecutionRecords"] = ("Serviços", "Execuções de Serviços"),
            ["PaymentMethods"] = ("Financeiro", "Formas de Pagamento"), ["OrderInstallments"] = ("Financeiro", "Parcelas de Pedido"),
            ["CashAccounts"] = ("Financeiro", "Contas de Caixa"), ["CashTransactions"] = ("Financeiro", "Movimentações de Caixa"), ["AccountsPayable"] = ("Financeiro", "Contas a Pagar"),
            ["Users"] = ("Configurações", "Usuários"), ["Roles"] = ("Configurações", "Grupos de Permissões"), ["DatabaseMaintenance"] = ("Configurações", "Administração do Sistema")
        };

    private static readonly IReadOnlyDictionary<string, string> Actions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["View"] = "Consultar", ["Create"] = "Cadastrar", ["Edit"] = "Editar", ["Delete"] = "Excluir",
            ["Activate"] = "Ativar", ["Deactivate"] = "Desativar", ["AssignRoles"] = "Atribuir grupos",
            ["ManagePermissions"] = "Gerenciar permissões", ["RestorePermissions"] = "Restaurar permissões",
            ["RestoreDefaults"] = "Restaurar permissões", ["ResetPassword"] = "Redefinir senha", ["Register"] = "Registrar",
            ["Approve"] = "Aprovar", ["Cancel"] = "Cancelar", ["Respond"] = "Responder", ["Close"] = "Encerrar",
            ["Release"] = "Liberar", ["Update"] = "Atualizar", ["Entry"] = "Registrar entrada", ["Exit"] = "Registrar saída",
            ["Adjust"] = "Ajustar", ["Transfer"] = "Transferir", ["Review"] = "Revisar", ["Send"] = "Enviar",
            ["ConvertFromQuote"] = "Converter orçamento", ["SendToProduction"] = "Enviar para produção", ["CreatePlan"] = "Criar plano",
            ["RegisterPayment"] = "Registrar pagamento", ["MarkOverdue"] = "Marcar em atraso", ["Reschedule"] = "Reagendar"
        };

    public static DecorPermissionPresentation Describe(string? permissionCode)
    {
        var parts = (permissionCode ?? string.Empty).Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var resource = parts.FirstOrDefault() ?? string.Empty;
        var action = parts.Skip(1).FirstOrDefault() ?? string.Empty;
        var location = Resources.TryGetValue(resource, out var value) ? value : (Module: "Administração", Form: "Permissão não catalogada");
        return new DecorPermissionPresentation(location.Module, location.Form,
            Actions.TryGetValue(action, out var actionName) ? actionName : "Ação não catalogada");
    }
}
