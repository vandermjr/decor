using System.Text.Json;
using Decor.Core.Configuration;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Decor.Infrastructure.Services;
public class JsonUserSettingsService : IUserSettingsService
{
    private readonly string _filePath;
    private readonly ApplicationSettings _defaultSettings;

    // >>> PASSO 1: Criamos um campo estático para armazenar as opções <<<
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public JsonUserSettingsService(IOptions<ApplicationSettings> defaultSettingsOptions)
    {
        // Pegamos as configurações padrão do appsettings.json via IOptions
        _defaultSettings = defaultSettingsOptions.Value;

        // Define um caminho seguro na pasta de dados do usuário para salvar as configurações
        // Ex: C:\Users\SeuUsuario\AppData\Roaming\Decor\user_settings.json
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolderPath = Path.Combine(appDataPath, "Decor");
        Directory.CreateDirectory(appFolderPath); // Garante que a pasta exista
        _filePath = Path.Combine(appFolderPath, "user_settings.json");
    }

    public ApplicationSettings LoadSettings()
    {
        if (!File.Exists(_filePath))
        {
            // Se o usuário ainda não salvou nenhuma preferência, retorna os padrões do appsettings.json
            return _defaultSettings;
        }

        var json = File.ReadAllText(_filePath);
        // Podemos usar as mesmas opções para desserializar, se quisermos no futuro.
        return JsonSerializer.Deserialize<ApplicationSettings>(json, _serializerOptions) ?? _defaultSettings;
    }

    public async Task SaveSettingsAsync(ApplicationSettings settings)
    {
        // >>> PASSO 2: Usamos a instância em cache em vez de criar uma nova <<<
        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
