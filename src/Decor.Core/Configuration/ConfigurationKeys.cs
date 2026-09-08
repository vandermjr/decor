namespace Decor.Core.Configuration;
public static class ConfigurationKeys
{
    // Nome da seção principal no appsettings.json
    public const string ApplicationSettingsSection = "ApplicationSettings";

    // Nomes das chaves dentro da seção
    public const string UserLanguage = "UserLanguage";
    public const string UserTheme = "UserTheme";

    // Propriedade auxiliar para obter o caminho completo da chave, evitando erros.
    public static string UserLanguagePath => $"{ApplicationSettingsSection}:{UserLanguage}";
}
