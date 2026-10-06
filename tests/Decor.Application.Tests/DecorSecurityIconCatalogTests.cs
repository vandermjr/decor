using Avalonia.Skia;
using Decor.AvaloniaUI.Icons;

namespace Decor.Application.Tests;

public class DecorSecurityIconCatalogTests
{
    [Fact]
    public void Security_and_barcode_symbols_are_distinct_and_weighted()
    {
        SkiaPlatform.Initialize();
        DecorIconId[] ids =
        [
            DecorIconId.Common.Barcode,
            DecorIconId.Common.Status,
            DecorIconId.Common.Unlocked,
            DecorIconId.Forms.Permissions
        ];

        foreach (var weight in DecorIconCatalog.SupportedWeights)
        {
            var geometries = ids.Select(id => DecorIconCatalog.Get(id, weight)).ToArray();
            Assert.Equal(ids.Length, geometries.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.NotSame(DecorIconCatalog.Get(DecorIconId.Forms.PermissionGroups, weight), geometries[3]);
            Assert.NotSame(DecorIconCatalog.Get(DecorIconId.User.Preferences, weight), geometries[1]);
            Assert.All(geometries, geometry => Assert.True(geometry.Bounds.Width > 0 && geometry.Bounds.Height > 0));
        }

        foreach (var id in ids)
            Assert.Equal(7, DecorIconCatalog.SupportedWeights.Select(weight => DecorIconCatalog.Get(id, weight))
                .Distinct(ReferenceEqualityComparer.Instance).Count());
    }
}