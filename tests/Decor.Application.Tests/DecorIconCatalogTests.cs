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
            DecorIconId.Actions.Generic,
            DecorIconId.Actions.Copy,
            DecorIconId.User.Profile,
            DecorIconId.User.Preferences,
            DecorIconId.User.ChangePassword,
            DecorIconId.User.Notifications,
            DecorIconId.User.SignOut,
            DecorIconId.Common.Calendar,
            DecorIconId.Common.Clock
        ];

        Assert.Equal(expectedIds.Length, DecorIconCatalog.Ids.Count);
        Assert.All(expectedIds, id => Assert.IsAssignableFrom<Geometry>(DecorIconCatalog.Get(id)));
    }

    [Fact]
    public void New_semantic_ids_are_registered_in_their_expected_categories()
    {
        _ = AvaloniaInitialized.Value;

        (DecorIconId Id, string Category)[] newIds =
        [
            (DecorIconId.Actions.Copy, "Actions."),
            (DecorIconId.User.Profile, "User."),
            (DecorIconId.User.Preferences, "User."),
            (DecorIconId.User.ChangePassword, "User."),
            (DecorIconId.User.Notifications, "User."),
            (DecorIconId.User.SignOut, "User."),
            (DecorIconId.Common.Calendar, "Common."),
            (DecorIconId.Common.Clock, "Common.")
        ];

        Assert.All(newIds, item =>
        {
            Assert.Contains(item.Id, DecorIconCatalog.Ids);
            Assert.IsAssignableFrom<Geometry>(DecorIconCatalog.Get(item.Id));
            Assert.StartsWith(item.Category, item.Id.Value);
        });
    }

    [Fact]
    public void Unknown_semantic_id_is_rejected()
    {
        Assert.Throws<KeyNotFoundException>(() => DecorIconCatalog.Get(new DecorIconId("Actions.Unknown")));
    }
}