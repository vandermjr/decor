using Decor.Core.Common;

namespace Decor.Application.Tests;

public sealed class DecorPermissionPresentationCatalogTests
{
    [Theory]
    [InlineData("Products.View", "Cadastros", "Produtos", "Consultar")]
    [InlineData("Products.Create", "Cadastros", "Produtos", "Cadastrar")]
    [InlineData("Products.Edit", "Cadastros", "Produtos", "Editar")]
    [InlineData("Products.Delete", "Cadastros", "Produtos", "Excluir")]
    [InlineData("PurchaseOrders.View", "Compras", "Pedidos de Compra", "Consultar")]
    [InlineData("StockMovements.Entry", "Estoque", "Movimentações de Estoque", "Registrar entrada")]
    [InlineData("Users.View", "Configurações", "Usuários", "Consultar")]
    [InlineData("Roles.View", "Configurações", "Grupos de Permissões", "Consultar")]
    [InlineData("DatabaseMaintenance.View", "Configurações", "Administração do Sistema", "Consultar")]
    public void Describe_returns_the_portuguese_hierarchy(string permissionCode, string module, string form, string action)
    {
        var presentation = DecorPermissionPresentationCatalog.Describe(permissionCode);

        presentation.ModuleName.Should().Be(module);
        presentation.FormName.Should().Be(form);
        presentation.ActionName.Should().Be(action);
    }
}
