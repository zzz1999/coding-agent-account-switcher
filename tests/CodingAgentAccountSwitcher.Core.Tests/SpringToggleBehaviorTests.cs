using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CodingAgentAccountSwitcher.App;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class SpringToggleBehaviorTests
{
    [Fact]
    public void ThumbMotionHasVisibleContainedOvershootAndExactEndpoints()
    {
        RunOnSta(() =>
        {
            var animation = SpringToggleBehavior.CreateThumbAnimation(0, 18);
            var easing = Assert.IsAssignableFrom<IEasingFunction>(animation.EasingFunction);
            var samples = Enumerable.Range(0, 1001).Select(index => easing.Ease(index / 1000d)).ToArray();

            Assert.Equal(TimeSpan.FromMilliseconds(420), animation.Duration.TimeSpan);
            Assert.Equal(FillBehavior.Stop, animation.FillBehavior);
            Assert.Equal(0d, samples[0]);
            Assert.Equal(1d, samples[^1]);
            Assert.InRange(samples[200], 0.95, 1.05);
            Assert.InRange(samples.Max(), 1.11, 1.125);
            Assert.InRange(samples.Min(), 0, 0);
            Assert.InRange(Math.Abs(samples[900] - 1), 0, 0.0015);
            // Both directions must retain a gap from the 50-DIP track edge,
            // including at the peak of the bounce (24-DIP thumb, 4-DIP inset).
            foreach (var progress in samples)
            {
                foreach (var x in new[] { 18 * progress, 18 * (1 - progress) })
                {
                    Assert.InRange(4 + x, 1, 25);
                    Assert.InRange(4 + x + 24, 25, 49);
                }
            }
        });
    }

    [Fact]
    public void TrackMotionStaysWithinItsOpacityEndpoints()
    {
        RunOnSta(() =>
        {
            foreach (var (from, to) in new[] { (0d, 1d), (1d, 0d), (0.4d, 1d), (0.6d, 0d) })
            {
                var animation = SpringToggleBehavior.CreateTrackAnimation(from, to);
                var easing = Assert.IsAssignableFrom<IEasingFunction>(animation.EasingFunction);
                var samples = Enumerable.Range(0, 101)
                    .Select(index => from + (to - from) * easing.Ease(index / 100d));

                Assert.Equal(from, animation.From);
                Assert.Equal(to, animation.To);
                Assert.All(samples, opacity => Assert.InRange(opacity, Math.Min(from, to), Math.Max(from, to)));
                Assert.Equal(FillBehavior.Stop, animation.FillBehavior);
            }
        });
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 18, 1)]
    [InlineData(null, 0, 0)]
    public void InitialLoadAndReloadSnapToLatestState(bool? isChecked, double x, double opacity)
    {
        RunOnSta(() =>
        {
            using var parts = CreateToggle(isChecked);
            SpringToggleBehavior.SetIsEnabled(parts.Toggle, true);
            parts.Toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal(x, parts.Translation.X);
            Assert.Equal(opacity, parts.Track.Opacity);
            Assert.False(parts.Translation.HasAnimatedProperties);

            parts.Toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            parts.Toggle.IsChecked = !isChecked.GetValueOrDefault();
            parts.Toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal(parts.Toggle.IsChecked == true ? 18 : 0, parts.Translation.X);
            Assert.Equal(parts.Toggle.IsChecked == true ? 1 : 0, parts.Track.Opacity);
            Assert.False(parts.Translation.HasAnimatedProperties);
        });
    }

    [Fact]
    public void RapidReversalContinuesFromCurrentPresentedValuesAndSettlesExactly()
    {
        RunOnSta(() =>
        {
            using var parts = CreateToggle(false);
            SpringToggleBehavior.ApplyVisualState(parts.Toggle, animate: false, clientAreaAnimationEnabled: true);
            parts.Toggle.IsChecked = true;
            var motion = SpringToggleBehavior.ApplyVisualState(parts.Toggle, animate: true, clientAreaAnimationEnabled: true);
            Advance(motion, TimeSpan.FromMilliseconds(45));

            foreach (var isChecked in new[] { false, true, false, true })
            {
                var currentX = parts.Translation.X;
                var currentOpacity = parts.Track.Opacity;
                parts.Toggle.IsChecked = isChecked;
                motion = SpringToggleBehavior.ApplyVisualState(parts.Toggle, animate: true, clientAreaAnimationEnabled: true);

                Assert.Equal(currentX, parts.Translation.X, precision: 6);
                Assert.Equal(currentOpacity, parts.Track.Opacity, precision: 6);
                Assert.Equal(isChecked ? 18d : 0d,
                    parts.Translation.GetAnimationBaseValue(TranslateTransform.XProperty));
                Advance(motion, TimeSpan.FromMilliseconds(45));
            }

            Advance(motion, SpringToggleBehavior.MotionDuration + TimeSpan.FromMilliseconds(60));
            Assert.Equal(18d, parts.Translation.X);
            Assert.Equal(1d, parts.Track.Opacity);
        });
    }

    [Fact]
    public void DisablingSystemAnimationImmediatelyRemovesCurrentMotion()
    {
        RunOnSta(() =>
        {
            using var parts = CreateToggle(false);
            parts.Toggle.IsChecked = true;
            SpringToggleBehavior.ApplyVisualState(parts.Toggle, animate: true, clientAreaAnimationEnabled: true);
            Assert.True(parts.Translation.HasAnimatedProperties);

            SpringToggleBehavior.ApplyVisualState(parts.Toggle, animate: true, clientAreaAnimationEnabled: false);

            Assert.Equal(18d, parts.Translation.X);
            Assert.Equal(1d, parts.Track.Opacity);
            Assert.False(parts.Translation.HasAnimatedProperties);
            Assert.False(parts.Track.HasAnimatedProperties);
            parts.Toggle.IsChecked = false;
            SpringToggleBehavior.ApplyVisualState(parts.Toggle, animate: true, clientAreaAnimationEnabled: false);
            Assert.Equal(0d, parts.Translation.X);
            Assert.Equal(0d, parts.Track.Opacity);
        });
    }

    [Fact]
    public void UnloadAndDisablingBehaviorRemoveAnimationClocks()
    {
        RunOnSta(() =>
        {
            using var parts = CreateToggle(false);
            SpringToggleBehavior.SetIsEnabled(parts.Toggle, true);
            parts.Toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            foreach (var detachBehavior in new[] { false, true })
            {
                parts.Toggle.IsChecked = false;
                SpringToggleBehavior.ApplyVisualState(parts.Toggle, animate: false, clientAreaAnimationEnabled: true);
                parts.Toggle.IsChecked = true;
                SpringToggleBehavior.ApplyVisualState(parts.Toggle, animate: true, clientAreaAnimationEnabled: true);
                if (detachBehavior)
                {
                    SpringToggleBehavior.SetIsEnabled(parts.Toggle, false);
                }
                else
                {
                    parts.Toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                }

                Assert.Equal(18d, parts.Translation.X);
                Assert.Equal(1d, parts.Track.Opacity);
                Assert.False(parts.Translation.HasAnimatedProperties);
                Assert.False(parts.Track.HasAnimatedProperties);
            }
        });
    }

    [Fact]
    public void RtlUsesTheSameLocalOffsetsAndControlsRemainIndependent()
    {
        RunOnSta(() =>
        {
            using var leftToRight = CreateToggle(true);
            using var rightToLeft = CreateToggle(false, FlowDirection.RightToLeft);
            SpringToggleBehavior.ApplyVisualState(leftToRight.Toggle, false, true);
            SpringToggleBehavior.ApplyVisualState(rightToLeft.Toggle, false, true);
            Assert.Equal(18d, leftToRight.Translation.X);
            Assert.Equal(0d, rightToLeft.Translation.X);

            rightToLeft.Toggle.IsChecked = true;
            SpringToggleBehavior.ApplyVisualState(rightToLeft.Toggle, false, true);
            Assert.Equal(18d, rightToLeft.Translation.X);
            Assert.Equal(18d, leftToRight.Translation.X);
            Assert.NotSame(leftToRight.Translation, rightToLeft.Translation);
        });
    }

    [Fact]
    public void MissingTemplatePartsAndNonToggleTargetsAreHarmless()
    {
        RunOnSta(() =>
        {
            var toggle = new ToggleButton { IsChecked = true };
            SpringToggleBehavior.ApplyVisualState(toggle, true, true);
            var border = new Border();
            SpringToggleBehavior.SetIsEnabled(border, true);
            SpringToggleBehavior.SetIsEnabled(border, false);
        });
    }

    [Theory]
    [InlineData("Colors.Light.xaml", FlowDirection.LeftToRight)]
    [InlineData("Colors.Light.xaml", FlowDirection.RightToLeft)]
    [InlineData("Colors.Dark.xaml", FlowDirection.LeftToRight)]
    [InlineData("Colors.Dark.xaml", FlowDirection.RightToLeft)]
    public void SharedStyleWiresInitialStateAndCheckedEvents(string theme, FlowDirection flow)
    {
        RunOnSta(() =>
        {
            var resources = new ResourceDictionary();
            foreach (var file in new[] { theme, "Controls.xaml" })
            {
                resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri(
                    $"/CodingAgentAccountSwitcher;component/Themes/{file}", UriKind.Relative)));
            }
            var toggle = new ToggleButton
            {
                Resources = resources,
                Style = (Style)resources["Style.SettingsToggle"],
                IsChecked = true,
                FlowDirection = flow,
            };
            toggle.ApplyTemplate();
            toggle.Measure(new Size(54, 44));
            toggle.Arrange(new Rect(0, 0, 54, 44));
            toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            using var parts = new ToggleParts(
                toggle,
                Assert.IsType<Border>(toggle.Template.FindName("Thumb", toggle)),
                Assert.IsType<Border>(toggle.Template.FindName("TrackOn", toggle)));

            Assert.True(SpringToggleBehavior.GetIsEnabled(toggle));
            Assert.Equal(18d, parts.Translation.X);
            Assert.Equal(1d, parts.Track.Opacity);
            Assert.False(parts.Translation.HasAnimatedProperties);

            toggle.IsChecked = false;

            Assert.Equal(0d, parts.Translation.GetAnimationBaseValue(TranslateTransform.XProperty));
            Assert.Equal(0d, parts.Track.GetAnimationBaseValue(UIElement.OpacityProperty));
            Assert.Equal(SystemParameters.ClientAreaAnimation, parts.Translation.HasAnimatedProperties);
            toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Equal(0d, parts.Translation.X);
            Assert.Equal(0d, parts.Track.Opacity);
        });
    }

    private static ToggleParts CreateToggle(bool? isChecked, FlowDirection flow = FlowDirection.LeftToRight)
    {
        const string template = """
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="ToggleButton">
                <Grid Width="50" Height="30">
                    <Border x:Name="TrackOn" Opacity="0" />
                    <Border x:Name="Thumb" Width="24" Height="24" Margin="3" HorizontalAlignment="Left">
                        <Border.RenderTransform><TranslateTransform /></Border.RenderTransform>
                    </Border>
                </Grid>
            </ControlTemplate>
            """;
        var toggle = new ToggleButton
        {
            Template = (ControlTemplate)XamlReader.Parse(template),
            IsChecked = isChecked,
            FlowDirection = flow,
        };
        toggle.ApplyTemplate();
        var thumb = (Border)toggle.Template.FindName("Thumb", toggle);
        var track = (Border)toggle.Template.FindName("TrackOn", toggle);
        return new ToggleParts(toggle, thumb, track);
    }

    private static void Advance((AnimationClock? Thumb, AnimationClock? Track) motion, TimeSpan time)
    {
        motion.Thumb?.Controller!.SeekAlignedToLastTick(time, TimeSeekOrigin.BeginTime);
        motion.Track?.Controller!.SeekAlignedToLastTick(time, TimeSeekOrigin.BeginTime);
    }

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The isolated WPF test did not finish.");
        error?.Throw();
    }

    private sealed record ToggleParts(ToggleButton Toggle, Border Thumb, Border Track) : IDisposable
    {
        public TranslateTransform Translation => (TranslateTransform)Thumb.RenderTransform;

        public void Dispose()
        {
            Toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            SpringToggleBehavior.SetIsEnabled(Toggle, false);
            SpringToggleBehavior.ApplyVisualState(Toggle, false, false);
        }
    }
}
