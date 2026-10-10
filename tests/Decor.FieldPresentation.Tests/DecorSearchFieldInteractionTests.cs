using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Decor.AvaloniaUI.Controls;

namespace Decor.FieldPresentation.Tests;

public sealed class DecorSearchFieldInteractionTests
{
    [AvaloniaFact]
    public void Text_defaults_to_two_way_and_inner_edits_preserve_binding()
    {
        var field = new DecorSearchField { Text = "piso" };
        var input = field.FindControl<TextBox>("SearchInput")!;
        Assert.Equal(BindingMode.TwoWay, DecorSearchField.TextProperty.GetMetadata(typeof(DecorSearchField)).DefaultBindingMode);
        Assert.Equal("piso", input.Text);
        input.SetCurrentValue(TextBox.TextProperty, "marca");
        Assert.Equal("marca", field.Text);
        field.Text = "referência";
        Assert.Equal("referência", input.Text);
    }

    [AvaloniaTheory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Enter_respects_can_execute_and_is_handled_once(bool canExecute, int expected)
    {
        var command = new TestCommand(canExecute);
        var field = new DecorSearchField { SearchCommand = command };
        var input = field.FindControl<TextBox>("SearchInput")!;
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = input };
        input.RaiseEvent(args);
        Assert.Equal(expected, command.Executions);
        Assert.True(args.Handled);
    }

    [AvaloniaFact]
    public void Optional_clear_and_help_update_without_recreating_control()
    {
        var field = new DecorSearchField();
        Assert.False(field.FindControl<Button>("ClearButton")!.IsVisible);
        var command = new TestCommand(true);
        field.ClearCommand = command;
        Assert.True(field.HasClearCommand);
        Assert.True(field.FindControl<Button>("ClearButton")!.IsVisible);
        Assert.Same(command, field.FindControl<Button>("ClearButton")!.Command);
        field.ClearCommand = null;
        Assert.False(field.HasClearCommand);
        field.SearchHelp = "Exemplo";
        Assert.Equal("Exemplo", field.FindControl<TextBlock>("HelpText")!.Text);
        Assert.IsType<Flyout>(field.FindControl<Button>("HelpButton")!.Flyout);
        field.SearchHelp = " ";
        Assert.False(field.HasSearchHelp);
    }

    [AvaloniaTheory]
    [InlineData("SearchInput", false)]
    [InlineData("SearchInput", true)]
    [InlineData("SearchButton", false)]
    [InlineData("SearchButton", true)]
    [InlineData("ClearButton", false)]
    [InlineData("ClearButton", true)]
    [InlineData("HelpButton", false)]
    [InlineData("HelpButton", true)]
    public void Actual_focus_on_text_or_buttons_colors_the_whole_border_black_or_white(string targetName, bool dark)
    {
        var field = new DecorSearchField
        {
            Text = "piso",
            SearchCommand = new TestCommand(true),
            ClearCommand = new TestCommand(true),
            SearchHelp = "Exemplo"
        };
        var window = new Window
        {
            Width = 360,
            Height = 100,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = field
        };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var target = field.FindControl<Control>(targetName)!;
            Assert.True(target.Focus());
            Dispatcher.UIThread.RunJobs();
            Assert.True(target.IsFocused);

            var border = field.FindControl<Border>("FieldBorder")!;
            Assert.True(border.IsKeyboardFocusWithin);
            Assert.Equal(dark ? Colors.White : Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(border.BorderBrush).Color);
            Assert.Equal(new Thickness(1), border.BorderThickness);

            var input = field.FindControl<TextBox>("SearchInput")!;
            var inputBorder = Assert.Single(input.GetVisualDescendants().OfType<Border>(), child => child.Name == "PART_BorderElement");
            Assert.Equal(new Thickness(0), inputBorder.BorderThickness);
            foreach (var buttonName in new[] { "SearchButton", "ClearButton", "HelpButton" })
            {
                var button = field.FindControl<Button>(buttonName)!;
                var presenter = Assert.Single(button.GetVisualDescendants().OfType<ContentPresenter>(), child => child.Name == "PART_ContentPresenter");
                Assert.Equal(new Thickness(0), presenter.BorderThickness);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TestCommand(bool canExecute) : ICommand
    {
        public int Executions { get; private set; }
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter) => Executions++;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}