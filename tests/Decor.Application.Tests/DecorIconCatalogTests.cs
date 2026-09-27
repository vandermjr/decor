using Avalonia.Media;
using Avalonia.Skia;
using Decor.AvaloniaUI.Icons;

namespace Decor.Application.Tests;

public class DecorIconCatalogTests
{
    private static readonly Lazy<bool> AvaloniaInitialized = new(() =>
    {
        SkiaPlatform.Initialize();
        return true;
    });

    [Fact]
    public void All_initial_semantic_ids_resolve_to_vector_geometry()
    {
        _ = AvaloniaInitialized.Value;

        DecorIconId[] expectedIds =
        [
            DecorIconId.Application.Home,
            DecorIconId.Application.Settings,
            DecorIconId.Application.Help,
            DecorIconId.Modules.Cadastros,
            DecorIconId.Modules.Compras,
            DecorIconId.Modules.Estoque,
            DecorIconId.Modules.Comercial,
            DecorIconId.Modules.Servicos,
            DecorIconId.Modules.Financeiro,
            DecorIconId.Modules.Configuracoes,
            DecorIconId.Forms.Products,
            DecorIconId.Forms.Brands,
            DecorIconId.Forms.Classifications,
            DecorIconId.Forms.TermDelivery,
            DecorIconId.Forms.Users,
            DecorIconId.Forms.PermissionGroups,
            DecorIconId.Forms.DatabaseMaintenance,
            DecorIconId.Actions.View,
            DecorIconId.Actions.Create,
            DecorIconId.Actions.Edit,
            DecorIconId.Actions.Delete,
            DecorIconId.Actions.Generic
        ];

        Assert.Equal(expectedIds.Length, DecorIconCatalog.Ids.Count);
        Assert.All(expectedIds, id => Assert.IsAssignableFrom<Geometry>(DecorIconCatalog.Get(id)));
    }

    [Fact]
    public void Unknown_semantic_id_is_rejected()
    {
        Assert.Throws<KeyNotFoundException>(() => DecorIconCatalog.Get(new DecorIconId("Actions.Unknown")));
    }
}