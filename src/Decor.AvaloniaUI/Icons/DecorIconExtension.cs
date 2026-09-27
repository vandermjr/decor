using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Decor.AvaloniaUI.Icons;

public sealed class DecorIconExtension : MarkupExtension
{
    public DecorIconId Id { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => DecorIconCatalog.Get(Id);
}
