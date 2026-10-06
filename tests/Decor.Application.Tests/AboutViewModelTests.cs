using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;
using FluentAssertions;

namespace Decor.Application.Tests;

public sealed class AboutViewModelTests
{
    [Fact]
    public void Metadata_IsNonempty()
    {
        var viewModel = new AboutViewModel();

        viewModel.Version.Should().NotBeNullOrWhiteSpace();
        viewModel.Framework.Should().NotBeNullOrWhiteSpace();
        viewModel.OperatingSystem.Should().NotBeNullOrWhiteSpace();
        viewModel.Architecture.Should().NotBeNullOrWhiteSpace();
        viewModel.InterfaceVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Metadata_MatchesLoadedAssembliesAndCurrentPlatform()
    {
        var viewModel = new AboutViewModel();
        var applicationVersion = typeof(AboutViewModel).Assembly.GetName().Version;
        var interfaceVersion = typeof(Control).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        applicationVersion.Should().NotBeNull();
        interfaceVersion.Should().NotBeNull();

        viewModel.Version.Should().Be(applicationVersion!.ToString());
        viewModel.Framework.Should().Be(RuntimeInformation.FrameworkDescription);
        viewModel.OperatingSystem.Should().Be(RuntimeInformation.OSDescription);
        viewModel.Architecture.Should().Be(RuntimeInformation.ProcessArchitecture.ToString());
        viewModel.InterfaceVersion.Should().Be(interfaceVersion!.InformationalVersion.Split('+')[0]);
        viewModel.InterfaceVersion.Should().NotContain("+");
    }
}