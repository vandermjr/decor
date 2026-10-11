using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.Application.Tests;

public sealed class ServicesNavigationTests
{
    [Fact]
    public void DeniedView_DoesNotResolveCatalog()
    {
        var navigation = new Mock<INavigationService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        var viewModel = new MainViewModel(navigation.Object, new Mock<IThemeService>().Object, authorization.Object, new Mock<IAuthenticatedUserContext>().Object);
        Assert.False(viewModel.CanViewServices);
        Assert.False(viewModel.ShowServicesCommand.CanExecute(null));
        viewModel.ShowServicesCommand.Execute(null);
        Assert.Empty(viewModel.OpenDocuments);
        Assert.Empty(navigation.Invocations);
    }
}