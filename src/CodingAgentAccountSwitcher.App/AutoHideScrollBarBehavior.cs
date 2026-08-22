using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace CodingAgentAccountSwitcher.App;

/// <summary>
/// Keeps a scroll bar's hit-test area available while fading its indicator in
/// only for actual scrolling or direct scroll-bar interaction.
/// </summary>
public static class AutoHideScrollBarBehavior
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(800);

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AutoHideScrollBarBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyPropertyKey IsIndicatorVisiblePropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsIndicatorVisible",
            typeof(bool),
            typeof(AutoHideScrollBarBehavior),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsIndicatorVisibleProperty =
        IsIndicatorVisiblePropertyKey.DependencyProperty;

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(ScrollActivityState),
            typeof(AutoHideScrollBarBehavior));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsIndicatorVisible(DependencyObject element) =>
        (bool)element.GetValue(IsIndicatorVisibleProperty);

    internal static bool ShouldRevealIndicator(
        double verticalChange,
        double horizontalChange,
        double scrollableHeight,
        double scrollableWidth) =>
        (verticalChange != 0 || horizontalChange != 0) &&
        (scrollableHeight > 0 || scrollableWidth > 0);

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollViewer viewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            if (viewer.GetValue(StateProperty) is null)
            {
                // Establish a local state boundary so nested viewers never
                // mirror another viewer's transient indicator state.
                SetIndicatorVisible(viewer, false);
                viewer.SetValue(StateProperty, new ScrollActivityState(viewer));
            }
            return;
        }

        if (viewer.GetValue(StateProperty) is ScrollActivityState state)
        {
            state.Dispose();
            viewer.ClearValue(StateProperty);
        }
        SetIndicatorVisible(viewer, false);
    }

    private static void SetIndicatorVisible(DependencyObject element, bool value) =>
        element.SetValue(IsIndicatorVisiblePropertyKey, value);

    private sealed class ScrollActivityState : IDisposable
    {
        private readonly ScrollViewer _viewer;
        private readonly DispatcherTimer _idleTimer;
        private bool _eventsAttached;
        private bool _isScrollBarPointerDown;
        private bool _disposed;

        internal ScrollActivityState(ScrollViewer viewer)
        {
            _viewer = viewer;
            _idleTimer = new DispatcherTimer(
                IdleDelay,
                DispatcherPriority.Background,
                IdleTimer_Tick,
                viewer.Dispatcher);
            _idleTimer.Stop();
            _viewer.Loaded += Viewer_Loaded;
            _viewer.Unloaded += Viewer_Unloaded;
            if (_viewer.IsLoaded)
            {
                AttachInteractionEvents();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _idleTimer.Stop();
            _idleTimer.Tick -= IdleTimer_Tick;
            _viewer.Loaded -= Viewer_Loaded;
            _viewer.Unloaded -= Viewer_Unloaded;
            DetachInteractionEvents();
            SetIndicatorVisible(_viewer, false);
        }

        private void Viewer_Loaded(object sender, RoutedEventArgs e) =>
            AttachInteractionEvents();

        private void Viewer_Unloaded(object sender, RoutedEventArgs e)
        {
            _idleTimer.Stop();
            _isScrollBarPointerDown = false;
            SetIndicatorVisible(_viewer, false);
            DetachInteractionEvents();
        }

        private void AttachInteractionEvents()
        {
            if (_eventsAttached || _disposed)
            {
                return;
            }

            _eventsAttached = true;
            _viewer.ScrollChanged += Viewer_ScrollChanged;
            _viewer.PreviewMouseDown += Viewer_PreviewMouseDown;
            _viewer.PreviewMouseMove += Viewer_PreviewMouseMove;
            _viewer.PreviewMouseUp += Viewer_PreviewMouseUp;
            _viewer.LostMouseCapture += Viewer_LostMouseCapture;
            _viewer.PreviewTouchDown += Viewer_PreviewTouchDown;
            _viewer.PreviewTouchUp += Viewer_PreviewTouchUp;
            _viewer.LostTouchCapture += Viewer_LostTouchCapture;
        }

        private void DetachInteractionEvents()
        {
            if (!_eventsAttached)
            {
                return;
            }

            _eventsAttached = false;
            _viewer.ScrollChanged -= Viewer_ScrollChanged;
            _viewer.PreviewMouseDown -= Viewer_PreviewMouseDown;
            _viewer.PreviewMouseMove -= Viewer_PreviewMouseMove;
            _viewer.PreviewMouseUp -= Viewer_PreviewMouseUp;
            _viewer.LostMouseCapture -= Viewer_LostMouseCapture;
            _viewer.PreviewTouchDown -= Viewer_PreviewTouchDown;
            _viewer.PreviewTouchUp -= Viewer_PreviewTouchUp;
            _viewer.LostTouchCapture -= Viewer_LostTouchCapture;
        }

        private void Viewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_viewer.ScrollableHeight <= 0 && _viewer.ScrollableWidth <= 0)
            {
                _idleTimer.Stop();
                SetIndicatorVisible(_viewer, false);
                return;
            }

            if (ShouldRevealIndicator(
                    e.VerticalChange,
                    e.HorizontalChange,
                    _viewer.ScrollableHeight,
                    _viewer.ScrollableWidth))
            {
                RevealIndicator();
            }
        }

        private void Viewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && IsScrollBarSource(e.OriginalSource))
            {
                _isScrollBarPointerDown = true;
                RevealIndicator(keepVisible: true);
            }
        }

        private void Viewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isScrollBarPointerDown && e.LeftButton == MouseButtonState.Pressed)
            {
                RevealIndicator(keepVisible: true);
            }
        }

        private void Viewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isScrollBarPointerDown)
            {
                _isScrollBarPointerDown = false;
                RevealIndicator();
            }
        }

        private void Viewer_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isScrollBarPointerDown)
            {
                _isScrollBarPointerDown = false;
                RevealIndicator();
            }
        }

        private void Viewer_PreviewTouchDown(object? sender, TouchEventArgs e)
        {
            if (IsScrollBarSource(e.OriginalSource))
            {
                _isScrollBarPointerDown = true;
                RevealIndicator(keepVisible: true);
            }
        }

        private void Viewer_PreviewTouchUp(object? sender, TouchEventArgs e)
        {
            if (!_isScrollBarPointerDown)
            {
                return;
            }

            _isScrollBarPointerDown = false;
            RevealIndicator();
        }

        private void Viewer_LostTouchCapture(object? sender, TouchEventArgs e)
        {
            if (_isScrollBarPointerDown)
            {
                _isScrollBarPointerDown = false;
                RevealIndicator();
            }
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            _idleTimer.Stop();
            if (_isScrollBarPointerDown)
            {
                _idleTimer.Start();
                return;
            }

            SetIndicatorVisible(_viewer, false);
        }

        private void RevealIndicator(bool keepVisible = false)
        {
            if (_disposed ||
                (_viewer.ScrollableHeight <= 0 && _viewer.ScrollableWidth <= 0))
            {
                return;
            }

            SetIndicatorVisible(_viewer, true);
            _idleTimer.Stop();
            if (!keepVisible)
            {
                _idleTimer.Start();
            }
        }

        private bool IsScrollBarSource(object? source)
        {
            var current = source as DependencyObject;
            while (current is not null && !ReferenceEquals(current, _viewer))
            {
                if (current is ScrollBar)
                {
                    return true;
                }

                current = GetParent(current);
            }

            return false;
        }

        private static DependencyObject? GetParent(DependencyObject element) =>
            element is Visual or Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
    }
}
