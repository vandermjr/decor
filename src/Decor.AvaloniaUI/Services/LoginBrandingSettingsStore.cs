using System.Text.Json;

namespace Decor.AvaloniaUI.Services;

public sealed class LoginBrandingSettingsStore : ILoginBrandingSettingsStore
{
    private const long MaximumLogoSizeBytes = 10 * 1024 * 1024;
    private const int DefaultLogoSizePercent = 90;
    private const string DefaultLogoPosition = "Centro";
    private static readonly string[] HorizontalPositions = ["Esquerda", "Centro", "Direita"];
    private static readonly string[] VerticalPositions = ["Topo", "Centro", "Base"];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly string _logoDirectory;
    private readonly string _defaultLogoPath;

    public LoginBrandingSettingsStore()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Decor", "login-branding-settings.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Decor", "LoginBranding"),
            "avares://Decor.AvaloniaUI/Assets/login-logo-default.png")
    {
    }

    public LoginBrandingSettingsStore(string settingsPath, string logoDirectory, string defaultLogoPath)
    {
        _settingsPath = settingsPath;
        _logoDirectory = logoDirectory;
        _defaultLogoPath = defaultLogoPath;
    }

    public string LogoPath => GetSelectedLogoPath() ?? _defaultLogoPath;
    public string LogoFileName => GetSelectedLogoPath() is { } path ? Path.GetFileName(path) : "Logo padrão do Decor";
    public int LogoSizePercent => Math.Clamp(ReadSettings().SizePercent, 25, 100);
    public string LogoHorizontalPosition => HorizontalPositions.Contains(ReadSettings().HorizontalPosition)
        ? ReadSettings().HorizontalPosition
        : DefaultLogoPosition;
    public string LogoVerticalPosition => VerticalPositions.Contains(ReadSettings().VerticalPosition)
        ? ReadSettings().VerticalPosition
        : DefaultLogoPosition;
    public bool InvertLogoInLightTheme => ReadSettings().InvertLogoInLightTheme;

    public void SaveLogo(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg"))
            throw new InvalidDataException("Selecione uma imagem PNG ou JPEG.");

        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists || sourceInfo.Length is <= 0 or > MaximumLogoSizeBytes || !HasSupportedSignature(sourcePath, extension))
            throw new InvalidDataException("O arquivo selecionado não é uma imagem PNG ou JPEG válida.");

        Directory.CreateDirectory(_logoDirectory);
        var fileName = "login-logo" + extension;
        var targetPath = Path.Combine(_logoDirectory, fileName);
        var temporaryImagePath = targetPath + ".tmp";
        File.Copy(sourcePath, temporaryImagePath, overwrite: true);
        File.Move(temporaryImagePath, targetPath, overwrite: true);

        WriteSettings(ReadSettings() with { LogoFileName = fileName });
    }

    public void SaveLayout(int sizePercent, string horizontalPosition, string verticalPosition)
    {
        if (sizePercent is < 25 or > 100)
            throw new ArgumentOutOfRangeException(nameof(sizePercent));
        if (!HorizontalPositions.Contains(horizontalPosition))
            throw new ArgumentOutOfRangeException(nameof(horizontalPosition));
        if (!VerticalPositions.Contains(verticalPosition))
            throw new ArgumentOutOfRangeException(nameof(verticalPosition));

        WriteSettings(ReadSettings() with
        {
            SizePercent = sizePercent,
            HorizontalPosition = horizontalPosition,
            VerticalPosition = verticalPosition
        });
    }

    public void RemoveLogo() => WriteSettings(ReadSettings() with { LogoFileName = null });

    public void SaveInvertLogoInLightTheme(bool invert) =>
        WriteSettings(ReadSettings() with { InvertLogoInLightTheme = invert });

    public void ResetLogo() => WriteSettings(new LoginBrandingSettings());

    private string? GetSelectedLogoPath()
    {
        try
        {
            var settings = ReadSettings();
            if (settings?.LogoFileName is not { } fileName
                || fileName != Path.GetFileName(fileName)
                || Path.GetExtension(fileName).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg"))
                return null;

            var path = Path.Combine(_logoDirectory, fileName);
            return File.Exists(path) ? path : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private LoginBrandingSettings ReadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new LoginBrandingSettings();

            return JsonSerializer.Deserialize<LoginBrandingSettings>(File.ReadAllText(_settingsPath), JsonOptions)
                ?? new LoginBrandingSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new LoginBrandingSettings();
        }
    }

    private void WriteSettings(LoginBrandingSettings settings)
    {
        var settingsDirectory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("O caminho das configurações não possui diretório.");
        Directory.CreateDirectory(settingsDirectory);
        var temporarySettingsPath = _settingsPath + ".tmp";
        File.WriteAllText(temporarySettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporarySettingsPath, _settingsPath, overwrite: true);
    }

    private static bool HasSupportedSignature(string path, string extension)
    {
        Span<byte> header = stackalloc byte[8];
        using var stream = File.OpenRead(path);
        var bytesRead = stream.Read(header);
        return extension == ".png"
            ? bytesRead >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : bytesRead >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
    }

    private sealed record LoginBrandingSettings
    {
        public string? LogoFileName { get; init; }
        public int SizePercent { get; init; } = DefaultLogoSizePercent;
        public string HorizontalPosition { get; init; } = DefaultLogoPosition;
        public string VerticalPosition { get; init; } = DefaultLogoPosition;
        public bool InvertLogoInLightTheme { get; init; }
    }
}