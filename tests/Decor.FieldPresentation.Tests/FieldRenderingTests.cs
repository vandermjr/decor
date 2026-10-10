using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Decor.Application.Services;
using Decor.AvaloniaUI;
using Decor.AvaloniaUI.Controls;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

[assembly: AvaloniaTestApplication(typeof(Decor.FieldPresentation.Tests.FieldTestApplication))]

namespace Decor.FieldPresentation.Tests;

public static class FieldTestApplication
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var services = new ServiceCollection()
            .AddSingleton<IIconAppearanceService>(new IconAppearanceService(new TestSettings()))
            .BuildServiceProvider();
        return AppBuilder.Configure(() => new App(services))
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    private sealed class TestSettings : ISystemSettingsRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task SetAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

public sealed class FieldRenderingTests
{
    [AvaloniaTheory]
    [InlineData("TextBox", false)]
    [InlineData("TextBox", true)]
    [InlineData("ComboBox", false)]
    [InlineData("ComboBox", true)]
    [InlineData("NumericUpDown", false)]
    [InlineData("NumericUpDown", true)]
    [InlineData("DatePicker", false)]
    [InlineData("DatePicker", true)]
    public void Actual_fluent_focus_is_neutral_and_one_pixel(string kind, bool dark)
    {
        var field = CreateField(kind);
        var window = Show(field, dark);
        try
        {
            FocusInput(field);
            var (brush, thickness) = FieldBorder(field);
            Assert.Equal(dark ? Colors.White : Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color);
            Assert.Equal(new Thickness(1), thickness);

            if (field is NumericUpDown)
            {
                var inner = Part<TextBox>(field, "PART_TextBox");
                Assert.Equal(new Thickness(0), FieldBorder(inner).Thickness);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("TextBox")]
    [InlineData("ComboBox")]
    [InlineData("NumericUpDown")]
    [InlineData("DatePicker")]
    public void Explicit_borderless_fields_remain_borderless_when_focused(string kind)
    {
        var field = CreateField(kind);
        field.BorderThickness = new Thickness(0);
        var window = Show(field, false);
        try
        {
            FocusInput(field);
            Assert.Equal(new Thickness(0), FieldBorder(field).Thickness);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Required_adorner_is_inside_noninteractive_and_removed_on_toggle_or_detach()
    {
        var field = new TextBox { Text = "Nome" };
        DecorRequiredField.SetIsRequired(field, true);
        var window = Show(field, false);
        try
        {
            var layer = Assert.IsType<AdornerLayer>(AdornerLayer.GetAdornerLayer(field));
            var marker = Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field);
            Assert.False(marker.IsHitTestVisible);
            Assert.False(marker.Focusable);
            Assert.Equal(field.Bounds.Size, marker.Bounds.Size);
            Assert.True(AdornerLayer.GetIsClipEnabled(marker));
            AssertRedSquare(window, field);

            var template = field.Template;
            field.Template = null;
            field.ApplyTemplate();
            field.Template = template;
            field.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(marker, Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field));

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(marker, Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field));
            AssertRedSquare(window, field);

            DecorRequiredField.SetIsRequired(field, false);
            Assert.DoesNotContain(marker, layer.Children);
            DecorRequiredField.SetIsRequired(field, true);
            Dispatcher.UIThread.RunJobs();
            var replacement = Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field);
            window.Content = null;
            Assert.DoesNotContain(replacement, layer.Children);
            window.Content = field;
            Dispatcher.UIThread.RunJobs();
            Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Optional_host_renders_the_marker_when_no_adorner_layer_exists()
    {
        var field = new Border { Width = 120, Height = 32, Background = Brushes.White };
        var host = new DecorRequiredFieldHost { Children = { field } };
        DecorRequiredField.SetIsRequired(field, true);
        var window = new Window
        {
            Width = 200,
            Height = 100,
            Template = new FuncControlTemplate<Window>((owner, scope) => host)
        };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            Assert.Null(AdornerLayer.GetAdornerLayer(field));
            Assert.Equal(2, host.Children.Count);
            Assert.Equal(new Size(120, 32), host.DesiredSize);
            AssertRedSquare(window, field);

            DecorRequiredField.SetIsRequired(field, false);
            Assert.Single(host.Children);
            DecorRequiredField.SetIsRequired(field, true);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, host.Children.Count);
            host.Children.Remove(field);
            Assert.Empty(host.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Required_marker_follows_ancestor_visibility_when_switching_cached_documents(bool dark)
    {
        var field = new Border { Width = 120, Height = 32, Background = Brushes.White };
        var productView = new Border { Child = new Grid { Children = { field } } };
        var brandsView = new Border { Background = Brushes.White, IsVisible = false };
        var documents = new Grid { Children = { productView, brandsView } };
        DecorRequiredField.SetIsRequired(field, true);
        var window = Show(documents, dark);
        try
        {
            var layer = Assert.IsType<AdornerLayer>(AdornerLayer.GetAdornerLayer(field));
            var marker = Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field);
            AssertRedSquare(window, field);

            for (var transition = 0; transition < 3; transition++)
            {
                productView.IsVisible = false;
                brandsView.IsVisible = true;
                Dispatcher.UIThread.RunJobs();
                Assert.True(field.IsVisible);
                Assert.False(field.IsEffectivelyVisible);
                Assert.True(field.IsAttachedToVisualTree());
                AssertRedSquare(window, field, expected: false);
                Assert.False(marker.IsEffectivelyVisible);

                brandsView.IsVisible = false;
                productView.IsVisible = true;
                Dispatcher.UIThread.RunJobs();
                AssertRedSquare(window, field);
                Assert.True(marker.IsEffectivelyVisible);
                Assert.Same(marker, Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field));
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Required_marker_is_removed_and_restored_when_document_content_is_replaced()
    {
        var field = new Border { Width = 120, Height = 32, Background = Brushes.White };
        var productView = new Border { Child = field };
        var brandsView = new Border { Width = 120, Height = 32, Background = Brushes.White };
        var documents = new ContentControl { Content = productView };
        DecorRequiredField.SetIsRequired(field, true);
        var window = Show(documents, false);
        try
        {
            var layer = Assert.IsType<AdornerLayer>(AdornerLayer.GetAdornerLayer(field));
            var marker = Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field);
            AssertRedSquare(window, field);

            documents.Content = brandsView;
            Dispatcher.UIThread.RunJobs();
            Assert.False(field.IsAttachedToVisualTree());
            Assert.DoesNotContain(marker, layer.Children);
            AssertRedSquare(window, brandsView, expected: false);

            documents.Content = productView;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(marker, Assert.Single(layer.Children, child => AdornerLayer.GetAdornedElement(child) == field));
            AssertRedSquare(window, field);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertRedSquare(Window window, Control field, bool expected = true)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var pixels = frame.Lock();
        Assert.True(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888);
        var origin = field.TranslatePoint(default, window)!.Value;
        var redOffset = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
        var blueOffset = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;

        bool IsRed(int horizontal, int vertical)
        {
            var pixelX = (int)((origin.X + horizontal) * window.RenderScaling);
            var pixelY = (int)((origin.Y + vertical) * window.RenderScaling);
            var offset = pixelY * pixels.RowBytes + pixelX * 4;
            return Marshal.ReadByte(pixels.Address, offset + redOffset) == 255
                && Marshal.ReadByte(pixels.Address, offset + 1) == 0
                && Marshal.ReadByte(pixels.Address, offset + blueOffset) == 0;
        }

        for (var horizontal = 3; horizontal < 8; horizontal++)
        for (var vertical = 3; vertical < 8; vertical++)
            Assert.Equal(expected, IsRed(horizontal, vertical));
        Assert.False(IsRed(2, 4));
        Assert.False(IsRed(8, 4));
        Assert.False(IsRed(4, 2));
        Assert.False(IsRed(4, 8));
    }

    private static TemplatedControl CreateField(string kind) => kind switch
    {
        "TextBox" => new TextBox { Text = "Nome" },
        "ComboBox" => new ComboBox { ItemsSource = new[] { "Unidade" }, SelectedIndex = 0 },
        "NumericUpDown" => new NumericUpDown { Value = 1 },
        "DatePicker" => new DatePicker(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static Window Show(Control field, bool dark)
    {
        field.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        field.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var window = new Window
        {
            Width = 360,
            Height = 100,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = field
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void FocusInput(TemplatedControl field)
    {
        Control target = field switch
        {
            NumericUpDown => Part<TextBox>(field, "PART_TextBox"),
            DatePicker => Part<Button>(field, "PART_FlyoutButton"),
            _ => field
        };
        Assert.True(target.Focus());
        Dispatcher.UIThread.RunJobs();
    }

    private static TControl Part<TControl>(Control field, string name) where TControl : Control =>
        Assert.Single(field.GetVisualDescendants().OfType<TControl>(), child => child.Name == name);

    private static (IBrush? Brush, Thickness Thickness) FieldBorder(TemplatedControl field)
    {
        if (field is DatePicker)
        {
            var presenter = Part<ContentPresenter>(Part<Button>(field, "PART_FlyoutButton"), "PART_ContentPresenter");
            return (presenter.BorderBrush, presenter.BorderThickness);
        }
        var border = field switch
        {
            TextBox => Part<Border>(field, "PART_BorderElement"),
            ComboBox => Part<Border>(field, "HighlightBackground"),
            NumericUpDown => Part<ButtonSpinner>(field, "PART_Spinner")
                .GetVisualDescendants().OfType<Border>().First(),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        return (border.BorderBrush, border.BorderThickness);
    }
}