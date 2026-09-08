using Decor.Core.Common;
using Decor.Core.Helpers;

namespace Decor.Core.Configuration;
public class ApplicationSettings
{
    public string UserTheme { get; set; } = EnumHelper.EnumToString(DecorDefaults.Theme);
    public string UserLanguage { get; set; } = DecorDefaults.DefaultLanguage.Name;
}
