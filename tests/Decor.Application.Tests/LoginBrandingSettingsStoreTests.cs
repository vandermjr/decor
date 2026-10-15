using Decor.AvaloniaUI.Services;

namespace Decor.Application.Tests;

public sealed class LoginBrandingSettingsStoreTests
{
    [Fact]
    public void SaveLogo_persists_image_and_reset_restores_default()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(root.FullName, "src", "Decor.AvaloniaUI", "Assets", "login-logo-default.png");
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "decor-login-branding-tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(temporaryDirectory, "settings.json");
        var logoDirectory = Path.Combine(temporaryDirectory, "logos");
        var store = new LoginBrandingSettingsStore(settingsPath, logoDirectory, "default-logo");

        try
        {
            Assert.Equal("default-logo", store.LogoPath);
            store.SaveLogo(sourcePath);

            Assert.Equal(Path.Combine(logoDirectory, "login-logo.png"), store.LogoPath);
            Assert.True(File.Exists(store.LogoPath));
            Assert.Equal("login-logo.png", store.LogoFileName);

            store.SaveLayout(72, "Direita", "Topo");
            store.SaveInvertLogoInLightTheme(true);
            var reloadedStore = new LoginBrandingSettingsStore(settingsPath, logoDirectory, "default-logo");
            Assert.Equal(72, reloadedStore.LogoSizePercent);
            Assert.Equal("Direita", reloadedStore.LogoHorizontalPosition);
            Assert.Equal("Topo", reloadedStore.LogoVerticalPosition);
            Assert.True(reloadedStore.InvertLogoInLightTheme);

            reloadedStore.ResetLogo();

            Assert.Equal("default-logo", reloadedStore.LogoPath);
            Assert.Equal("Logo padrão do Decor", reloadedStore.LogoFileName);
            Assert.Equal(90, reloadedStore.LogoSizePercent);
            Assert.Equal("Centro", reloadedStore.LogoHorizontalPosition);
            Assert.Equal("Centro", reloadedStore.LogoVerticalPosition);
            Assert.False(reloadedStore.InvertLogoInLightTheme);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void RemoveLogo_restores_default_image_without_resetting_layout()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "decor-login-branding-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var sourcePath = Path.Combine(FindRepositoryRoot().FullName, "src", "Decor.AvaloniaUI", "Assets", "login-logo-default.png");
        var store = new LoginBrandingSettingsStore(Path.Combine(temporaryDirectory, "settings.json"), Path.Combine(temporaryDirectory, "logos"), "default-logo");

        try
        {
            store.SaveLogo(sourcePath);
            store.SaveLayout(70, "Direita", "Topo");
            store.SaveInvertLogoInLightTheme(true);
            store.RemoveLogo();

            Assert.Equal("default-logo", store.LogoPath);
            Assert.Equal("Logo padrão do Decor", store.LogoFileName);
            Assert.Equal(70, store.LogoSizePercent);
            Assert.Equal("Direita", store.LogoHorizontalPosition);
            Assert.Equal("Topo", store.LogoVerticalPosition);
            Assert.True(store.InvertLogoInLightTheme);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void SaveLogo_rejects_files_that_are_not_supported_images()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "decor-login-branding-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var sourcePath = Path.Combine(temporaryDirectory, "invalid.png");
        File.WriteAllText(sourcePath, "not an image");
        var store = new LoginBrandingSettingsStore(
            Path.Combine(temporaryDirectory, "settings.json"),
            Path.Combine(temporaryDirectory, "logos"),
            "default-logo");

        try
        {
            Assert.Throws<InvalidDataException>(() => store.SaveLogo(sourcePath));
            Assert.Equal("default-logo", store.LogoPath);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Decor.sln")))
            root = root.Parent;
        return root ?? throw new InvalidOperationException("Decor.sln not found.");
    }
}