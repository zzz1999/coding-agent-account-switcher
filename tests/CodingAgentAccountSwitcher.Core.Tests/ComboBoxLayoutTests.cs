using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class ComboBoxLayoutTests
{
    [Theory]
    [InlineData(320d, FlowDirection.LeftToRight)]
    [InlineData(488d, FlowDirection.LeftToRight)]
    [InlineData(320d, FlowDirection.RightToLeft)]
    [InlineData(488d, FlowDirection.RightToLeft)]
    public void PopupItemBackgroundsFillViewportBeforeAndAfterScrolling(
        double width,
        FlowDirection flowDirection) => RunOnSta(() =>
    {
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(LoadDictionary("Colors.Light.xaml"));
        resources.MergedDictionaries.Add(LoadDictionary("Controls.xaml"));
        var comboBox = new ComboBox
        {
            Resources = resources,
            Style = (Style)resources["Style.ComboBox"],
            Width = width,
            FlowDirection = flowDirection
        };
        foreach (var language in new[]
        {
            "English", "简体中文", "繁體中文", "日本語", "한국어", "Deutsch",
            "Español", "Français", "Português", "Русский", "हिन्दी", "العربية"
        })
        {
            comboBox.Items.Add(language);
        }
        comboBox.SelectedIndex = 0;
        Layout(comboBox, new Size(width, 40));
        var popup = Assert.IsType<Popup>(comboBox.Template.FindName("PART_Popup", comboBox));
        var popupBorder = Assert.IsType<Border>(popup.Child);

        // Measure the actual template-generated popup subtree without opening a
        // native window or invoking App/MainWindow startup and account access.
        Layout(popupBorder, new Size(width, 244));
        var viewer = Assert.IsType<ScrollViewer>(popupBorder.Child);
        var presenter = Assert.IsType<ItemsPresenter>(viewer.Content);
        Assert.Equal(flowDirection, popupBorder.FlowDirection);
        Assert.Equal(ScrollBarVisibility.Disabled, viewer.HorizontalScrollBarVisibility);
        Assert.Equal(0d, viewer.ScrollableWidth);
        Assert.True(viewer.ScrollableHeight > 0, "The language list must actually require vertical scrolling.");

        AssertFillsViewport(comboBox, popupBorder, viewer, presenter, width);
        Assert.Equal(HorizontalAlignment.Stretch, viewer.HorizontalContentAlignment);
        var firstItem = Assert.IsType<ComboBoxItem>(comboBox.ItemContainerGenerator.ContainerFromIndex(0));
        var firstBackground = Assert.IsType<Border>(firstItem.Template.FindName("ItemRoot", firstItem));
        Assert.True(firstItem.IsSelected);
        Assert.Same(resources["Brush.Accent.Muted"], firstBackground.Background);
        var hoverTrigger = Assert.Single(firstItem.Template.Triggers.OfType<Trigger>(),
            trigger => trigger.Property == ComboBoxItem.IsHighlightedProperty);
        Assert.Contains(hoverTrigger.Setters.OfType<Setter>(),
            setter => setter.TargetName == "ItemRoot" && setter.Property == Border.BackgroundProperty);

        viewer.ScrollToEnd();
        Layout(popupBorder, new Size(width, 244));
        Assert.True(viewer.VerticalOffset > 0, "The verification must include an actual scrolled layout.");
        AssertFillsViewport(comboBox, popupBorder, viewer, presenter, width);

        comboBox.SelectedIndex = comboBox.Items.Count - 1;
        Layout(popupBorder, new Size(width, 244));
        var lastItem = Assert.IsType<ComboBoxItem>(
            comboBox.ItemContainerGenerator.ContainerFromIndex(comboBox.Items.Count - 1));
        var lastBackground = Assert.IsType<Border>(lastItem.Template.FindName("ItemRoot", lastItem));
        Assert.True(lastItem.IsSelected);
        Assert.Same(resources["Brush.Accent.Muted"], lastBackground.Background);
        AssertFillsViewport(comboBox, popupBorder, viewer, presenter, width);

        // Negative control: reproduce the previous inherited Left alignment on
        // this same real template. The geometry check must distinguish the bug
        // from the corrected Stretch layout, not merely inspect XAML setters.
        viewer.HorizontalContentAlignment = HorizontalAlignment.Left;
        Layout(popupBorder, new Size(width, 244));
        Assert.True(presenter.ActualWidth < viewer.ActualWidth - 20,
            "The former Left alignment should reproduce a content-sized list.");
        Assert.True(lastBackground.ActualWidth < viewer.ActualWidth - 20,
            "The former Left alignment should reproduce a text-width selected background.");
    });

    private static void AssertFillsViewport(
        ComboBox comboBox,
        Border popupBorder,
        ScrollViewer viewer,
        ItemsPresenter presenter,
        double expectedPopupWidth)
    {
        AssertClose(expectedPopupWidth, popupBorder.ActualWidth, "Popup border");
        var expectedViewportWidth = popupBorder.ActualWidth -
            popupBorder.BorderThickness.Left - popupBorder.BorderThickness.Right -
            popupBorder.Padding.Left - popupBorder.Padding.Right;
        AssertClose(expectedViewportWidth, viewer.ActualWidth, "ScrollViewer");
        AssertClose(expectedViewportWidth, viewer.ViewportWidth, "Viewport");
        var scrollContent = Assert.IsType<ScrollContentPresenter>(
            viewer.Template.FindName("PART_ScrollContentPresenter", viewer));
        AssertClose(expectedViewportWidth, scrollContent.ActualWidth, "ScrollContentPresenter");
        AssertClose(expectedViewportWidth, presenter.ActualWidth, "ItemsPresenter");

        for (var index = 0; index < comboBox.Items.Count; index++)
        {
            var item = Assert.IsType<ComboBoxItem>(comboBox.ItemContainerGenerator.ContainerFromIndex(index));
            var background = Assert.IsType<Border>(item.Template.FindName("ItemRoot", item));
            AssertClose(expectedViewportWidth, item.ActualWidth, $"Item {index}");
            AssertClose(expectedViewportWidth, background.ActualWidth, $"Item {index} background");
        }
    }

    private static void AssertClose(double expected, double actual, string element) =>
        Assert.True(Math.Abs(expected - actual) < 0.5,
            $"{element} should fill {expected:0.##} DIPs, but its actual width was {actual:0.##} DIPs.");

    private static ResourceDictionary LoadDictionary(string fileName) =>
        (ResourceDictionary)Application.LoadComponent(new Uri(
            $"/CodingAgentAccountSwitcher;component/Themes/{fileName}", UriKind.Relative));

    private static void Layout(FrameworkElement element, Size availableSize)
    {
        element.ApplyTemplate();
        element.Measure(availableSize);
        element.Arrange(new Rect(new Point(), availableSize));
        element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        element.UpdateLayout();
    }

    private static void RunOnSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The isolated WPF layout test timed out.");
        failure?.Throw();
    }
}
