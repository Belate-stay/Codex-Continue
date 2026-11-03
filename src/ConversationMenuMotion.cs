using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CodexContinue;

public static class ConversationMenuMotion
{
    public static Duration EnterDuration => DurationFor(220);
    public static Duration ExitDuration => DurationFor(140);
    public static Duration HoverDuration => DurationFor(160);
    public static Duration RestDuration => DurationFor(220);
    public static PopupAnimation PopupEffect => SystemParameters.ClientAreaAnimation ? PopupAnimation.Fade : PopupAnimation.None;

    static Duration DurationFor(int milliseconds) => new(TimeSpan.FromMilliseconds(
        SystemParameters.ClientAreaAnimation ? milliseconds : 0));

    public static readonly DependencyProperty BottomRadiusProperty = DependencyProperty.RegisterAttached(
        "BottomRadius", typeof(double), typeof(ConversationMenuMotion), new PropertyMetadata(12d, (d, e) =>
        {
            double radius = (double)e.NewValue;
            ((Border)d).CornerRadius = new CornerRadius(12, 12, radius, radius);
        }));

    public static double GetBottomRadius(DependencyObject element) => (double)element.GetValue(BottomRadiusProperty);
    public static void SetBottomRadius(DependencyObject element, double value) => element.SetValue(BottomRadiusProperty, value);

    public static readonly DependencyProperty RevealProperty = DependencyProperty.RegisterAttached(
        "Reveal", typeof(double), typeof(ConversationMenuMotion), new PropertyMetadata(0d, (d, _) => UpdateReveal((Border)d)));

    public static double GetReveal(DependencyObject element) => (double)element.GetValue(RevealProperty);
    public static void SetReveal(DependencyObject element, double value) => element.SetValue(RevealProperty, value);

    static void UpdateReveal(Border border)
    {
        if (border.Clip is not RectangleGeometry clip)
        {
            border.Clip = clip = new RectangleGeometry { RadiusX = 12, RadiusY = 12 };
            border.SizeChanged += (_, _) => UpdateReveal(border);
        }
        // The upper corners stay above the join; only the moving lower edge is rounded.
        clip.Rect = new Rect(0, -12, border.ActualWidth, 12 + border.ActualHeight * GetReveal(border));
    }
}
