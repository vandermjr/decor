using Decor.Core.Common;

namespace Decor.Core.Interfaces.Services;
public interface IThemeService
{
    /// <summary>
    /// O tema atual da aplicação.
    /// </summary>
    DecorThemeStyle CurrentTheme { get; }

    /// <summary>
    /// Evento disparado sempre que o tema é alterado.
    /// </summary>
    event Action<DecorThemeStyle>? ThemeChanged;

    /// <summary>
    /// Define um novo tema para a aplicação, o que irá disparar o evento ThemeChanged.
    /// </summary>
    Task SetThemeAsync(DecorThemeStyle newTheme);
}
