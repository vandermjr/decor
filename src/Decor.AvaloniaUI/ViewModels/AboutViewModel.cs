using System.Reflection;
using System.Runtime.InteropServices;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class AboutViewModel
{
    public string Version { get; } = typeof(AboutViewModel).Assembly.GetName().Version?.ToString() ?? "Não informada";
    public string Framework { get; } = RuntimeInformation.FrameworkDescription;
    public string OperatingSystem { get; } = RuntimeInformation.OSDescription;
    public string Architecture { get; } = RuntimeInformation.ProcessArchitecture.ToString();
    public string InterfaceVersion { get; } = typeof(Avalonia.Controls.Control).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "Não informada";
}