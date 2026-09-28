using System.Collections.ObjectModel;
using Avalonia.Media;

namespace Decor.AvaloniaUI.Icons;

public static class DecorIconCatalog
{
    private static readonly IReadOnlyList<string> FamilyOrder = ["Application", "Modules", "Forms", "Actions", "User", "Common"];

    private static readonly IReadOnlyDictionary<DecorIconId, Lazy<Geometry>> Geometries =
        new ReadOnlyDictionary<DecorIconId, Lazy<Geometry>>(new Dictionary<DecorIconId, Lazy<Geometry>>
        {
            [DecorIconId.Application.Home] = CreateGeometry("M 3,11 L 12,3 L 21,11 L 19,11 L 19,21 L 14,21 L 14,15 L 10,15 L 10,21 L 5,21 L 5,11 Z"),
            [DecorIconId.Application.Settings] = CreateGeometry("M 12,3 L 13.5,5.2 L 16,6.2 L 18.5,5.8 L 19.8,8.2 L 18.3,10.2 L 18.3,13 L 20,15 L 18.7,17.4 L 16.2,17 L 13.7,18 L 12.4,20.3 L 9.7,20 L 9,17.5 L 6.8,16 L 4.3,16.5 L 3,14 L 4.5,12 L 4.4,9.3 L 2.8,7.3 L 4.2,5 L 6.7,5.4 L 9,4.1 L 9.8,2 Z M 12,14.5 A 2.5,2.5 0 1 0 12,9.5 A 2.5,2.5 0 0 0 12,14.5 Z"),
            [DecorIconId.Application.Help] = CreateGeometry("M 12,3 A 9,9 0 1 0 12,21 A 9,9 0 0 0 12,3 Z M 9.4,9 A 2.7,2.7 0 1 1 14,11.3 C 12.6,12.2 12,12.8 12,14 M 12,17.2 L 12,17.3"),

            [DecorIconId.Modules.Cadastros] = CreateGeometry("M 4,4 L 20,4 L 20,20 L 4,20 Z M 8,4 L 8,20 M 12,8 L 17,8 M 12,12 L 17,12 M 12,16 L 16,16"),
            [DecorIconId.Modules.Compras] = CreateGeometry("M 3,5 L 5,5 L 7.5,16 L 18,16 L 21,8 L 6,8 M 9,19 A 1,1 0 1 0 9,21 A 1,1 0 0 0 9,19 Z M 17,19 A 1,1 0 1 0 17,21 A 1,1 0 0 0 17,19 Z"),
            [DecorIconId.Modules.Estoque] = CreateGeometry("M 3,7 L 12,3 L 21,7 L 12,11 Z M 3,7 L 3,17 L 12,21 L 12,11 M 21,7 L 21,17 L 12,21 M 7.5,5 L 16.5,9"),
            [DecorIconId.Modules.Comercial] = CreateGeometry("M 4,5 L 20,5 L 20,17 L 4,17 Z M 8,21 L 16,21 M 12,17 L 12,21 M 8,11 L 11,8 L 14,12 L 17,9"),
            [DecorIconId.Modules.Servicos] = CreateGeometry("M 14,4 A 5,5 0 0 0 8,10 L 3,15 L 3,19 L 7,19 L 12,14 A 5,5 0 0 0 18,8 L 15,11 L 12,10 L 11,7 Z M 15,17 L 19,21 M 17,15 L 21,19"),
            [DecorIconId.Modules.Financeiro] = CreateGeometry("M 4,5 L 20,5 L 20,19 L 4,19 Z M 4,9 L 20,9 M 8,13 L 11,13 M 8,16 L 14,16"),
            [DecorIconId.Modules.Configuracoes] = CreateGeometry("M 4,4 L 10,4 L 10,10 L 4,10 Z M 14,4 L 20,4 L 20,10 L 14,10 Z M 4,14 L 10,14 L 10,20 L 4,20 Z M 14,14 L 20,14 L 20,20 L 14,20 Z"),

            [DecorIconId.Forms.Products] = CreateGeometry("M 4,5 L 20,5 L 20,19 L 4,19 Z M 4,9 L 20,9 M 9,5 L 9,19 M 13,13 L 17,13 M 13,16 L 17,16"),
            [DecorIconId.Forms.Brands] = CreateGeometry("M 4,4 L 20,4 L 20,20 L 4,20 Z M 7,8 L 17,8 M 7,12 L 17,12 M 7,16 L 13,16"),
            [DecorIconId.Forms.Classifications] = CreateGeometry("M 4,5 L 11,5 L 11,10 L 4,10 Z M 13,14 L 20,14 L 20,19 L 13,19 Z M 7.5,10 L 7.5,14 L 16.5,14 M 16.5,10 L 16.5,14"),
            [DecorIconId.Forms.TermDelivery] = CreateGeometry("M 3,6 L 14,6 L 14,17 L 3,17 Z M 14,10 L 18,10 L 21,13 L 21,17 L 14,17 Z M 6,17 A 2,2 0 1 0 10,17 A 2,2 0 0 0 6,17 Z M 16,17 A 2,2 0 1 0 20,17 A 2,2 0 0 0 16,17 Z"),
            [DecorIconId.Forms.Users] = CreateGeometry("M 12,11 A 3.5,3.5 0 1 0 12,4 A 3.5,3.5 0 0 0 12,11 Z M 5,20 C 5.5,16 8,14 12,14 C 16,14 18.5,16 19,20 Z M 18,5 A 3,3 0 0 1 18,10 M 20,14 C 21,15.2 21.5,16.8 21.5,18"),
            [DecorIconId.Forms.PermissionGroups] = CreateGeometry("M 12,3 L 20,6 L 20,11 C 20,16 16.5,19.5 12,21 C 7.5,19.5 4,16 4,11 L 4,6 Z M 8,12 L 11,15 L 16,9"),
            [DecorIconId.Forms.DatabaseMaintenance] = CreateGeometry("M 12,3 C 7,3 4,4.5 4,6.5 C 4,8.5 7,10 12,10 C 17,10 20,8.5 20,6.5 C 20,4.5 17,3 12,3 Z M 4,6.5 L 4,17.5 C 4,19.5 7,21 12,21 C 17,21 20,19.5 20,17.5 L 20,6.5 M 4,12 C 4,14 7,15.5 12,15.5 C 17,15.5 20,14 20,12"),

            [DecorIconId.Actions.View] = CreateGeometry("M 2,12 C 4.5,7.5 8,5 12,5 C 16,5 19.5,7.5 22,12 C 19.5,16.5 16,19 12,19 C 8,19 4.5,16.5 2,12 Z M 12,15.5 A 3.5,3.5 0 1 0 12,8.5 A 3.5,3.5 0 0 0 12,15.5 Z"),
            [DecorIconId.Actions.Create] = CreateGeometry("M 12,3 L 12,21 M 3,12 L 21,12"),
            [DecorIconId.Actions.Edit] = CreateGeometry("M 4,16.5 L 3,21 L 7.5,20 L 19.5,8 L 16,4.5 Z M 14.5,6 L 18,9.5"),
            [DecorIconId.Actions.Delete] = CreateGeometry("M 5,7 L 19,7 L 18,21 L 6,21 Z M 3,4 L 21,4 M 9,4 L 10,2 L 14,2 L 15,4 M 10,10 L 10,17 M 14,10 L 14,17"),
            [DecorIconId.Actions.Generic] = CreateGeometry("M 5,4 L 19,4 L 19,20 L 5,20 Z M 9,9 L 15,9 M 9,12 L 15,12 M 9,15 L 13,15"),
            [DecorIconId.Actions.Copy] = CreateGeometry("M 8,7 L 20,7 L 20,21 L 8,21 Z M 4,3 L 16,3 L 16,17 L 4,17 Z"),

            [DecorIconId.User.Profile] = CreateGeometry("M 12,12 A 4,4 0 1 0 12,4 A 4,4 0 0 0 12,12 M 4,21 C 4.5,17 7.5,15 12,15 C 16.5,15 19.5,17 20,21"),
            [DecorIconId.User.Preferences] = CreateGeometry("M 12,3 L 13.5,5.2 A 7.5,7.5 0 0 1 16,6.7 L 18.6,6.2 L 20,8.6 L 18.1,10.5 A 7.5,7.5 0 0 1 18.1,13.5 L 20,15.4 L 18.6,17.8 L 16,17.3 A 7.5,7.5 0 0 1 13.5,18.8 L 12,21 L 9.2,20.2 L 8.8,17.7 A 7.5,7.5 0 0 1 6.5,15.5 L 4,16 L 2.8,13.2 L 4.7,11 A 7.5,7.5 0 0 1 4.7,9 L 2.8,6.8 L 4,4 L 6.5,4.5 A 7.5,7.5 0 0 1 8.8,2.3 L 9.2,-.2 Z M 12,15 A 3,3 0 1 0 12,9 A 3,3 0 0 0 12,15"),
            [DecorIconId.User.ChangePassword] = CreateGeometry("M 14,10 A 4,4 0 1 0 6,10 A 4,4 0 0 0 14,10 M 13,13 L 21,13 L 21,16 L 18,16 L 18,19 L 15,19 L 15,16 L 13,16 Z"),
            [DecorIconId.User.Notifications] = CreateGeometry("M 12,3 A 2,2 0 0 0 10,5 C 7.8,5.8 6,7.9 6,10.5 V 16 L 4,18 H 20 L 18,16 V 10.5 C 18,7.9 16.2,5.8 14,5 A 2,2 0 0 0 12,3 Z M 9.5,20 A 2.5,2.5 0 0 0 14.5,20 Z"),
            [DecorIconId.User.SignOut] = CreateGeometry("M 13,4 H 5 V 20 H 13 M 12,12 H 21 M 18,9 L 21,12 L 18,15"),

            [DecorIconId.Common.Calendar] = CreateGeometry("M7,2V4H17V2H19V4H20A2,2 0,0 1,22 6V20A2,2 0,0 1,20 22H4A2,2 0,0 1,2 20V6A2,2 0,0 1,4 4H5V2H7M4,9V20H20V9H4M6,11H8V13H6V11M10,11H12V13H10V11M14,11H16V13H14V11M6,15H8V17H6V15M10,15H12V17H10V15M14,15H16V17H14V15Z"),
            [DecorIconId.Common.Clock] = CreateGeometry("M12,20A8,8 0,1 0,12 4A8,8 0,0 0,12 20M12,2A10,10 0,1 1,12 22A10,10 0,0 1,12 2M12.5,7V12.25L17,14.92L16.25,16.15L11,13V7H12.5Z")
        });

    private static readonly IReadOnlyList<DecorIconId> RegisteredIds = Array.AsReadOnly(Geometries.Keys.ToArray());

    public static IReadOnlyList<DecorIconId> Ids => RegisteredIds;

    public static IReadOnlyList<DecorIconCatalogFamily> GetFamilyGroups()
    {
        var familyIds = FamilyOrder
            .Select(name => new DecorIconCatalogFamily(
                name,
                RegisteredIds
                    .Where(id => id.Value.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(id => id.Value, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .Where(group => group.Ids.Count > 0)
            .ToArray();

        return familyIds;
    }

    public static Geometry Get(DecorIconId id) => Geometries.TryGetValue(id, out var geometry)
        ? geometry.Value
        : throw new KeyNotFoundException($"No vector geometry is registered for icon '{id.Value}'.");

    private static Lazy<Geometry> CreateGeometry(string path) => new(
        () => Geometry.Parse(path),
        LazyThreadSafetyMode.ExecutionAndPublication);
}

public sealed record DecorIconCatalogFamily(string Name, IReadOnlyList<DecorIconId> Ids);