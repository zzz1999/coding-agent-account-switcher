using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodingAgentAccountSwitcher.App;

/// <summary>A decorative, one-device-pixel outline for the Windows 10 shell.</summary>
public sealed class PixelWindowOutline : Border
{
    public PixelWindowOutline()
    {
        IsHitTestVisible = false;
        Focusable = false;
        // A 1-DIP Border rounds to two pixels at some DPI settings. Retain the
        // fractional DIP thickness instead; straight edges then fill one pixel.
        UseLayoutRounding = false;
        SnapsToDevicePixels = false;
        SetCurrentValue(BorderThicknessProperty, ThicknessForDpi(VisualTreeHelper.GetDpi(this)));
        // The actual HWND/parent DPI is only established when first attached.
        Loaded += (_, _) => SetCurrentValue(
            BorderThicknessProperty, ThicknessForDpi(VisualTreeHelper.GetDpi(this)));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        SetCurrentValue(BorderThicknessProperty, ThicknessForDpi(newDpi));
    }

    internal static Thickness ThicknessForDpi(DpiScale dpi) =>
        new(1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY, 1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY);

    // DWM expects COLORREF (0x00BBGGRR), not WPF's ARGB representation.
    internal static uint ToColorRef(Color color) => (uint)(color.R | color.G << 8 | color.B << 16);
}
