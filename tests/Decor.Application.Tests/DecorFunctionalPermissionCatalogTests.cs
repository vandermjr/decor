using System.Reflection;
using Decor.Core.Common;
using FluentAssertions;

namespace Decor.Application.Tests;

public sealed class DecorFunctionalPermissionCatalogTests
{
    [Fact]
    public void Contexts_are_declared_in_the_required_order()
    {
        var expected = new[]
        {
            ("Cadastros", "Produtos"),
            ("Cadastros", "Marcas"),
            ("Cadastros", "Classificações"),
            ("Cadastros", "Clientes"),
            ("Cadastros", "Fornecedores"),
            ("Cadastros", "Funcionários"),
            ("Cadastros", "Parceiros"),
            ("Compras", "Pedidos de Compra"),
            ("Compras", "Recebimentos"),
            ("Estoque", "Estoque"),
            ("Estoque", "Transferências"),
            ("Estoque", "Ajustes"),
            ("Estoque", "Locais de Estoque"),
            ("Comercial", "Orçamentos"),
            ("Comercial", "Pedidos"),
            ("Serviços", "Agenda"),
            ("Financeiro", "Contas a Pagar"),
            ("Financeiro", "Caixa"),
            ("Administração", "Usuários"),
            ("Administração", "Grupos de Permissões"),
            ("Administração", "Manutenção do Banco")
        };

        DecorFunctionalPermissionCatalog.Contexts
            .Should().HaveCount(21);

        DecorFunctionalPermissionCatalog.Contexts
            .Select(context => (context.ModuleName, context.ContextName))
            .Should().Equal(expected);
    }

    [Fact]
    public void Contexts_and_permission_codes_are_unique()
    {
        var contexts = DecorFunctionalPermissionCatalog.Contexts;

        contexts.Select(context => (context.ModuleName, context.ContextName))
            .Should().OnlyHaveUniqueItems();
        contexts.SelectMany(context => context.PermissionCodes)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Catalog_covers_all_public_technical_permission_codes()
    {
        var technicalCodes = typeof(DecorPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();
        var classifiedCodes = DecorFunctionalPermissionCatalog.PermissionCodes
            .Concat(DecorFunctionalPermissionCatalog.TransversalPermissionCodes)
            .Concat(DecorFunctionalPermissionCatalog.UnclassifiedPermissionCodes)
            .ToArray();

        technicalCodes.Should().HaveCount(129);
        DecorFunctionalPermissionCatalog.PermissionCodes.Should().HaveCount(124);
        DecorFunctionalPermissionCatalog.TransversalPermissionCodes.Should().HaveCount(4);
        DecorFunctionalPermissionCatalog.UnclassifiedPermissionCodes.Should().HaveCount(1);
        classifiedCodes.Should().OnlyHaveUniqueItems();
        classifiedCodes.Should().BeEquivalentTo(technicalCodes);
    }

    [Fact]
    public void Payment_methods_and_term_delivery_are_explicitly_excluded_from_contexts()
    {
        var contextualCodes = DecorFunctionalPermissionCatalog.PermissionCodes;

        contextualCodes.Should().NotContain(DecorPermissions.PaymentMethodsView);
        contextualCodes.Should().NotContain(DecorPermissions.PaymentMethodsCreate);
        contextualCodes.Should().NotContain(DecorPermissions.PaymentMethodsEdit);
        contextualCodes.Should().NotContain(DecorPermissions.PaymentMethodsDelete);
        contextualCodes.Should().NotContain(DecorPermissions.TermDeliveryView);

        DecorFunctionalPermissionCatalog.TransversalPermissionCodes
            .Should().Equal(
                DecorPermissions.PaymentMethodsView,
                DecorPermissions.PaymentMethodsCreate,
                DecorPermissions.PaymentMethodsEdit,
                DecorPermissions.PaymentMethodsDelete);
        DecorFunctionalPermissionCatalog.UnclassifiedPermissionCodes
            .Should().Equal(DecorPermissions.TermDeliveryView);
    }

    [Fact]
    public void Modules_are_exactly_the_declared_functional_modules()
    {
        DecorFunctionalPermissionCatalog.ModuleNames.Should().Equal(
            "Cadastros",
            "Compras",
            "Estoque",
            "Comercial",
            "Serviços",
            "Financeiro",
            "Administração");

        DecorFunctionalPermissionCatalog.Contexts
            .Select(context => context.ModuleName)
            .Distinct()
            .Should().Equal(DecorFunctionalPermissionCatalog.ModuleNames);
    }

    [Theory]
    [InlineData(DecorPermissions.StockLocationsView, "Estoque", "Locais de Estoque")]
    [InlineData(DecorPermissions.StockLocationsCreate, "Estoque", "Locais de Estoque")]
    [InlineData(DecorPermissions.StockLocationsEdit, "Estoque", "Locais de Estoque")]
    [InlineData(DecorPermissions.StockLocationsDelete, "Estoque", "Locais de Estoque")]
    [InlineData(DecorPermissions.StockReservationsView, "Comercial", "Pedidos")]
    [InlineData(DecorPermissions.StockReservationsCreate, "Comercial", "Pedidos")]
    [InlineData(DecorPermissions.StockReservationsRelease, "Comercial", "Pedidos")]
    [InlineData(DecorPermissions.OrderInstallmentsView, "Comercial", "Pedidos")]
    [InlineData(DecorPermissions.OrderInstallmentsCreatePlan, "Comercial", "Pedidos")]
    [InlineData(DecorPermissions.OrderInstallmentsRegisterPayment, "Comercial", "Pedidos")]
    [InlineData(DecorPermissions.OrderInstallmentsMarkOverdue, "Comercial", "Pedidos")]
    [InlineData(DecorPermissions.OrderInstallmentsCancel, "Comercial", "Pedidos")]
    [InlineData(DecorPermissions.PurchaseOrderInstallmentsView, "Compras", "Pedidos de Compra")]
    [InlineData(DecorPermissions.PurchaseOrderInstallmentsCreatePlan, "Compras", "Pedidos de Compra")]
    [InlineData(DecorPermissions.PurchaseOrderInstallmentsRegisterPayment, "Compras", "Pedidos de Compra")]
    [InlineData(DecorPermissions.PurchaseOrderInstallmentsMarkOverdue, "Compras", "Pedidos de Compra")]
    [InlineData(DecorPermissions.PurchaseOrderInstallmentsCancel, "Compras", "Pedidos de Compra")]
    [InlineData(DecorPermissions.ServiceExecutionRecordsView, "Serviços", "Agenda")]
    [InlineData(DecorPermissions.ServiceExecutionRecordsCreate, "Serviços", "Agenda")]
    [InlineData(DecorPermissions.TailorQuotationsView, "Comercial", "Orçamentos")]
    [InlineData(DecorPermissions.TailorQuotationsCreate, "Comercial", "Orçamentos")]
    [InlineData(DecorPermissions.TailorQuotationsRespond, "Comercial", "Orçamentos")]
    [InlineData(DecorPermissions.TailorQuotationsClose, "Comercial", "Orçamentos")]
    [InlineData(DecorPermissions.PartnerPriceTablesView, "Comercial", "Orçamentos")]
    [InlineData(DecorPermissions.PartnerPriceTablesCreate, "Comercial", "Orçamentos")]
    [InlineData(DecorPermissions.PartnerPriceTablesEdit, "Comercial", "Orçamentos")]
    [InlineData(DecorPermissions.PartnerPriceTablesDelete, "Comercial", "Orçamentos")]
    public void Internal_resources_remain_in_their_functional_context(
        string permissionCode,
        string expectedModule,
        string expectedContext)
    {
        var context = DecorFunctionalPermissionCatalog.Contexts
            .Single(context => context.PermissionCodes.Contains(permissionCode));

        context.ModuleName.Should().Be(expectedModule);
        context.ContextName.Should().Be(expectedContext);
    }
}