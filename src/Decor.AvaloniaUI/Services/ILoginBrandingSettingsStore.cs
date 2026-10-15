namespace Decor.AvaloniaUI.Services;

public interface ILoginBrandingSettingsStore
{
    string LogoPath { get; }
    string LogoFileName { get; }
    int LogoSizePercent { get; }
    string LogoHorizontalPosition { get; }
    string LogoVerticalPosition { get; }
    bool InvertLogoInLightTheme { get; }
    void SaveLogo(string sourcePath);
    void RemoveLogo();
    void SaveInvertLogoInLightTheme(bool invert);
    void SaveLayout(int sizePercent, string horizontalPosition, string verticalPosition);
    void ResetLogo();
}