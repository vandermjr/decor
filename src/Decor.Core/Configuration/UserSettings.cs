using Decor.Core.Common;

namespace Decor.Core.Configuration;

public sealed class UserSettings
{
    public DecorThemeStyle Theme { get; init; }
    public string Language { get; init; } = string.Empty;
}