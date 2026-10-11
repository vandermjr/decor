using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace Decor.AvaloniaUI.Controls;

public sealed class DecorActionAvailability : AvaloniaObject
{
    private static readonly ConditionalWeakTable<Button, State> States = new();

    public static readonly AttachedProperty<bool> HideWhenDisabledProperty =
        AvaloniaProperty.RegisterAttached<DecorActionAvailability, Button, bool>("HideWhenDisabled");

    static DecorActionAvailability()
    {
        HideWhenDisabledProperty.Changed.AddClassHandler<Button>((button, args) =>
        {
            if (GetHideWhenDisabled(button))
            {
                if (!States.TryGetValue(button, out _))
                    States.Add(button, new State(button));
            }
            else if (States.TryGetValue(button, out var state))
            {
                state.Dispose();
                States.Remove(button);
            }
        });
    }

    public static bool GetHideWhenDisabled(Button button) => button.GetValue(HideWhenDisabledProperty);
    public static void SetHideWhenDisabled(Button button, bool value) => button.SetValue(HideWhenDisabledProperty, value);

    private sealed class State : IDisposable
    {
        private readonly Button _button;
        private IDisposable? _hiddenValue;

        public State(Button button)
        {
            _button = button;
            _button.PropertyChanged += OnPropertyChanged;
            Update();
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == InputElement.IsEffectivelyEnabledProperty)
                Update();
        }

        private void Update()
        {
            if (!_button.IsEffectivelyEnabled)
                _hiddenValue ??= _button.SetValue(Visual.IsVisibleProperty, false, BindingPriority.Animation);
            else
            {
                _hiddenValue?.Dispose();
                _hiddenValue = null;
            }
        }

        public void Dispose()
        {
            _button.PropertyChanged -= OnPropertyChanged;
            _hiddenValue?.Dispose();
        }
    }
}