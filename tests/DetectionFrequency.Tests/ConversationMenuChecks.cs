using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CodexContinue;

static class ConversationMenuChecks
{
    public static void Run(MainWindow main)
    {
        var combo = (ComboBox)main.FindName("ConvoPick");
        var row = (StackPanel)combo.Parent;
        ((Panel)row.Parent).Children.Remove(row);
        row.VerticalAlignment = VerticalAlignment.Top;
        var label = (TextBlock)main.FindName("LblConvo");
        label.Text = "● 对话";
        var host = new Window
        {
            Title = "Conversation menu regression",
            Width = 520, Height = 420, ShowActivated = false,
            Content = new Border
            {
                Padding = new Thickness(24),
                Child = row,
            },
        };
        combo.VerticalAlignment = VerticalAlignment.Top;
        combo.ItemsSource = Enumerable.Range(1, 30)
            .Select(i => new CodexService.RunningThread($"test-{i}", i == 1 ? "短对话" : $"Conversation {i}: " + new string('W', 96), i % 3 == 0))
            .ToArray();
        combo.SelectedIndex = 0;
        var originalTheme = ThemeManager.Mode;
        try
        {
            host.Show();
            Pump(80);
            foreach (var mode in new[] { CodexContinue.ThemeMode.Dark, CodexContinue.ThemeMode.Light })
            {
                ThemeManager.SetMode(mode, Application.Current);
                Pump(40);
                var expected = ((SolidColorBrush)Application.Current.FindResource("TextPrimaryBrush")).Color;
                Require(combo.Foreground is SolidColorBrush ink && ink.Color == expected,
                    "Collapsed conversation text does not follow the active theme");
                var presenter = (ContentPresenter)combo.Template.FindName("cp", combo);
                Require(Find<TextBlock>(presenter).Foreground is SolidColorBrush titleInk && titleInk.Color == expected,
                    "The actual selected conversation title does not inherit the active theme");
            }
            double shortWidth = combo.ActualWidth;
            combo.SelectedIndex = 12;
            Pump(80);
            double longWidth = combo.ActualWidth;
            var toggle = (ToggleButton)combo.Template.FindName("toggle", combo);
            var field = (Border)toggle.Template.FindName("bd", toggle);
            Require(field.CornerRadius.BottomLeft == 12, "Closed field lost its lower corners");
            double closedHeight = combo.ActualHeight;
            var closedLabelPosition = label.PointToScreen(new Point(0, 0));
            void CheckRowPosition(string state)
            {
                var position = label.PointToScreen(new Point(0, 0));
                Console.WriteLine($"Conversation row {state}: height={combo.ActualHeight:0.###} (closed {closedHeight:0.###}), label delta={position.X - closedLabelPosition.X:0.###}/{position.Y - closedLabelPosition.Y:0.###}px");
                Require(Math.Abs(combo.ActualHeight - closedHeight) < 0.01 &&
                    Math.Abs(position.X - closedLabelPosition.X) < 0.01 && Math.Abs(position.Y - closedLabelPosition.Y) < 0.01,
                    "Opening or closing changes the row height and shifts the conversation label");
            }
            combo.IsDropDownOpen = true;
            Require(!SystemParameters.ClientAreaAnimation || field.CornerRadius.BottomLeft > 0,
                "Opening snaps the lower corners square instead of interpolating them");
            Pump(55);
            CheckRowPosition("opening");
            var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
            Require(PresentationSource.FromVisual(popup.Child) != null, "Menu did not open");
            var menu = (Border)combo.Template.FindName("MenuFace", combo);
            Require(menu.Clip is RectangleGeometry, "Menu is missing its rounded reveal");
            var reveal = (RectangleGeometry)menu.Clip;
            Require(!SystemParameters.ClientAreaAnimation ||
                (reveal.Rect.Bottom > 0 && reveal.Rect.Bottom < menu.ActualHeight),
                "The drawer appears at its full height instead of revealing downwards");
            Pump(210);
            CheckRowPosition("open");
            Require(Math.Abs(reveal.Rect.Bottom - menu.ActualHeight) < 0.01 && reveal.RadiusX == 12,
                "The reveal does not finish at the rounded bottom edge");
            Require(field.CornerRadius.BottomLeft == 0 && field.CornerRadius.TopLeft == 12,
                "Open field and menu do not share a continuous outline");
            var fieldLeft = field.PointToScreen(new Point(0, 0));
            var fieldBottom = field.PointToScreen(new Point(0, field.ActualHeight));
            var menuTop = menu.PointToScreen(new Point(0, 0));
            var fieldRight = field.PointToScreen(new Point(field.ActualWidth, 0));
            var menuRight = menu.PointToScreen(new Point(menu.ActualWidth, 0));
            Console.WriteLine($"Menu geometry: short/long width={shortWidth:0.##}/{longWidth:0.##}, left delta={menuTop.X - fieldLeft.X:0.##}px, right delta={menuRight.X - fieldRight.X:0.##}px, gap={menuTop.Y - fieldBottom.Y:0.##}px");
            Require(Math.Abs(shortWidth - 380) < 0.01 && Math.Abs(longWidth - 380) < 0.01,
                "Conversation field changes width with its title instead of staying at 380 DIPs");
            Require(Math.Abs(menuTop.X - fieldLeft.X) <= 1 && Math.Abs(menuRight.X - fieldRight.X) <= 1,
                "Menu edges do not align with the conversation field");
            Require(menuTop.Y <= fieldBottom.Y && fieldBottom.Y - menuTop.Y <= 2,
                "Menu leaves a gap or excessive overlap below the conversation field");
            combo.SelectedIndex = 12;
            combo.IsDropDownOpen = false;
            Pump(45);
            CheckRowPosition("closing");
            Require(!SystemParameters.ClientAreaAnimation || PresentationSource.FromVisual(popup.Child) != null,
                "Menu window disappeared before its closing animation could play");
            Require(!SystemParameters.ClientAreaAnimation || field.CornerRadius.BottomLeft < 12,
                "Closing snaps the lower corners back before the drawer has retracted");
            Pump(200);
            CheckRowPosition("closed");
            Require(PresentationSource.FromVisual(popup.Child) == null, "Closing animation never released the popup");
            Require(combo.SelectedIndex == 12, "Selection changed during the exit animation");
            Require(field.CornerRadius.BottomLeft == 12, "Closed field did not recover its lower corners");

            combo.IsDropDownOpen = true;
            Pump(200);
            var reopenedTop = menu.PointToScreen(new Point(0, 0));
            Console.WriteLine($"Reopened menu gap={reopenedTop.Y - fieldBottom.Y:0.##}px");
            Require(reopenedTop.Y <= fieldBottom.Y && fieldBottom.Y - reopenedTop.Y <= 2,
                "Reopening the menu introduces a gap below the conversation field");
            var scroll = Find<ScrollViewer>(popup.Child);
            var bar = (ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar", scroll);
            Require(bar != null && bar.ActualWidth <= 10, "Scrollbar is still the full-width system control");
            var thumb = Find<Thumb>(bar!);
            var face = (Border?)thumb.Template.FindName("ThumbFace", thumb);
            Require(face != null && FaceWidth(face, thumb) <= 4 && face.CornerRadius.TopLeft > 0,
                "Scrollbar needs a slim, rounded visible thumb");
            // Set the protected state directly: synthetic mouse events are cancelled by
            // WPF when the physical mouse button is released.
            typeof(Thumb).GetProperty(nameof(Thumb.IsDragging))!.SetValue(thumb, true);
            Pump(65);
            double growingWidth = FaceWidth(face!, thumb);
            Console.WriteLine($"Scrollbar width during expansion={growingWidth:0.##} DIPs");
            Require(!SystemParameters.ClientAreaAnimation || (growingWidth > 4.3 && growingWidth < 6),
                "Scrollbar does not grow with a decelerating transition while dragging");
            Pump(130);
            Require(Math.Abs(FaceWidth(face!, thumb) - 6) < 0.01,
                "Scrollbar does not expand to 6 DIPs while dragging");
            typeof(Thumb).GetProperty(nameof(Thumb.IsDragging))!.SetValue(thumb, false);
            Pump(260);
            Require(Math.Abs(FaceWidth(face!, thumb) - 3) < 0.01,
                "Scrollbar does not return to its slim resting width");
            scroll.ScrollToBottom();
            Pump(60);
            Require(scroll.VerticalOffset > 0 && bar!.Value > 0, "Scrolling and thumb position are not connected");
            double beforeDrag = scroll.VerticalOffset;
            thumb.RaiseEvent(new DragStartedEventArgs(0, 0));
            thumb.RaiseEvent(new DragDeltaEventArgs(0, -60));
            thumb.RaiseEvent(new DragCompletedEventArgs(0, -60, false));
            Pump(60);
            Require(scroll.VerticalOffset < beforeDrag, "Dragging the thumb did not scroll the conversation list");

            combo.IsDropDownOpen = false;
            Pump(30);
            combo.IsDropDownOpen = true;
            Pump(240);
            CheckRowPosition("rapid reopen");
            Require(combo.IsDropDownOpen && PresentationSource.FromVisual(popup.Child) != null,
                "A stale close animation closed a reopened menu");
            Require(Math.Abs(reveal.Rect.Bottom - menu.ActualHeight) < 0.01 && field.CornerRadius.BottomLeft == 0,
                "A rapid reversal leaves the drawer clipped or its joining corners rounded");
            var finalTop = menu.PointToScreen(new Point(0, 0));
            Require(finalTop.Y <= fieldBottom.Y && fieldBottom.Y - finalTop.Y <= 2,
                "A rapid reopen leaves a gap below the conversation field");
        }
        finally
        {
            combo.IsDropDownOpen = false;
            Pump(200);
            host.Close();
            ThemeManager.SetMode(originalTheme, Application.Current);
        }
    }

    static double FaceWidth(Border face, Thumb thumb) => face.TransformToAncestor(thumb)
        .TransformBounds(new Rect(0, 0, face.ActualWidth, face.ActualHeight)).Width;

    static T Find<T>(DependencyObject root) where T : DependencyObject
        => FindOrNull<T>(root) ?? throw new Exception("Missing " + typeof(T).Name);

    static T? FindOrNull<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindOrNull<T>(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }

    static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var stop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        stop.Tick += (_, _) => { stop.Stop(); frame.Continue = false; };
        stop.Start();
        Dispatcher.PushFrame(frame);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
