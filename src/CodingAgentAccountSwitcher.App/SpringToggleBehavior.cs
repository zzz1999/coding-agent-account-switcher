using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodingAgentAccountSwitcher.App;

/// <summary>Animates the named parts of the shared settings-toggle template.</summary>
public static class SpringToggleBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SpringToggleBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(ToggleState), typeof(SpringToggleBehavior));

    internal const double CheckedOffset = 18;
    internal static readonly TimeSpan MotionDuration = TimeSpan.FromMilliseconds(420);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ToggleButton toggle)
        {
            return;
        }

        if (toggle.GetValue(StateProperty) is ToggleState previous)
        {
            previous.Detach();
            toggle.ClearValue(StateProperty);
        }

        if ((bool)e.NewValue)
        {
            var state = new ToggleState(toggle);
            toggle.SetValue(StateProperty, state);
            state.Attach();
        }
    }

    internal static DoubleAnimation CreateThumbAnimation(double from, double to) => new()
    {
        From = from,
        To = to,
        Duration = MotionDuration,
        EasingFunction = new DampedSpringEase { EasingMode = EasingMode.EaseIn },
        FillBehavior = FillBehavior.Stop,
    };

    internal static DoubleAnimation CreateTrackAnimation(double from, double to) => new()
    {
        From = Math.Clamp(from, 0, 1),
        To = Math.Clamp(to, 0, 1),
        Duration = TimeSpan.FromMilliseconds(220),
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop,
    };

    internal static (AnimationClock? Thumb, AnimationClock? Track) ApplyVisualState(
        ToggleButton toggle,
        bool animate,
        bool clientAreaAnimationEnabled)
    {
        toggle.ApplyTemplate();
        var isChecked = toggle.IsChecked == true;
        var useAnimation = animate && clientAreaAnimationEnabled;
        AnimationClock? thumbClock = null;
        AnimationClock? trackClock = null;
        if (toggle.Template?.FindName("Thumb", toggle) is Border thumb &&
            thumb.RenderTransform is TranslateTransform translation)
        {
            if (translation.IsFrozen)
            {
                translation = translation.CloneCurrentValue();
                thumb.SetCurrentValue(UIElement.RenderTransformProperty, translation);
            }

            // X is in the template's local coordinate system. WPF mirrors its
            // layout for RTL; changing this sign would mirror the motion twice.
            var current = translation.X;
            var target = isChecked ? CheckedOffset : 0;
            translation.BeginAnimation(TranslateTransform.XProperty, null);
            translation.X = target;
            if (useAnimation && Math.Abs(current - target) > 0.001)
            {
                thumbClock = StartAnimation(
                    translation,
                    TranslateTransform.XProperty,
                    CreateThumbAnimation(current, target));
            }
        }

        if (toggle.Template?.FindName("TrackOn", toggle) is Border track)
        {
            var current = track.Opacity;
            var target = isChecked ? 1d : 0d;
            track.BeginAnimation(UIElement.OpacityProperty, null);
            track.Opacity = target;
            if (useAnimation && Math.Abs(current - target) > 0.001)
            {
                trackClock = StartAnimation(
                    track,
                    UIElement.OpacityProperty,
                    CreateTrackAnimation(current, target));
            }
        }
        return (thumbClock, trackClock);
    }

    private static AnimationClock StartAnimation(
        IAnimatable target,
        DependencyProperty property,
        DoubleAnimation animation)
    {
        var clock = (AnimationClock)animation.CreateClock(true);
        target.ApplyAnimationClock(property, clock, HandoffBehavior.SnapshotAndReplace);
        // Prime the clock at the captured value immediately. Otherwise a new
        // clock can expose the destination base until the next rendering tick.
        clock.Controller!.Begin();
        clock.Controller.SeekAlignedToLastTick(TimeSpan.Zero, TimeSeekOrigin.BeginTime);
        return clock;
    }

    private sealed class ToggleState(ToggleButton toggle)
    {
        private bool _loaded;
        private bool _observingSystemSettings;

        public void Attach()
        {
            toggle.Loaded += OnLoaded;
            toggle.Unloaded += OnUnloaded;
            toggle.Checked += OnCheckedChanged;
            toggle.Unchecked += OnCheckedChanged;
            toggle.Indeterminate += OnCheckedChanged;
            if (toggle.IsLoaded)
            {
                OnLoaded(toggle, new RoutedEventArgs());
            }
        }

        public void Detach()
        {
            toggle.Loaded -= OnLoaded;
            toggle.Unloaded -= OnUnloaded;
            toggle.Checked -= OnCheckedChanged;
            toggle.Unchecked -= OnCheckedChanged;
            toggle.Indeterminate -= OnCheckedChanged;
            OnUnloaded(toggle, new RoutedEventArgs());
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _loaded = true;
            if (!_observingSystemSettings)
            {
                SystemParameters.StaticPropertyChanged += OnSystemSettingsChanged;
                _observingSystemSettings = true;
            }
            ApplyVisualState(toggle, animate: false, clientAreaAnimationEnabled: false);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _loaded = false;
            if (_observingSystemSettings)
            {
                SystemParameters.StaticPropertyChanged -= OnSystemSettingsChanged;
                _observingSystemSettings = false;
            }
            ApplyVisualState(toggle, animate: false, clientAreaAnimationEnabled: false);
        }

        private void OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, toggle))
            {
                ApplyVisualState(toggle, _loaded, SystemParameters.ClientAreaAnimation);
            }
        }

        private void OnSystemSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            {
                if (toggle.Dispatcher.CheckAccess())
                {
                    SnapIfAnimationDisabled();
                }
                else if (!toggle.Dispatcher.HasShutdownStarted)
                {
                    _ = toggle.Dispatcher.BeginInvoke(new Action(SnapIfAnimationDisabled));
                }
            }
        }

        private void SnapIfAnimationDisabled()
        {
            if (_loaded && !SystemParameters.ClientAreaAnimation)
            {
                ApplyVisualState(toggle, animate: false, clientAreaAnimationEnabled: false);
            }
        }
    }

    private sealed class DampedSpringEase : EasingFunctionBase
    {
        protected override double EaseInCore(double normalizedTime)
        {
            if (normalizedTime <= 0)
            {
                return 0;
            }
            if (normalizedTime >= 1)
            {
                return 1;
            }

            // Fast initial travel with a visible but contained spring: about
            // 12% (2.1 DIP) overshoot stays inside the track's 4-DIP inset.
            return Response(normalizedTime) / Response(1);
        }

        private static double Response(double time) =>
            1 - Math.Exp(-7.5 * time) * (Math.Cos(11 * time) + 7.5 / 11 * Math.Sin(11 * time));

        protected override Freezable CreateInstanceCore() => new DampedSpringEase();
    }
}
