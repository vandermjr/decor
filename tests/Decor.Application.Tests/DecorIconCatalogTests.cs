using Avalonia.Media;
using Avalonia.Skia;
using Decor.AvaloniaUI.Icons;
using System.Reflection;

namespace Decor.Application.Tests;

public class DecorIconCatalogTests
{
    private static readonly Lazy<bool> AvaloniaInitialized = new(() =>
    {
        SkiaPlatform.Initialize();
        return true;
    });

    [Fact]
    public void All_semantic_ids_resolve_to_vector_geometry()
    {
        _ = AvaloniaInitialized.Value;

        DecorIconId[] expectedIds =
        [
            DecorIconId.Application.Home,
            DecorIconId.Application.Settings,
            DecorIconId.Application.Help,
            DecorIconId.Application.ThemeToggle,
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
            DecorIconId.Forms.Permissions,
            DecorIconId.Forms.DatabaseMaintenance,
            DecorIconId.Actions.Search,
            DecorIconId.Actions.View,
            DecorIconId.Actions.Create,
            DecorIconId.Actions.Edit,
            DecorIconId.Actions.Delete,
            DecorIconId.Actions.Save,
            DecorIconId.Actions.Cancel,
            DecorIconId.Actions.Add,
            DecorIconId.Actions.Remove,
            DecorIconId.Actions.Report,
            DecorIconId.Actions.Close,
            DecorIconId.Actions.Clear,
            DecorIconId.Actions.Copy,
            DecorIconId.User.Profile,
            DecorIconId.User.Preferences,
            DecorIconId.User.ChangePassword,
            DecorIconId.User.Notifications,
            DecorIconId.User.SignIn,
            DecorIconId.User.SignOut,
            DecorIconId.Common.Calendar,
            DecorIconId.Common.Clock,
            DecorIconId.Common.Folder,
            DecorIconId.Common.Database,
            DecorIconId.Common.Backup,
            DecorIconId.Common.Code,
            DecorIconId.Common.Barcode,
            DecorIconId.Common.Status,
            DecorIconId.Common.Unlocked,
            DecorIconId.Navigation.FirstPage,
            DecorIconId.Navigation.PreviousPage,
            DecorIconId.Navigation.NextPage,
            DecorIconId.Navigation.LastPage,
            DecorIconId.Navigation.Dropdown,
            DecorIconId.Navigation.SortAscending,
            DecorIconId.Navigation.SortDescending
        ];

        Assert.Equal(54, expectedIds.Length);
        Assert.Equal(expectedIds.OrderBy(id => id.Value), DecorIconCatalog.Ids.OrderBy(id => id.Value));
        Assert.All(expectedIds, id => Assert.IsAssignableFrom<Geometry>(DecorIconCatalog.Get(id)));
    }

    [Fact]
    public void Declared_ids_exactly_match_registered_ids_and_generic_is_absent()
    {
        _ = AvaloniaInitialized.Value;

        var declaredIds = typeof(DecorIconId)
            .GetNestedTypes(BindingFlags.Public)
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            .Where(property => property.PropertyType == typeof(DecorIconId))
            .Select(property => (DecorIconId)property.GetValue(null)!)
            .OrderBy(id => id.Value)
            .ToArray();

        Assert.Equal(54, declaredIds.Length);
        Assert.Equal(declaredIds, DecorIconCatalog.Ids.OrderBy(id => id.Value));
        Assert.Null(typeof(DecorIconId.Actions).GetProperty("Generic"));
        Assert.DoesNotContain(DecorIconCatalog.Ids, id => id.Value == "Actions." + "Generic");
    }

    [Fact]
    public void Catalog_groups_expose_all_registered_families_and_ids()
    {
        _ = AvaloniaInitialized.Value;

        var groups = DecorIconCatalog.GetFamilyGroups();

        Assert.Equal(["Application", "Modules", "Forms", "Actions", "User", "Common", "Navigation"], groups.Select(group => group.Name).ToArray());
        Assert.Equal(DecorIconCatalog.Ids.Count, groups.Sum(group => group.Ids.Count));
        Assert.All(groups, group => Assert.NotEmpty(group.Ids));
        Assert.All(DecorIconCatalog.Ids, id => Assert.Contains(id, groups.SelectMany(group => group.Ids)));
    }

    [Fact]
    public void Physical_symbols_are_shared_by_the_expected_semantic_ids()
    {
        _ = AvaloniaInitialized.Value;

        var geometries = DecorIconCatalog.Ids.Select(id => DecorIconCatalog.Get(id)).ToArray();

        Assert.Equal(48, geometries.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Same(DecorIconCatalog.Get(DecorIconId.Application.Settings), DecorIconCatalog.Get(DecorIconId.Modules.Configuracoes));
        Assert.Same(DecorIconCatalog.Get(DecorIconId.Modules.Estoque), DecorIconCatalog.Get(DecorIconId.Forms.Products));
        Assert.Same(DecorIconCatalog.Get(DecorIconId.Actions.Create), DecorIconCatalog.Get(DecorIconId.Actions.Add));
        Assert.Same(DecorIconCatalog.Get(DecorIconId.Forms.DatabaseMaintenance), DecorIconCatalog.Get(DecorIconId.Common.Database));
    }

    [Fact]
    public void Unknown_semantic_id_is_rejected()
    {
        Assert.Throws<KeyNotFoundException>(() => DecorIconCatalog.Get(new DecorIconId("Actions.Unknown")));
    }

    [Fact]
    public void Supported_weights_resolve_every_semantic_id_to_non_empty_geometry()
    {
        _ = AvaloniaInitialized.Value;

        Assert.Equal([100, 200, 300, 400, 500, 600, 700], DecorIconCatalog.SupportedWeights);

        foreach (var weight in DecorIconCatalog.SupportedWeights)
        {
            foreach (var id in DecorIconCatalog.Ids)
            {
                var geometry = DecorIconCatalog.Get(id, weight);
                Assert.NotNull(geometry);
                Assert.True(geometry.Bounds.Width > 0);
                Assert.True(geometry.Bounds.Height > 0);
            }
        }
    }

    [Fact]
    public void Original_get_uses_the_default_weight_and_invalid_weight_is_rejected()
    {
        _ = AvaloniaInitialized.Value;

        Assert.Same(DecorIconCatalog.Get(DecorIconId.Application.Home), DecorIconCatalog.Get(DecorIconId.Application.Home, 400));
        Assert.Throws<ArgumentOutOfRangeException>(() => DecorIconCatalog.Get(DecorIconId.Application.Home, 450));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(600)]
    [InlineData(700)]
    public void First_and_last_page_symbols_are_mirrored_and_have_terminal_bars(int weight)
    {
        _ = AvaloniaInitialized.Value;
        var first = DecorIconCatalog.Get(DecorIconId.Navigation.FirstPage, weight);
        var last = DecorIconCatalog.Get(DecorIconId.Navigation.LastPage, weight);
        Assert.Equal(first.Bounds.Width, last.Bounds.Width, 2);
        Assert.Equal(first.Bounds.Height, last.Bounds.Height, 2);
        Assert.True(first.FillContains(new Avalonia.Point(280, -480)));
        Assert.True(last.FillContains(new Avalonia.Point(680, -480)));
        Assert.False(first.FillContains(new Avalonia.Point(680, -480)));
        Assert.False(last.FillContains(new Avalonia.Point(280, -480)));
    }

    [Fact]
    public void Sign_in_uses_the_catalog_weighted_filled_sign_out_geometry_mirrored()
    {
        _ = AvaloniaInitialized.Value;

        var light = DecorIconCatalog.Get(DecorIconId.User.SignIn, 100);
        var regular = DecorIconCatalog.Get(DecorIconId.User.SignIn, 400);
        var bold = DecorIconCatalog.Get(DecorIconId.User.SignIn, 700);

        Assert.IsType<GeometryGroup>(regular);
        Assert.NotSame(light, regular);
        Assert.NotSame(regular, bold);
        Assert.Equal(DecorIconCatalog.Get(DecorIconId.User.SignOut, 400).Bounds.Width, regular.Bounds.Width, 3);
        Assert.Equal(DecorIconCatalog.Get(DecorIconId.User.SignOut, 400).Bounds.Height, regular.Bounds.Height, 3);
    }
}
