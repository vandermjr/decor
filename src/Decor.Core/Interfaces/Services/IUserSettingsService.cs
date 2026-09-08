using Decor.Core.Configuration;

namespace Decor.Core.Interfaces.Services;
public interface IUserSettingsService
{
    // Carrega as configurações do usuário do arquivo.
    // Se o arquivo não existir, retorna as configurações padrão.
    ApplicationSettings LoadSettings();

    // Salva as configurações do usuário no arquivo.
    Task SaveSettingsAsync(ApplicationSettings settings);
}