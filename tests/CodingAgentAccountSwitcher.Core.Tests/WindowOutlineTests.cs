using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using CodingAgentAccountSwitcher.App;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class WindowOutlineTests
{
    [Theory]
    [InlineData("Light", 1)]
    [InlineData("Light", 1.25)]
    [InlineData("Light", 1.5)]
    [InlineData("Light", 2)]
    [InlineData("Dark", 1)]
    [InlineData("Dark", 1.25)]
    [InlineData("Dark", 1.5)]
    [InlineData("Dark", 2)]
    public void ActualOutlineRendersOnePixelOnAllFourEdges(string theme, double scale) => RunOnSta(() =>
    {
        var resources = LoadResources(theme);
        var outline = new PixelWindowOutline
        {
            Resources = resources,
            Style = (Style)resources["Style.WindowOutline"],
        };
        VisualTreeHelper.SetRootDpi(outline, new DpiScale(scale, scale));
        outline.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        outline.Measure(new Size(160, 100));
        outline.Arrange(new Rect(0, 0, 160, 100));
        outline.UpdateLayout();

        Assert.Equal(new Thickness(1 / scale), outline.BorderThickness);
        Assert.Equal(new Thickness(0), outline.Margin);
        Assert.Equal(new CornerRadius(22), outline.CornerRadius);
        Assert.False(outline.IsHitTestVisible);
        Assert.False(outline.Focusable);
        Assert.False(outline.UseLayoutRounding);
        Assert.Null(outline.Background);
        Assert.Null(outline.Child);
        Assert.Null(outline.Effect);
        Assert.Null(outline.CacheMode);
        var gray = Assert.IsType<SolidColorBrush>(resources["Brush.Window.Outline"]).Color;
        Assert.Equal(gray.R, gray.G);
        Assert.Equal(gray.G, gray.B);
        Assert.Equal(255, gray.A);

        var width = (int)(160 * scale);
        var height = (int)(100 * scale);
        var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(outline);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        byte Alpha(int x, int y) => pixels[(y * width + x) * 4 + 3];

        // Only the outermost physical pixel is filled on straight edges. The
        // next pixel and the interior stay transparent, without a blurred halo.
        Assert.Equal(255, Alpha(width / 2, 0));
        Assert.Equal(255, Alpha(width / 2, height - 1));
        Assert.Equal(255, Alpha(0, height / 2));
        Assert.Equal(255, Alpha(width - 1, height / 2));
        Assert.Equal(0, Alpha(width / 2, 1));
        Assert.Equal(0, Alpha(width / 2, height - 2));
        Assert.Equal(0, Alpha(1, height / 2));
        Assert.Equal(0, Alpha(width - 2, height / 2));
        Assert.Equal(0, Alpha(width / 2, height / 2));
        Assert.Equal(0, Alpha(0, 0));
    });

    [Fact]
    public void DpiChangesUpdateTheExistingOutlineWithoutAccumulatingThickness() => RunOnSta(() =>
    {
        var outline = new PixelWindowOutline();
        VisualTreeHelper.SetRootDpi(outline, new DpiScale(1, 1));
        outline.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        foreach (var scale in new[] { 1d, 1.25, 2, 1.5, 1, 2, 1 })
        {
            VisualTreeHelper.SetRootDpi(outline, new DpiScale(scale, scale));
            Assert.Equal(new Thickness(1 / scale), outline.BorderThickness);
        }
        var thickness = PixelWindowOutline.ThicknessForDpi(new DpiScale(1.25, 1.5));
        Assert.Equal(1, thickness.Left * 1.25);
        Assert.Equal(1, thickness.Top * 1.5);
        Assert.Equal(thickness.Left, thickness.Right);
        Assert.Equal(thickness.Top, thickness.Bottom);
    });

    [Fact]
    public void NativeBorderColorUsesColorRefOrdering() =>
        Assert.Equal(0x00332211u, PixelWindowOutline.ToColorRef(Color.FromRgb(0x11, 0x22, 0x33)));

    [Fact]
    public void OutlineIsAboveAllOverlaysWithNoShadowOrLayoutGutter()
    {
        XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var document = XDocument.Parse(ReadSource("MainWindow.xaml"));
        var window = document.Root!;
        var root = Assert.Single(window.Elements(ui + "Grid"));
        var children = root.Elements().ToArray();
        Assert.Equal(2, children.Length);
        Assert.Equal("WindowSurface", (string?)children[0].Attribute(x + "Name"));
        Assert.Equal("WindowOutline", (string?)children[1].Attribute(x + "Name"));
        Assert.Equal("PixelWindowOutline", children[1].Name.LocalName);
        Assert.False(children[1].HasElements);
        Assert.Null(children[0].Attribute("Margin"));
        Assert.Equal("700", (string?)window.Attribute("Height"));
        Assert.Equal("520", (string?)window.Attribute("MinHeight"));
        var chrome = Assert.Single(document.Descendants(ui + "WindowChrome"));
        Assert.Equal("0", (string?)chrome.Attribute("GlassFrameThickness"));
        Assert.Equal("8", (string?)chrome.Attribute("ResizeBorderThickness"));
        Assert.All(document.Descendants().Where(element =>
            ((string?)element.Attribute(x + "Name"))?.EndsWith("Overlay", StringComparison.Ordinal) == true),
            element => Assert.Same(children[0], element.Parent));

        var controls = XDocument.Parse(ReadSource("Themes", "Controls.xaml"));
        var style = Assert.Single(controls.Descendants(ui + "Style"),
            element => (string?)element.Attribute(x + "Key") == "Style.WindowOutline");
        var maximized = Assert.Single(style.Descendants(ui + "DataTrigger"),
            element => (string?)element.Attribute("Value") == "Maximized");
        Assert.Contains(maximized.Elements(ui + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "CornerRadius" && (string?)setter.Attribute("Value") == "0");
        Assert.Contains(style.Descendants(ui + "DataTrigger"), element =>
            (string?)element.Attribute("Binding") == "{Binding Path=(SystemParameters.HighContrast)}");
        Assert.DoesNotContain("DropShadowEffect", controls.ToString());
        var code = ReadSource("MainWindow.xaml.cs");
        Assert.DoesNotContain("WindowShadow", code);
        Assert.DoesNotContain("Height +=", code);
        Assert.DoesNotContain("WindowSurface.Margin", code);
        Assert.Contains("chrome.GlassFrameThickness = default;", code);
        Assert.Contains("WindowOutline.Visibility = _usesNativeFrame ? Visibility.Collapsed : Visibility.Visible;", code);
    }

    private static ResourceDictionary LoadResources(string theme)
    {
        var resources = new ResourceDictionary();
        foreach (var file in new[] { $"Colors.{theme}.xaml", "Controls.xaml" })
        {
            resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri(
                $"/CodingAgentAccountSwitcher;component/Themes/{file}", UriKind.Relative)));
        }
        return resources;
    }

    private static string ReadSource(params string[] path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CodingAgentAccountSwitcher.sln")))
            {
                return File.ReadAllText(Path.Combine(
                    [directory.FullName, "src", "CodingAgentAccountSwitcher.App", .. path]));
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Window outline tests require a repository checkout.");
    }

    private static void RunOnSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The isolated outline test timed out.");
        failure?.Throw();
    }
}
