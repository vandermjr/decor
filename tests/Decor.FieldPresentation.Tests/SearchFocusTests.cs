using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Decor.AvaloniaUI.Controls;

namespace Decor.FieldPresentation.Tests;

public sealed class SearchFocusTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Embedded_search_actions_show_contrasting_focus_background(bool dark)
    {
        var field = new DecorSearchField
        {
            SearchHelp = "Ajuda",
            ClearCommand = new TestCommand()
        };
        var window = new Window
        {
            Content = field,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            foreach (var name in new[] { "SearchButton", "ClearButton", "HelpButton" })
            {
                var button = field.GetVisualDescendants().OfType<Button>().Single(control => control.Name == name);
                Assert.True(button.Focus());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single(control => control.Name == "PART_ContentPresenter");
                Assert.Equal(Color.Parse(dark ? "#272B30" : "#E2E5E9"), Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TestCommand : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}