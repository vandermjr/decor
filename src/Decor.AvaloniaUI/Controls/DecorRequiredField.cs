using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Decor.AvaloniaUI.Controls;

/// <summary>
/// Marks the actual input (or a lookup's outer Border) with
/// controls:DecorRequiredField.IsRequired="True". This is not a validation error.
/// In a custom visual root without an AdornerLayer, wrap the input in a
/// DecorRequiredFieldHost; keep IsRequired on the input, not on its label.
/// </summary>
public sealed class DecorRequiredField : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsRequiredProperty =
        AvaloniaProperty.RegisterAttached<DecorRequiredField, Control, bool>("IsRequired");

    private static readonly AttachedProperty<Registration?> RegistrationProperty =
        AvaloniaProperty.RegisterAttached<DecorRequiredField, Control, Registration?>("Registration");

    static DecorRequiredField()
    {
        IsRequiredProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.GetValue(RegistrationProperty)?.Dispose();
            control.ClearValue(RegistrationProperty);
            if (GetIsRequired(control))
                control.SetValue(RegistrationProperty, new Registration(control));
        });
    }

    public static bool GetIsRequired(Control control) => control.GetValue(IsRequiredProperty);

    public static void SetIsRequired(Control control, bool value) => control.SetValue(IsRequiredProperty, value);

    private sealed class Registration : IDisposable
    {
        private readonly Control _control;
        private readonly RequiredMarker _marker = new();
        private readonly IDisposable? _helpText;
        private Panel? _surface;
        private Visual[] _visibilitySources = Array.Empty<Visual>();

        public Registration(Control control)
        {
            _control = control;
            if (string.IsNullOrEmpty(AutomationProperties.GetHelpText(control)))
                _helpText = control.SetValue(AutomationProperties.HelpTextProperty,
                    "Campo obrigatorio", BindingPriority.Style);
            control.AttachedToVisualTree += OnAttached;
            control.DetachedFromVisualTree += OnDetached;
            if (control.IsAttachedToVisualTree())
                Attach();
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs args) => Attach();

        private void Attach()
        {
            UnsubscribeVisibility();
            _visibilitySources = _control.GetVisualAncestors().Prepend(_control).ToArray();
            foreach (var visual in _visibilitySources)
                visual.PropertyChanged += OnVisibilityChanged;
            _control.LayoutUpdated -= OnLayoutUpdated;
            _control.LayoutUpdated += OnLayoutUpdated;
            UpdateSurface();
        }

        private void OnVisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == Visual.IsVisibleProperty)
                UpdateVisibility();
        }

        private void UpdateVisibility() => _marker.IsVisible = _control.IsEffectivelyVisible;

        private void UnsubscribeVisibility()
        {
            foreach (var visual in _visibilitySources)
                visual.PropertyChanged -= OnVisibilityChanged;
            _visibilitySources = Array.Empty<Visual>();
        }

        private void OnLayoutUpdated(object? sender, EventArgs args) => UpdateSurface();

        private void UpdateSurface()
        {
            UpdateVisibility();
            Panel? surface = AdornerLayer.GetAdornerLayer(_control)
                ?? (Panel?)_control.GetVisualAncestors().OfType<DecorRequiredFieldHost>().FirstOrDefault();
            if (ReferenceEquals(surface, _surface))
                return;
            RemoveMarker();
            if (surface is null)
                return;
            _surface = surface;
            _marker.Target = _control;
            if (surface is AdornerLayer)
            {
                AdornerLayer.SetAdornedElement(_marker, _control);
                AdornerLayer.SetIsClipEnabled(_marker, true);
            }
            surface.Children.Add(_marker);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            UnsubscribeVisibility();
            _control.LayoutUpdated -= OnLayoutUpdated;
            RemoveMarker();
        }

        private void RemoveMarker()
        {
            _surface?.Children.Remove(_marker);
            AdornerLayer.SetAdornedElement(_marker, null);
            _marker.Target = null;
            _surface = null;
        }

        public void Dispose()
        {
            UnsubscribeVisibility();
            _control.AttachedToVisualTree -= OnAttached;
            _control.DetachedFromVisualTree -= OnDetached;
            _control.LayoutUpdated -= OnLayoutUpdated;
            RemoveMarker();
            _helpText?.Dispose();
        }
    }

    private sealed class RequiredMarker : Control
    {
        public Control? Target { get; set; }

        public RequiredMarker()
        {
            IsHitTestVisible = false;
            Focusable = false;
            ClipToBounds = true;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (Target is { IsEffectivelyVisible: true } target
                && target.TranslatePoint(new Point(3, 3), this) is { } point)
                context.FillRectangle(Brushes.Red, new Rect(point, new Size(5, 5)));
        }
    }
}

/// <summary>Optional overlay host for custom roots without an Avalonia AdornerLayer.</summary>
public sealed class DecorRequiredFieldHost : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = new Size();
        foreach (var child in Children.Where(child => DecorRequiredField.GetIsRequired(child) || child.IsHitTestVisible))
        {
            child.Measure(availableSize);
            desired = new Size(Math.Max(desired.Width, child.DesiredSize.Width),
                Math.Max(desired.Height, child.DesiredSize.Height));
        }
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
            child.Arrange(new Rect(finalSize));
        return finalSize;
    }
}