using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Styling;
using Decor.AvaloniaUI.Icons;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Services;
using Moq;
using System.Reflection;
using System.Xml.Linq;

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
            DecorIconId.Application.Info,
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
            DecorIconId.Actions.Remove,
            DecorIconId.Actions.Report,
            DecorIconId.Actions.Close,
            DecorIconId.Actions.Clear,
            DecorIconId.Actions.Filter,
            DecorIconId.Actions.Copy,
            DecorIconId.Actions.Cart,
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

        Assert.Equal(56, expectedIds.Length);
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

        Assert.Equal(56, declaredIds.Length);
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
    public void Icon_catalog_labels_are_localized()
    {
        _ = AvaloniaInitialized.Value;

        var family = new IconCatalogFamilyViewModel("Actions");
        var names = DecorIconCatalog.Ids
            .Select(id => new IconCatalogItemViewModel(id, 400))
            .ToArray();

        Assert.Equal("Ações", family.Name);
        Assert.Equal("Início", Assert.Single(names, item => item.Id == DecorIconId.Application.Home).Name);
        Assert.Equal("Pesquisar", Assert.Single(names, item => item.Id == DecorIconId.Actions.Search).Name);
        Assert.Equal("Página anterior", Assert.Single(names, item => item.Id == DecorIconId.Navigation.PreviousPage).Name);
        Assert.All(names, item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
        var portugueseCognates = new HashSet<string>(StringComparer.Ordinal)
        {
            "Cadastros", "Compras", "Estoque", "Comercial", "Financeiro"
        };
        Assert.All(names, item =>
        {
            var semanticName = item.Id.Value[(item.Id.Value.LastIndexOf('.') + 1)..];
            Assert.True(!string.Equals(semanticName, item.Name, StringComparison.Ordinal)
                || portugueseCognates.Contains(item.Name));
        });
    }

    [Fact]
    public void Icon_catalog_uses_the_global_semantic_default_color()
    {
        _ = AvaloniaInitialized.Value;

        var item = new IconCatalogItemViewModel(DecorIconId.Actions.Save, 400);

        Assert.Equal(DecorIconColors.GetDefaultColor(item.Id), ((SolidColorBrush)item.ColorPreviewBrush).Color);
    }

    [Fact]
    public async Task Updating_icon_color_does_not_block_while_persistence_is_pending()
    {
        _ = AvaloniaInitialized.Value;
        var pendingSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var appearanceService = new Mock<IIconAppearanceService>();
        appearanceService.SetupGet(service => service.CurrentAppearance).Returns(IconAppearance.Default);
        appearanceService.Setup(service => service.SetIconColorOverrideAsync(
                DecorIconId.Actions.Save.Value,
                "#ef4444",
                It.IsAny<CancellationToken>()))
            .Returns(pendingSave.Task);
        var item = new IconCatalogItemViewModel(DecorIconId.Actions.Save, 400, appearanceService.Object);

        var update = item.SetColorAsync("#ef4444");

        Assert.False(update.IsCompleted);
        pendingSave.SetResult();
        await update;
    }

    [Fact]
    public void Unmapped_icon_default_contrasts_with_the_active_theme()
    {
        Assert.Equal(Colors.Black, DecorIconColors.GetDefaultColor(DecorIconId.Application.Home, ThemeVariant.Light));
        Assert.Equal(Colors.White, DecorIconColors.GetDefaultColor(DecorIconId.Application.Home, ThemeVariant.Dark));
    }

    [Fact]
    public void Previous_page_icon_uses_the_same_default_color_as_next_page()
    {
        Assert.Equal(DecorIconColors.GetDefaultColor(DecorIconId.Navigation.NextPage, ThemeVariant.Light),
            DecorIconColors.GetDefaultColor(DecorIconId.Navigation.PreviousPage, ThemeVariant.Light));
        Assert.Equal(DecorIconColors.GetDefaultColor(DecorIconId.Navigation.NextPage, ThemeVariant.Dark),
            DecorIconColors.GetDefaultColor(DecorIconId.Navigation.PreviousPage, ThemeVariant.Dark));
    }

    [Fact]
    public void Login_logo_directional_commands_step_through_three_positions_and_disable_at_edges()
    {
        _ = AvaloniaInitialized.Value;
        var appearance = new Mock<IIconAppearanceService>();
        appearance.SetupGet(service => service.CurrentAppearance).Returns(IconAppearance.Default);
        var theme = new Mock<IThemeService>();
        theme.SetupGet(service => service.CurrentTheme).Returns(DecorThemeStyle.Light);
        var branding = new Mock<ILoginBrandingSettingsStore>();
        branding.SetupGet(store => store.LogoPath).Returns("missing-logo.png");
        branding.SetupGet(store => store.LogoFileName).Returns("Logo padrão do Decor");
        branding.SetupGet(store => store.LogoSizePercent).Returns(90);
        branding.SetupGet(store => store.LogoHorizontalPosition).Returns("Centro");
        branding.SetupGet(store => store.LogoVerticalPosition).Returns("Centro");

        var viewModel = new IconCatalogViewModel(appearance.Object, theme.Object, loginBrandingSettingsStore: branding.Object);

        viewModel.MoveLoginLogoRightCommand.CanExecute(null).Should().BeTrue();
        viewModel.MoveLoginLogoRightCommand.Execute(null);
        viewModel.LoginLogoHorizontalPosition.Should().Be("Direita");
        viewModel.LoginLogoHorizontalAlignment.Should().Be(Avalonia.Layout.HorizontalAlignment.Right);
        viewModel.MoveLoginLogoRightCommand.CanExecute(null).Should().BeFalse();
        viewModel.MoveLoginLogoLeftCommand.Execute(null);
        viewModel.LoginLogoHorizontalPosition.Should().Be("Centro");
        viewModel.MoveLoginLogoLeftCommand.Execute(null);
        viewModel.LoginLogoHorizontalPosition.Should().Be("Esquerda");
        viewModel.LoginLogoHorizontalAlignment.Should().Be(Avalonia.Layout.HorizontalAlignment.Left);
        viewModel.MoveLoginLogoLeftCommand.CanExecute(null).Should().BeFalse();

        viewModel.MoveLoginLogoUpCommand.Execute(null);
        viewModel.LoginLogoVerticalPosition.Should().Be("Topo");
        viewModel.LoginLogoVerticalAlignment.Should().Be(Avalonia.Layout.VerticalAlignment.Top);
        viewModel.MoveLoginLogoUpCommand.CanExecute(null).Should().BeFalse();
        viewModel.MoveLoginLogoDownCommand.Execute(null);
        viewModel.LoginLogoVerticalPosition.Should().Be("Centro");
        viewModel.MoveLoginLogoDownCommand.Execute(null);
        viewModel.LoginLogoVerticalPosition.Should().Be("Base");
        viewModel.LoginLogoVerticalAlignment.Should().Be(Avalonia.Layout.VerticalAlignment.Bottom);
        viewModel.MoveLoginLogoDownCommand.CanExecute(null).Should().BeFalse();

        branding.Verify(store => store.SaveLayout(90, It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(6));
    }

    [Fact]
    public void Changing_system_options_persists_them_immediately()
    {
        _ = AvaloniaInitialized.Value;
        var appearance = new Mock<IIconAppearanceService>();
        appearance.SetupGet(service => service.CurrentAppearance).Returns(IconAppearance.Default);
        var theme = new Mock<IThemeService>();
        var currentTheme = DecorThemeStyle.Light;
        theme.SetupGet(service => service.CurrentTheme).Returns(() => currentTheme);
        theme.Setup(service => service.SetThemeAsync(It.IsAny<DecorThemeStyle>()))
            .Returns((DecorThemeStyle selected) =>
            {
                currentTheme = selected;
                return Task.CompletedTask;
            });
        var stock = new Mock<IStockSettingsService>();
        stock.Setup(service => service.SetAllowNegativeStockAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var backupSettings = new Mock<IBackupNotificationSettingsStore>();
        backupSettings.Setup(store => store.Save(It.IsAny<BackupNotificationSettings>()));

        var viewModel = new IconCatalogViewModel(appearance.Object, theme.Object, stock.Object, backupSettings.Object);

        viewModel.SelectedThemeOption = "Escuro";
        viewModel.IconWeight = 500;
        viewModel.AllowNegativeStock = true;
        viewModel.NotifyBeforeScheduledBackup = false;

        theme.Verify(service => service.SetThemeAsync(DecorThemeStyle.Dark), Times.Once);
        appearance.Verify(service => service.SetAppearanceAsync(500, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        stock.Verify(service => service.SetAllowNegativeStockAsync(true, It.IsAny<CancellationToken>()), Times.Once);
        backupSettings.Verify(store => store.Save(It.Is<BackupNotificationSettings>(settings => !settings.IsEnabled)), Times.Once);
    }

    [Fact]
    public async Task Initializing_system_options_does_not_resave_loaded_settings()
    {
        _ = AvaloniaInitialized.Value;
        var appearance = new Mock<IIconAppearanceService>();
        appearance.SetupGet(service => service.CurrentAppearance).Returns(IconAppearance.Default);
        var theme = new Mock<IThemeService>();
        theme.SetupGet(service => service.CurrentTheme).Returns(DecorThemeStyle.Light);
        var stock = new Mock<IStockSettingsService>();
        stock.Setup(service => service.GetAllowNegativeStockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var backupSettings = new Mock<IBackupNotificationSettingsStore>();
        backupSettings.Setup(store => store.Load()).Returns(new BackupNotificationSettings(false, 30));

        var viewModel = new IconCatalogViewModel(appearance.Object, theme.Object, stock.Object, backupSettings.Object);

        await viewModel.InitializeAsync();

        viewModel.AllowNegativeStock.Should().BeTrue();
        viewModel.NotifyBeforeScheduledBackup.Should().BeFalse();
        viewModel.BackupNotificationLeadTimeMinutes.Should().Be(30);
        stock.Verify(service => service.SetAllowNegativeStockAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        backupSettings.Verify(store => store.Save(It.IsAny<BackupNotificationSettings>()), Times.Never);
    }

    [Fact]
    public void Icon_catalog_content_reserves_scrollbar_space_and_stretches_each_family()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        Assert.NotNull(root);

        var document = XDocument.Load(Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Views", "IconCatalogView.axaml"));
        var inset = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("Padding") == "0,0,16,0");
        Assert.Equal("Stretch", (string?)inset.Attribute("HorizontalAlignment"));
        var familyItems = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ItemsControl"
            && (string?)element.Attribute("ItemsSource") == "{Binding FamilyGroups}");
        Assert.Equal("Stretch", (string?)familyItems.Attribute("HorizontalAlignment"));
        var expander = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.Equal("Stretch", (string?)expander.Attribute("HorizontalAlignment"));
        Assert.Equal("Stretch", (string?)expander.Attribute("HorizontalContentAlignment"));
        Assert.Equal("0,0,0,18", (string?)expander.Attribute("Margin"));
    }

    [Fact]
    public void Physical_symbols_are_shared_by_the_expected_semantic_ids()
    {
        _ = AvaloniaInitialized.Value;

        var geometries = DecorIconCatalog.Ids.Select(id => DecorIconCatalog.Get(id)).ToArray();

        Assert.Equal(50, geometries.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.NotSame(DecorIconCatalog.Get(DecorIconId.Application.Help), DecorIconCatalog.Get(DecorIconId.Application.Info));
        Assert.NotSame(DecorIconCatalog.Get(DecorIconId.Actions.Filter), DecorIconCatalog.Get(DecorIconId.User.Preferences));
        Assert.Same(DecorIconCatalog.Get(DecorIconId.Application.Settings), DecorIconCatalog.Get(DecorIconId.Modules.Configuracoes));
        Assert.Same(DecorIconCatalog.Get(DecorIconId.Modules.Estoque), DecorIconCatalog.Get(DecorIconId.Forms.Products));
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
